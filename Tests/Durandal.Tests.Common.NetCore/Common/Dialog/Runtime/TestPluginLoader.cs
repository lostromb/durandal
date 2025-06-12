
namespace Durandal.Tests.Common.Dialog.Runtime
{
    using Durandal.API;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
using Durandal.Tests.Common.Dialog.Runtime;

    public class TestPluginLoader : IDurandalPluginLoader
    {
        private BasicPluginLoader _loader;

        public BasicAnswer BasicAnswer = new BasicAnswer();
        public BasicTreeAnswer BasicTreeAnswer = new BasicTreeAnswer();
        public SideSpeechAnswer SideSpeechAnswer = new SideSpeechAnswer();
        public RetryTreeAnswer RetryTreeAnswer = new RetryTreeAnswer();
        public ClientCapsAnswer ClientCapsAnswer = new ClientCapsAnswer();
        public SandboxAnswer SandboxAnswer = new SandboxAnswer();
        public CrossDomainAnswer CrossDomainAnswerA = new CrossDomainAnswer("crossdomain_a");
        public CrossDomainAnswer CrossDomainAnswerB = new CrossDomainAnswer("crossdomain_b");
        public CrossDomainSuperAnswer CrossDomainSuper = new CrossDomainSuperAnswer();
        public CrossDomainSubAnswer CrossDomainSub = new CrossDomainSubAnswer();
        public TriggerAnswer TriggerAnswerA = new TriggerAnswer("trigger_a");
        public TriggerAnswer TriggerAnswerB = new TriggerAnswer("trigger_b");
        public TriggerAnswer TriggerAnswerSlow = new TriggerAnswer("trigger_slow");
        public ReflectionAnswer ReflectionAnswer = new ReflectionAnswer();
        public EntitiesAnswer EntitiesAnswerA = new EntitiesAnswer("entities_a");
        public EntitiesAnswer EntitiesAnswerB = new EntitiesAnswer("entities_b");
        public RemotingPlugin RemotingPlugin = new RemotingPlugin();

        public TestPluginLoader(IDialogExecutor executor)
        {
            _loader = new BasicPluginLoader(executor, NullFileSystem.Singleton);
            _loader.RegisterPluginType(BasicAnswer);
            _loader.RegisterPluginType(BasicTreeAnswer);
            _loader.RegisterPluginType(SideSpeechAnswer);
            _loader.RegisterPluginType(RetryTreeAnswer);
            _loader.RegisterPluginType(ClientCapsAnswer);
            _loader.RegisterPluginType(SandboxAnswer);
            _loader.RegisterPluginType(CrossDomainAnswerA);
            _loader.RegisterPluginType(CrossDomainAnswerB);
            _loader.RegisterPluginType(CrossDomainSuper);
            _loader.RegisterPluginType(CrossDomainSub);
            _loader.RegisterPluginType(TriggerAnswerA);
            _loader.RegisterPluginType(TriggerAnswerB);
            _loader.RegisterPluginType(TriggerAnswerSlow);
            _loader.RegisterPluginType(ReflectionAnswer);
            _loader.RegisterPluginType(EntitiesAnswerA);
            _loader.RegisterPluginType(EntitiesAnswerB);
            _loader.RegisterPluginType(RemotingPlugin);
        }

        public void ResetAllPlugins()
        {
            BasicAnswer.Reset();
            BasicTreeAnswer.Reset();
            SideSpeechAnswer.Reset();
            RetryTreeAnswer.Reset();
        }

        public Task<DialogProcessingResponse> LaunchPlugin(PluginStrongName pluginId, string entryPoint, bool isRetry, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger, IRealTimeProvider realTime)
        {
            return _loader.LaunchPlugin(pluginId, entryPoint, isRetry, query, services, queryLogger, realTime);
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(PluginStrongName pluginId, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger, IRealTimeProvider realTime)
        {
            return _loader.TriggerPlugin(pluginId, query, services, queryLogger, realTime);
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(PluginStrongName pluginId, string targetIntent, ILogger queryLogger, IRealTimeProvider realTime)
        {
            return _loader.CrossDomainRequest(pluginId, targetIntent, queryLogger, realTime);
        }

        public Task<CrossDomainResponseResponse> CrossDomainResponse(PluginStrongName pluginId, CrossDomainContext context, IPluginServicesInternal services, ILogger queryLogger, IRealTimeProvider realTime)
        {
            return _loader.CrossDomainResponse(pluginId, context, services, queryLogger, realTime);
        }

        public Task<LoadedPluginInformation> LoadPlugin(PluginStrongName pluginId, IPluginServicesInternal localServices, ILogger logger, IRealTimeProvider realTime)
        {
            return _loader.LoadPlugin(pluginId, localServices, logger, realTime);
        }

        public Task<bool> UnloadPlugin(PluginStrongName pluginId, IPluginServicesInternal localServices, ILogger logger, IRealTimeProvider realTime)
        {
            return _loader.UnloadPlugin(pluginId, localServices, logger, realTime);
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return _loader.GetAllAvailablePlugins(realTime);
        }

        public  Task<CachedWebData> FetchPluginViewData(PluginStrongName plugin, string path, DateTimeOffset? ifModifiedSince, ILogger traceLogger, IRealTimeProvider realTime)
        {
            return _loader.FetchPluginViewData(plugin, path, ifModifiedSince, traceLogger, realTime);
        }
     }
}
