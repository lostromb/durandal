using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.CoreTests
{
    public class UnitTestPlugin : DurandalPlugin
    {
        private bool _isLoaded;

        public UnitTestPlugin() : base("unit_test")
        {
            _isLoaded = false;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            // Make sure we can load sample data
            services.Logger.Log("Starting to load unit test plugin...");
            VirtualPath pluginDataFile = services.PluginDataDirectory.Combine("sample_data.txt");
            if (!(await services.FileSystem.ExistsAsync(pluginDataFile)))
            {
                services.Logger.Log("Plugin data file " + pluginDataFile + " was not found at startup", LogLevel.Err);
                throw new Exception("Plugin data file " + pluginDataFile + " was not found at startup");
            }

            List<string> lines = (await services.FileSystem.ReadLinesAsync(pluginDataFile).ConfigureAwait(false)).ToList();
            if (lines.Count != 1 || !string.Equals("this is sample plugin data", lines[0]))
            {
                services.Logger.Log("Plugin data file " + pluginDataFile + " did not have correct content", LogLevel.Err);
                throw new Exception("Plugin data file " + pluginDataFile + " did not have correct content");
            }

            _isLoaded = true;
        }

        public override Task OnUnload(IPluginServices services)
        {
            _isLoaded = false;
            return DurandalTaskExtensions.NoOpTask;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            returnVal.AddStartState("basic_test", RunBasicTest);
            returnVal.AddStartState("oauth_1", OAuthTest1);
            returnVal.AddStartState("oauth_2", OAuthTest2);
            returnVal.AddStartState("view_data", ViewDataTest);
            return returnVal;
        }

        public async Task<PluginResult> RunBasicTest(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!_isLoaded)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Plugin was not properly loaded"
                };
            }

            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return new PluginResult(Result.Success)
            {
                ResponseText = "The test passed"
            };
        }

        private static readonly OAuthConfig _fakeOAuthConfig = new OAuthConfig()
        {
            Type = OAuthFlavor.OAuth2,
            ConfigName = "default",
            ClientId = "TestClientId",
            ClientSecret = "TestClientSecret",
            Scope = "read",
            AuthUri = "https://service.com/auth",
            TokenUri = "https://service.com/token"
        };

        public async Task<PluginResult> OAuthTest1(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!_isLoaded)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Plugin was not properly loaded"
                };
            }

            Uri oauthUri = await services.CreateOAuthUri(_fakeOAuthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            return new PluginResult(Result.Success)
            {
                ResponseText = oauthUri.AbsoluteUri
            };
        }

        public async Task<PluginResult> OAuthTest2(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!_isLoaded)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Plugin was not properly loaded"
                };
            }

            OAuthToken token = await services.TryGetOAuthToken(_fakeOAuthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            if (token == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ResponseText = "The oauth test failed"
                };
            }

            return new PluginResult(Result.Success)
            {
                ResponseText = "The oauth test passed"
            };
        }

        public async Task<PluginResult> ViewDataTest(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!_isLoaded)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Plugin was not properly loaded"
                };
            }

            services.Logger.Log("Loading from view directory at execution time...");
            VirtualPath viewDataFile = services.PluginViewDirectory.Combine("sample_view.html");
            if (!(await services.FileSystem.ExistsAsync(viewDataFile)))
            {
                services.Logger.Log("View data file " + viewDataFile + " was not found", LogLevel.Err);
                throw new Exception("View data file " + viewDataFile + " was not found");
            }

            List<string> lines = (await services.FileSystem.ReadLinesAsync(viewDataFile).ConfigureAwait(false)).ToList();
            if (lines.Count != 1 || !string.Equals("this is sample view data", lines[0]))
            {
                services.Logger.Log("View data file " + viewDataFile + " did not have correct content", LogLevel.Err);
                throw new Exception("View data file " + viewDataFile + " did not have correct content");
            }

            return new PluginResult(Result.Success)
            {
                ResponseHtml = lines[0],
                ResponseUrl = "/views/unit_test/sample_view.html"
            };
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "UnitTests",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Unit Tests",
                ShortDescription = "This is a package intended for unit tests only",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Run a test");

            return returnVal;
        }
    }
}
