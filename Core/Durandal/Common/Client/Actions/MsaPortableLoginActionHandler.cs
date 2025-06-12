using Durandal.API;
using Durandal.Common.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Security.Login.Providers;
using System.Threading;
using Durandal.Common.Security.Login;
using Durandal.Common.Time;

namespace Durandal.Common.Client.Actions
{
    /// <summary>
    /// Implements the MSAPortableLoginAction action
    /// </summary>
    public class MsaPortableLoginActionHandler : IJsonClientActionHandler
    {
        private readonly ISet<string> _supportedActions = new HashSet<string>();
        private readonly MSAPortableLoginProvider _loginProvider;
        private readonly TimeSpan _loginDelay;

        public MsaPortableLoginActionHandler(MSAPortableLoginProvider loginProvider, TimeSpan loginDelay)
        {
            _supportedActions.Add(MSAPortableLoginAction.ActionName);
            _loginProvider = loginProvider;
            _loginDelay = loginDelay;
        }

        public async Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            queryLogger.Log("Starting a login process for MSA account", LogLevel.Wrn);
            
            try
            {
                MSAPortableLoginAction parsedAction = action.ToObject<MSAPortableLoginAction>();
                using (CancellationTokenSource cancelToken2 = new NonRealTimeCancellationTokenSource(realTime, _loginDelay))
                using (CancellationTokenSource jointCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, cancelToken2.Token))
                {
                    UserIdentity identity = await source.RegisterNewAuthenticatedUser("msa-portable", parsedAction.ExternalToken, jointCancellationSource.Token, realTime).ConfigureAwait(false);

                    if (identity == null)
                    {
                        queryLogger.Log("Login did not complete", LogLevel.Err);
                    }
                    else
                    {
                        source.SetActiveUserIdentity(identity, realTime);

                        if (!string.IsNullOrEmpty(parsedAction.SuccessActionId))
                        {
                            // Invoke the success callback action
                            InputMethod actionMethod = parsedAction.IsSpeechEnabled.GetValueOrDefault(false) ? InputMethod.TactileWithAudio : InputMethod.Tactile;
                            bool callbackWentToClient = await source.TryMakeDialogActionRequest(parsedAction.SuccessActionId, actionMethod, realTime: realTime).ConfigureAwait(false);
                            if (!callbackWentToClient)
                            {
                                queryLogger.Log("Client core was too busy to honor the request to trigger the login success action.", LogLevel.Wrn);
                            }
                        }
                        else
                        {
                            queryLogger.Log("No callback action was specified for login, but it did succeed.");
                        }
                    }
                }
            }
            catch (JsonException e)
            {
                queryLogger.Log("Couldn't parse MSAPortableLoginAction action", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }
            catch (TaskCanceledException)
            {
                queryLogger.Log("Login expired before it could be completed.", LogLevel.Wrn);
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }
        }

        public ISet<string> GetSupportedClientActions()
        {
            return _supportedActions;
        }
    }
}
