
namespace Durandal.Plugins.WindowsShell
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    public class WindowsShellPlugin : DurandalPlugin
    {
        private IDictionary<string, string> _knownPrograms;

        public WindowsShellPlugin()
            : base("windows_shell")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _knownPrograms = new Dictionary<string, string>();

            // Load the available programs from the external config file
            VirtualPath programListFile = services.PluginDataDirectory + "\\programs.txt";
            if (!(await services.FileSystem.ExistsAsync(programListFile)))
            {
                return;
            }

            using (StreamReader fileIn = new StreamReader(await services.FileSystem.OpenStreamAsync(programListFile, FileOpenMode.Open, FileAccessMode.Read)))
            {
                while (!fileIn.EndOfStream)
                {
                    string[] parts = fileIn.ReadLine().Split('\t');
                    if (parts.Length == 2)
                    {
                        _knownPrograms.Add(parts[0], parts[1]);
                    }
                }

                fileIn.Close();
            }
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode shutdownComputerNode = returnVal.CreateNode(this.ShutdownComputer);
            IConversationNode confirmShutdownNode = returnVal.CreateNode(this.ConfirmShutdown);
            IConversationNode denyShutdownNode = returnVal.CreateNode(this.ConfirmShutdown);

            shutdownComputerNode.CreateCommonEdge("confirm", confirmShutdownNode);
            shutdownComputerNode.CreateCommonEdge("deny", denyShutdownNode);
            shutdownComputerNode.EnableRetry(RetryConfirmShutdown);

            returnVal.AddStartState("launch_app", this.LaunchApp);
            returnVal.AddStartState("lock_screen", this.LockScreen);
            returnVal.AddStartState("suspend_computer", this.SuspendComputer);
            returnVal.AddStartState("shutdown_computer", shutdownComputerNode);
            returnVal.AddStartState("take_screenshot", this.TakeScreenshot);

            return returnVal;
        }

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        private async Task<PluginResult>  RetryConfirmShutdown(QueryWithContext input, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (input.RetryCount < 2)
            {
                return new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                        ResponseText = "I'm sorry, I didn't catch that",
                        ResponseSsml = "I'm sorry, I didn't catch that"
                    };
            }
            else
            {
                return new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.None,
                    ResponseText = "I'm giving up now",
                    ResponseSsml = "I'm giving up now"
                };
            }
        }

        private async Task<PluginResult> GetNotAuthorizedResponse(QueryWithContext input, IPluginServices services)
        {
            return await services.LanguageGenerator.GetPattern("NotAuthorized", input.ClientContext, services.Logger)
                .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.None,
                        ErrorMessage = "Client must be authorized to use windows shell domain",
                    });
        }

        public async Task<PluginResult> LaunchApp(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.ClientAuthorized))
            {
                return await this.GetNotAuthorizedResponse(queryWithContext, services);
            }

            string appName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "appname");

            if (string.IsNullOrWhiteSpace(appName))
            {
                return new PluginResult(Result.Skip);
            }

            // Find the app that most closely matches the input
            float confidence = 0;

            string closestProgramMatch = appName;// DialogHelpers.RewriteSlotValue(appName, this.knownPrograms.Keys, languageTools.Pronouncer, out confidence);
            if (confidence < 0.90)
            {
                return await services.LanguageGenerator.GetPattern("DidNotUnderstandInput", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            string exeName = this._knownPrograms[closestProgramMatch];

            // And execute it
            services.Logger.Log("Launching application " + closestProgramMatch);
            ShellExecute(exeName, string.Empty, services.Logger);
            string text = "Starting " + closestProgramMatch;
            return new PluginResult(Result.Success)
                {
                    ResponseText = text,
                    ResponseSsml = text
                };
        }

        public static void ShellExecute(string programName, string args, ILogger logger)
        {
            try
            {
                Process.Start(programName, args);
            }
            catch (Exception exp)
            {
                logger.Log("Exception in ShellExecute: " + exp.Message, LogLevel.Err);
            }
        }

        public async Task<PluginResult> LockScreen(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.ClientAuthorized))
            {
                return await this.GetNotAuthorizedResponse(queryWithContext, services);
            }
            
            if (LockWorkStation())
                return await services.LanguageGenerator.GetPattern("ComputerLocked", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            else
                return await services.LanguageGenerator.GetPattern("CouldNotLockComputer", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
        }

        public async Task<PluginResult> ShutdownComputer(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.ClientAuthorized))
            {
                return await this.GetNotAuthorizedResponse(queryWithContext, services);
            }

            return await services.LanguageGenerator.GetPattern("ConfirmShutdown", queryWithContext.ClientContext, services.Logger)
                .ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        MultiTurnResult = MultiTurnBehavior.ContinueBasic
                    });
        }

        public async Task<PluginResult> SuspendComputer(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.ClientAuthorized))
            {
                return await this.GetNotAuthorizedResponse(queryWithContext, services);
            }
            
            // We must start the suspend routine in a separate process.
            // A regular call to SetSuspendState will block until the
            // computer wakes up again, and the dispatcher will get mad at us for
            // "taking too long" (and possibly report "An error occurred")
            Process.Start(new ProcessStartInfo()
                {
                    FileName = ".\\" + RuntimeDirectoryName.EXT_DIR + "\\suspend.exe",
                    Arguments = "3000",
                    CreateNoWindow = true,
                }
            );

            return await services.LanguageGenerator.GetPattern("GoingToSleep", queryWithContext.ClientContext, services.Logger)
                .ApplyToDialogResult(new PluginResult(Result.Success));
        }

        public async Task<PluginResult> ConfirmShutdown(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (queryWithContext.Understanding.Intent.Equals("confirm"))
            {
                //DurandalUtils.ShellExecute("shutdown", "-s -t 30");
                return await services.LanguageGenerator.GetPattern("GoingToShutdown", queryWithContext.ClientContext, services.Logger)
                    .Sub("t", "30")
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            return await services.LanguageGenerator.GetPattern("ShutdownCancelled", queryWithContext.ClientContext, services.Logger)
                .ApplyToDialogResult(new PluginResult(Result.Success));
        }

        public async Task<PluginResult> TakeScreenshot(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.IsOnLocalMachine))
            {
                return await services.LanguageGenerator.GetPattern("ScreenshotFailNotLocal", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success));
            }
            
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!Directory.Exists(desktopPath))
            {
                return await services.LanguageGenerator.GetPattern("ScreenshotFail", queryWithContext.ClientContext, services.Logger)
                    .ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            ErrorMessage = "Local user environment does not specify a desktop directory, therefore no place to save a screenshot"
                        });
            }
            
            string fileName = desktopPath + "\\Screenshot.jpg";
            int count = 1;
            while (File.Exists(fileName))
            {
                fileName = string.Format("{0}\\Screenshot_{1}.jpg", desktopPath, count++);
            }

            ScreenCapture.CaptureScreenToFile(fileName, ImageFormat.Jpeg);

            return await services.LanguageGenerator.GetPattern("ScreenshotSuccess", queryWithContext.ClientContext, services.Logger)
                .ApplyToDialogResult(new PluginResult(Result.Success));
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            MemoryStream pngStream = new MemoryStream();
            if (pluginDataDirectory != null && pluginDataManager != null)
            {
                VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                if (pluginDataManager.Exists(iconFile))
                {
                    using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        iconStream.CopyTo(pngStream);
                    }
                }
            }

            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "Windows Shell",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Windows Shell",
                ShortDescription = "Interfaces with the Windows desktop",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Take a screenshot");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Launch Internet Explorer");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Put the computer to sleep");

            return returnVal;
        }
    }
}
