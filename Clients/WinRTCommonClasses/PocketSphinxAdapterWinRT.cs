#if WINDOWS_PHONE_APP && ARM
using Durandal.Common.Speech.Triggers.Sphinx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sphinx_WP8;
using Durandal.Common.Logger;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    public class PocketSphinxAdapterWinRT : IPocketSphinx
    {
        private PSphinxTrigger _backend = new PSphinxTrigger();
        private int _disposed = 0;

        ~PocketSphinxAdapterWinRT()
        {
            Dispose(false);
        }

        public bool Create(string modelDir, string dictionaryFile, bool verboseLogging)
        {
            return _backend.trigger_create(modelDir, dictionaryFile, verboseLogging);
        }

        public bool Reconfigure(KeywordSpottingConfiguration config)
        {
            return _backend.trigger_reconfigure(SphinxHelpers.CreateKeywordFile(config, NullLogger.Singleton));
        }

        public bool Start()
        {
            return _backend.trigger_start_processing();
        }

        public bool Stop()
        {
            return _backend.trigger_stop_processing();
        }

        public bool IsSpeechDetected()
        {
            return _backend.trigger_get_in_speech();
        }

        public string ProcessForKws(short[] samples, int numSamples)
        {
            bool triggered = _backend.trigger_process_samples(samples, numSamples);

            if (triggered)
            {
                return _backend.trigger_get_last_hyp();
            }

            return null;
        }

        public void ProcessForVad(short[] samples, int numSamples)
        {
            _backend.trigger_process_samples(samples, numSamples);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            _backend.trigger_free();
            _backend = null;
        }
    }
}

#endif