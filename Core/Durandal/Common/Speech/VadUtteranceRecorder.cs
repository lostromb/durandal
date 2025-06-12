//using Durandal.Common.Utils;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Durandal.Common.File;
//using Durandal.Common.MathExt;
//using Durandal.Common.IO;
//using Durandal.Common.Speech;
//using Durandal.Common.Logger;
//using Durandal.Common.Audio;

//namespace Durandal.Common.Speech
//{
//    /// <summary>
//    /// Utterance recorder which uses PocketSphinx voice activity detector to infer when speaking has started / stopped
//    /// </summary>
//    public class VadUtteranceRecorder : IUtteranceRecorder
//    {
//        private const int INCREMENT_SIZE_MS = 10;
//        private const int VAD_AVERAGE_WINDOW_MS = 1600;
//        private const double VAD_TO_START = 0.5;
//        private const double VAD_TO_FINISH = 0.4;
//        private const int MAX_INITIAL_SILENCE_MS = 1500;

//        private readonly MovingAverage _movingAverageValue;
//        private readonly IVoiceActivityDetector _vad;
//        private readonly BasicBufferShort _inBuf;

//        private RecorderState _lastRecorderState;
//        private int _msRecorded = 0;
//        private bool _started = false;

//        public VadUtteranceRecorder(IVoiceActivityDetector vad)
//        {
//            _vad = vad;
//            _inBuf = new BasicBufferShort(48000);
//            _lastRecorderState = RecorderState.NotStarted;
//            _movingAverageValue = new MovingAverage(VAD_AVERAGE_WINDOW_MS / INCREMENT_SIZE_MS, 0);
//        }

//        public RecorderState ProcessInput(AudioChunk next)
//        {
//            _inBuf.Write(next.Data);
//            int desiredSliceSize = (int)((long)next.SampleRate * INCREMENT_SIZE_MS / 1000);
//            while (_inBuf.Available > desiredSliceSize)
//            {
//                AudioChunk slice = new AudioChunk(_inBuf.Read(desiredSliceSize), next.SampleRate);
//                _lastRecorderState = ProcessInputInternal(slice);
//            }

//            return _lastRecorderState;
//        }

//        private RecorderState ProcessInputInternal(AudioChunk next)
//        {
//            if (next == null)
//            {
//                return RecorderState.Error;
//            }

//            if (_movingAverageValue.Average < VAD_TO_FINISH && _msRecorded > MAX_INITIAL_SILENCE_MS)
//            {
//                if (_started)
//                    return RecorderState.Finished;
//                else
//                    return RecorderState.NothingRecorded;
//            }

//            _msRecorded += INCREMENT_SIZE_MS;
//            _vad.ProcessForVad(next.Data, next.DataLength);
//            _movingAverageValue.Add(_vad.IsSpeechDetected() ? 1 : 0);

//            if (_movingAverageValue.Average > VAD_TO_START)
//            {
//                _started = true;
//            }
            
//            if (_started)
//                return RecorderState.Speaking;
//            else
//                return RecorderState.NotStarted;
//        }
//    }
//}
