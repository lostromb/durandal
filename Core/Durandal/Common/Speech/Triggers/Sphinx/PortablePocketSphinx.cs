using Durandal.Common.Logger;
using Durandal.Common.Speech.Triggers.Sphinx.Internal;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    public class PortablePocketSphinx : IPocketSphinx
    {
        private readonly ILogger _logger;
        private readonly IFileSystem _fileManager;
        private readonly FileAdapter _fileAdapter;
        private readonly SphinxLogger _sphinxLogger;

        private PocketSphinx _ps;
        private bool _utt_started;
        private bool _user_is_speaking;
        private bool _triggered;
        private Pointer<byte> _last_hyp;
        private int _disposed = 0;

        public PortablePocketSphinx(IFileSystem fileManager, ILogger logger)
        {
            _fileManager = fileManager;
            _logger = logger;
            _fileAdapter = new FileAdapter(_fileManager);
            _sphinxLogger = new SphinxLogger(logger);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PortablePocketSphinx()
        {
            Dispose(false);
        }
#endif

        public bool Create(string modelDir, string dictionaryFile, bool verboseLogging)
        {
            //printf("            creating recognizer\n");

            cmd_ln_t config = null;

            if (verboseLogging)
            {
                config = CommandLine.cmd_ln_init(config, PocketSphinx.ps_args(), 1, _sphinxLogger,
                    "-hmm", modelDir,
                    "-dict", dictionaryFile,
                    "-verbose", "y");
            }
            else
            {
                config = CommandLine.cmd_ln_init(config, PocketSphinx.ps_args(), 1, _sphinxLogger,
                    "-hmm", modelDir,
                    "-dict", dictionaryFile);
            }

            _ps = PocketSphinx.ps_init(config, _fileAdapter, _sphinxLogger);

            _user_is_speaking = false;
            _last_hyp = PointerHelpers.Malloc<byte>(512);
            _last_hyp[0] = 0;

            return true;
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
            }
        }

        public void ProcessForVad(short[] samples, int numSamples)
        {
            Process(samples, numSamples);
        }

        public string ProcessForKws(short[] samples, int numSamples)
        {
            return Process(samples, numSamples);
        }

        public string Process(short[] samples, int numSamples)
        {
            _ps.ps_process_raw(new Pointer<short>(samples), (uint)numSamples, 0, 0);
            byte in_speech = _ps.ps_get_in_speech();
            if (in_speech != 0 && !_user_is_speaking)
            {
                _user_is_speaking = true;
            }
            
            BoxedValueInt score = new BoxedValueInt();
            Pointer<byte> hyp = _ps.ps_get_hyp(score);

            if (hyp.IsNonNull)
            {
                //printf("            tenative hyp %s\n", hyp);
                if (!_triggered)
                {
                    _triggered = true;
                    uint hypsize = cstring.strlen(hyp);
                    cstring.strncpy(_last_hyp, hyp, hypsize);
                    _last_hyp[hypsize] = 0;
                    //printf("            adapter last hyp is %s\n", hyp);
                }
            }

            if (in_speech == 0 && _user_is_speaking)
            {
                /* speech->silence transition, time to start new utterance  */
                _ps.ps_end_utt();
                _utt_started = false;

                hyp = _ps.ps_get_hyp(score);

                if (hyp.IsNonNull)
                {
                    //printf("            final hyp %s\n", hyp);
                    if (!_triggered)
                    {
                        _triggered = true;
                        uint hypsize = cstring.strlen(hyp);
                        cstring.strncpy(_last_hyp, hyp, hypsize);
                        _last_hyp[hypsize] = 0;
                        //printf("            adapter last hyp is %s\n", hyp);
                    }
                }

                if (_ps.ps_start_utt() < 0)
                {
                    //printf("            failed to restart utterance\n");
                }
                _utt_started = true;

                _user_is_speaking = false;
                _triggered = false;
                //printf("Ready....\n");
            }

            if (cstring.strlen(_last_hyp) > 0)
            {
                string returnVal = cstring.FromCString(_last_hyp);
                if (_last_hyp.IsNonNull)
                {
                    _last_hyp[0] = (byte)'\0';
                }

                return returnVal;
            }

            return null;
        }

        public bool IsSpeechDetected()
        {
            return _ps.ps_get_in_speech() != 0;
        }

        public bool Reconfigure(KeywordSpottingConfiguration keywordConfig)
        {
            return Reconfigure(SphinxHelpers.CreateKeywordFile(keywordConfig, _logger));
        }

        public bool Reconfigure(string keywordFile)
        {
            //printf("            reconfiguring %s\n", keyfile);

            if (this._utt_started)
            {
                _ps.ps_end_utt();
            }

            if (_ps.ps_set_kws(cstring.ToCString("keyword_search"), cstring.ToCString(keywordFile)) != 0)
            {
                return false;
            }

            if (_ps.ps_set_search(cstring.ToCString("keyword_search")) != 0)
            {
                return false;
            }

            if (this._utt_started)
            {
                _ps.ps_start_utt();
            }

            return true;
        }

        public bool Start()
        {
            //printf("            process start\n");
            
            _utt_started = true;
            return _ps.ps_start_utt() == 0; // todo use ps_start_stream?
        }

        public bool Stop()
        {
            //printf("            process stop\n");
            
            if (_utt_started)
            {
                _ps.ps_end_utt();
                _utt_started = false;
                if (_last_hyp.IsNonNull)
                {
                    _last_hyp[0] = (byte)'\0';
                }
            }

            return true;
        }
    }
}
