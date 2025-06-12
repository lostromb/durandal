using Durandal.API;
using Durandal.Common.Logger;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Durandal.Common.Client.Actions;
using Durandal.Common.Client;
using Windows.UI.Core;
using Durandal;
using Durandal.Common.Time;

namespace DurandalWinRT
{
    /// <summary>
    /// Opens OAuth login URLs using Windows.System.Launcher
    /// </summary>
    public class OAuthLoginActionHandlerWinRT : IJsonClientActionHandler
    {
        private readonly ISet<string> _supportedActions = new HashSet<string>();
        private readonly CoreDispatcher _windowsUIThreadDispatcher;

        public OAuthLoginActionHandlerWinRT(CoreDispatcher uiThreadDispatcher)
        {
            _supportedActions.Add(OAuthLoginAction.ActionName);
            _windowsUIThreadDispatcher = uiThreadDispatcher;
        }

        public async Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                OAuthLoginAction parsedAction = action.ToObject<OAuthLoginAction>();
                queryLogger.Log("Handling oauth login action with URL " + parsedAction.LoginUrl, LogLevel.Vrb);
                Uri loginUri = new Uri(parsedAction.LoginUrl);
                await _windowsUIThreadDispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(
                    async () =>
                    {
                        try
                        {
                            bool launchSuccess = await Windows.System.Launcher.LaunchUriAsync(loginUri);
                            if (launchSuccess)
                            {
                                queryLogger.Log("Successfully launched external OAuth URI in system browser");
                            }
                            else
                            {
                                queryLogger.Log("FAILED to launch external OAuth URI in system browser: Unknown error", LogLevel.Err);
                            }
                        }
                        catch (Exception e)
                        {
                            queryLogger.Log("FAILED to launch external OAuth URI in system browser", LogLevel.Err);
                            queryLogger.Log(e, LogLevel.Err);
                        }
                    }));
            }
            catch (JsonException e)
            {
                queryLogger.Log("Failed to parse OAuthLoginAction object: " + action.ToString(), LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }
            catch (Exception e)
            {
                queryLogger.Log("Exception while handling OAuthLoginAction: " + action.ToString(), LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
        }
    }
}
