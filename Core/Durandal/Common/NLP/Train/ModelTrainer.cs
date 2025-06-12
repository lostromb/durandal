using Durandal.Common.Statistics.Classification;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Tagging;
using Durandal.Common.NLP.Train;
using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.NLP.Train
{
    public abstract class ModelTrainer : IDisposable
    {
        private readonly EventWaitHandle _doneTrigger = new EventWaitHandle(false, EventResetMode.ManualReset);
        private int _disposed = 0;

        public ModelTrainer()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ModelTrainer()
        {
            Dispose(false);
        }
#endif

        public void Join()
        {
            _doneTrigger.WaitOne();
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
                _doneTrigger?.Dispose();
            }
        }

        protected void Done()
        {
            _doneTrigger.Set();
        }
    }
}
