using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Config;
using Durandal.Common.Config.Accessors;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Extensions.BondProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Service
{
    public class DialogInteractiveConsole : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IDialogClient _dialogClient;
        private readonly WeakPointer<DialogProcessingEngine> _dialogEngine;
        private readonly IRealTimeProvider _realTime;
        private readonly string _clientId;
        private readonly IConfigValue<bool> _consoleEnabledInConfigAccessor;
        private readonly AutoResetEventAsync _consoleStateChangedSignal;
        private readonly CancellationTokenSource _cancelTokenSource;
        private readonly CancellationToken _cancelToken;
        private int _disposed = 0;

        public DialogInteractiveConsole(
            ILogger logger,
            WeakPointer<DialogProcessingEngine> dialogEngine,
            IHttpClientFactory httpClientFactory,
            Uri localDialogServiceUri,
            IConfiguration dialogConfig)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _dialogEngine = dialogEngine.AssertNonNull(nameof(dialogEngine));
            _realTime = DefaultRealTimeProvider.Singleton;
            _clientId = StringUtils.HashToGuid(Environment.MachineName).ToString("N");

            IDialogTransportProtocol testConsoleProtocol = new DialogBondTransportProtocol();
            IHttpClient dialogHttpClient = httpClientFactory.CreateHttpClient(localDialogServiceUri.AssertNonNull(nameof(localDialogServiceUri)), _logger.Clone("TestConsoleHttp"));
            _dialogClient = new DialogHttpClient(dialogHttpClient, _logger.Clone("TestConsoleClient"), testConsoleProtocol);
            _consoleEnabledInConfigAccessor = dialogConfig.CreateBoolAccessor(_logger, "enableInteractiveConsole", false);
            _consoleStateChangedSignal = new AutoResetEventAsync();
            _consoleEnabledInConfigAccessor.ChangedEvent.Subscribe(HandleConsoleStatusConfigChangedEvent);
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~DialogInteractiveConsole()
        {
            Dispose(false);
        }

        public void ExitConsole()
        {
            _cancelTokenSource.Cancel();
        }

        public async Task Run()
        {
            try
            {
                bool consoleHelpShown = false;
                while (!_cancelToken.IsCancellationRequested)
                {
                    if (!_consoleEnabledInConfigAccessor.Value)
                    {
                        await _consoleStateChangedSignal.WaitAsync(_cancelToken);
                    }
                    else
                    {
                        if (!consoleHelpShown)
                        {
                            _logger.Log("Test console client ID is " + _clientId);
                            _logger.Log(" \"/quit\" - exit");
                            _logger.Log(" \"/load {plugin-id}\" - dynamically load a plugin with given id");
                            _logger.Log(" \"/unload\" - unload all plugins");
                            _logger.Log(" \"/unload {plugin-id}\" - dynamically unload a plugin with given id");
                            _logger.Log(" (any other input) - Run a specific text query");
                            consoleHelpShown = true;
                        }

                        // Warning: this will block the current thread forever which could potentially contribute to thread pool starvation
                        string userInput = Console.ReadLine();

                        if (string.IsNullOrEmpty(userInput))
                        {
                            continue;
                        }

                        if (userInput.StartsWith("/quit", StringComparison.OrdinalIgnoreCase) ||
                            userInput.StartsWith("/exit", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Log("Shutting down...");
                            ExitConsole();
                        }
                        else if (userInput.StartsWith("/load", StringComparison.OrdinalIgnoreCase))
                        {
                            string pluginId = userInput.Substring("/load".Length).Trim();
                            if (string.IsNullOrEmpty(pluginId))
                            {
                                _logger.Log("/load requires a plugin ID to be loaded, such as \"/load myplugin\"", LogLevel.Wrn);
                            }
                            else
                            {
                                await _dialogEngine.Value.LoadPlugin(pluginId, _realTime);
                            }
                        }
                        else if (userInput.StartsWith("/unload", StringComparison.OrdinalIgnoreCase))
                        {
                            string pluginId = userInput.Substring("/unload".Length).Trim();
                            if (string.IsNullOrEmpty(pluginId))
                            {
                                await _dialogEngine.Value.UnloadAllPlugins(_realTime);
                            }
                            else
                            {
                                await _dialogEngine.Value.UnloadPlugin(pluginId, _realTime);
                            }
                        }
                        else if (userInput.StartsWith("/reload", StringComparison.OrdinalIgnoreCase))
                        {
                            //await PackageInstaller.InstallNewOrUpdatedPackages(coreLogger, fileSystem, packageLoader, PackageComponent.Dialog, realTimeDefinition);
                            // FIXME Does this even work?
                            await _dialogEngine.Value.SetLoadedPlugins(_dialogEngine.Value.GetLoadedPlugins(), _realTime, reloadAll: true);
                            continue;
                        }
                        else
                        {
                            await HandleTestConsoleQuery(userInput);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _consoleEnabledInConfigAccessor?.ChangedEvent?.Unsubscribe(HandleConsoleStatusConfigChangedEvent);
                _consoleEnabledInConfigAccessor?.Dispose();
                _dialogClient?.Dispose();
                _cancelTokenSource?.Cancel();
                _cancelTokenSource?.Dispose();
            }
        }

        private Task HandleConsoleStatusConfigChangedEvent(object source, ConfigValueChangedEventArgs<bool> args, IRealTimeProvider realTime)
        {
            _consoleStateChangedSignal.Set();
            return DurandalTaskExtensions.NoOpTask;
        }

        private async Task HandleTestConsoleQuery(string query)
        {
            // Send query
            DialogRequest request = new DialogRequest();
            request.InteractionType = InputMethod.Typed;
            request.ClientContext.ClientId = _clientId;
            request.ClientContext.UserId = _clientId;
            request.PreferredAudioCodec = "opus";
            request.ClientContext.Locale = LanguageCode.EN_US;
            request.ClientContext.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            request.ClientContext.ClientName = "debugclient";
            request.ClientContext.ExtraClientContext[ClientContextField.ClientType] = "DEBUG_CONSOLE";
            request.ClientContext.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            request.ClientContext.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Unknown.ToString();

            // todo: use geoip
            request.ClientContext.Latitude = 47.617108;
            request.ClientContext.Longitude = -122.191346;

            request.ClientContext.SetCapabilities(
                ClientCapabilities.DisplayUnlimitedText |
                ClientCapabilities.DisplayHtml5 |
                ClientCapabilities.HasSpeakers |
                ClientCapabilities.SupportsCompressedAudio |
                ClientCapabilities.ServeHtml |
                ClientCapabilities.SupportsStreamingAudio);
            request.TextInput = query;
            request.RequestFlags = (QueryFlags.Debug);

            Stopwatch latency = new Stopwatch();
            latency.Start();
            using (NetworkResponseInstrumented<DialogResponse> networkResponse =
                await _dialogClient.MakeQueryRequest(request, _logger, _cancelToken, _realTime))
            {
                latency.Stop();

                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder message = pooledSb.Builder;
                    if (networkResponse == null || !networkResponse.Success || networkResponse.Response == null)
                    {
                        message.AppendLine("Got null response! The dialog engine is not properly configured");
                    }
                    else
                    {
                        DialogResponse response = networkResponse.Response;

                        message.AppendLine("Got dialog response:");
                        message.AppendLine("Protocol version = " + response.ProtocolVersion);
                        message.AppendLine("Result = " + response.ExecutionResult);
                        if (!string.IsNullOrEmpty(response.ResponseText))
                        {
                            message.AppendLine("Response text = " + response.ResponseText);
                        }
                        if (!string.IsNullOrEmpty(response.ErrorMessage))
                        {
                            message.AppendLine("Response error message = " + response.ErrorMessage);
                        }
                        if (!string.IsNullOrEmpty(response.ResponseUrl))
                        {
                            message.AppendLine("Response Url = " + response.ResponseUrl);
                            message.AppendLine("Url scope = " + response.UrlScope.ToString());
                        }
                        if (response.ResponseAudio != null && response.ResponseAudio.Data != null &&
                            response.ResponseAudio.Data.Count > 0)
                        {
                            string audioInfo = string.Format("Response audio = length {0} codec {1} {2}", response.ResponseAudio.Data.Count, response.ResponseAudio.Codec, response.ResponseAudio.CodecParams);
                            message.AppendLine(audioInfo);
                        }
                        else if (!string.IsNullOrEmpty(response.StreamingAudioUrl))
                        {
                            message.AppendLine("Recieved streaming audio over this URL: " + response.StreamingAudioUrl);
                        }
                        else
                        {
                            message.AppendLine("No response audio");
                        }
                        message.AppendLine("Continue Immediately = " + response.ContinueImmediately);
                        if (response.ResponseData != null && response.ResponseData.Count > 0)
                        {
                            message.AppendLine("Got custom response data:");
                            foreach (var line in response.ResponseData)
                            {
                                message.AppendLine(string.Format("    \"{0}\" = \"{1}\"", line.Key, line.Value));
                            }
                        }
                        if (!string.IsNullOrEmpty(response.AugmentedFinalQuery))
                        {
                            message.AppendLine("Augmented Query = " + response.AugmentedFinalQuery);
                        }
                        if (response.SelectedRecoResult != null)
                        {
                            message.AppendLine(string.Format("Selected Reco Result = {0}/{1}/{2}", response.SelectedRecoResult.Domain, response.SelectedRecoResult.Intent, response.SelectedRecoResult.Confidence));
                        }
                    }

                    message.AppendLine("Latency was " + latency.ElapsedMilliseconds);

                    //if (response != null && !string.IsNullOrEmpty(response.HtmlToDisplay))
                    //{
                    //    message.AppendLine("\r\n--- RESPONSE HTML FOLLOWS ---\r\n");
                    //    message.Append(response.HtmlToDisplay);
                    //}

                    Console.Write(message.ToString());
                }
            }
        }
    }
}
