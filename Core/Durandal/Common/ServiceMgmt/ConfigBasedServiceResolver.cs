using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.ServiceMgmt
{
    public abstract class ConfigBasedServiceResolver<T> : ServiceResolver<T> where T : class
    {
        private readonly Committer _changeCommitter;
        private readonly WeakPointer<IConfiguration> _config;
        private int _disposed;

        public ConfigBasedServiceResolver(WeakPointer<IConfiguration> config, ILogger errorLogger, IRealTimeProvider realTime) : base(errorLogger)
        {
            _changeCommitter = new Committer(
                RunCommitter,
                realTime,
                commitmentDelay: TimeSpan.FromMilliseconds(1000),
                maxCommitmentDelay: TimeSpan.FromMilliseconds(2000));

            _config = config.AssertNonNull(nameof(config));
            _config.Value.ConfigValueChangedEvent.Subscribe(HandleConfigChanged);
        }

        private async Task RunCommitter(IRealTimeProvider realTime)
        {
            RetrieveResult<T> newImpl = await CreateNewImpl(_config.Value, realTime).ConfigureAwait(false);
            if (newImpl.Success)
            {
                base.SetServiceImplementation(newImpl.Result, TimeToWaitBeforeDisposingOldImplementations);
            }
        }

        protected virtual TimeSpan TimeToWaitBeforeDisposingOldImplementations => TimeSpan.FromSeconds(60);

        public Task HandleConfigChanged(object sender, ConfigValueChangedEventArgs<string> args, IRealTimeProvider realTime)
        {
            _changeCommitter.Commit();
            return DurandalTaskExtensions.NoOpTask;
        }

        protected abstract Task<RetrieveResult<T>> CreateNewImpl(IConfiguration config, IRealTimeProvider realTime);

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                _config.Value.ConfigValueChangedEvent.Unsubscribe(HandleConfigChanged);

                if (disposing)
                {
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
