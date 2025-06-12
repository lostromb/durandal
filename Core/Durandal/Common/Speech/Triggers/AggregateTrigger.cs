//using Durandal.Common.Audio;
//using Durandal.Common.Time;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Durandal.Common.Speech.Triggers
//{
//    public class AggregateTrigger : IAudioTrigger
//    {
//        private int _overlapTime;
//        private IList<IAudioTrigger> _triggers;
//        private IDictionary<IAudioTrigger, long> _triggerTimes;
//        private long currentMillisecond = 0;
//        private AggregateBehavior _behavior;
//        private int _disposed = 0;

//        public event EventHandler<AudioTriggerEventArgs> Triggered;

//        public enum AggregateBehavior
//        {
//            Consensus, Cascade
//        }

//        public AggregateTrigger(int overlapTime, IEnumerable<IAudioTrigger> subTriggers, AggregateBehavior behavior = AggregateBehavior.Consensus)
//        {
//            _behavior = behavior;
//            _triggers = new List<IAudioTrigger>(subTriggers);
//            _overlapTime = overlapTime;
//            _triggerTimes = new Dictionary<IAudioTrigger, long>();
//        }

//        ~AggregateTrigger()
//        {
//            Dispose(false);
//        }

//        public void Reset()
//        {
//            foreach (IAudioTrigger trigger in _triggers)
//            {
//                trigger.Reset();
//            }
//        }

//        private AudioTriggerResult TryCascade(AudioChunk audio, IRealTimeProvider realTime)
//        {
//            //int count = 1;

//            AudioTriggerResult returnVal = new AudioTriggerResult()
//            {
//                Triggered = true
//            };

//            foreach (IAudioTrigger trigger in _triggers)
//            {
//                // Try each subtrigger
//                if (returnVal.Triggered)
//                {
//                    // Triggered will be set if the model fired on this tick, or if it fired on a previous
//                    // tick and we're still carrying it forward
//                    returnVal = trigger.SendAudio(audio, realTime);

//                    if (returnVal.Triggered)
//                    {
//                        // Update the trigger time
//                        if (_triggerTimes.ContainsKey(trigger))
//                        {
//                            _triggerTimes.Remove(trigger);
//                        }

//                        _triggerTimes.Add(trigger, currentMillisecond);
//                    }
//                    else
//                    {
//                        returnVal.Triggered = _triggerTimes.ContainsKey(trigger) && (currentMillisecond - _triggerTimes[trigger]) <= _overlapTime;
//                    }

//                    if (returnVal.Triggered)
//                    {
//                        //Console.Write(count++ + " ");
//                    }
//                }
//                else
//                {
//                    // This case happens when the previous (lower-complexity) triggers did not signal a higher-complexity trigger to run.
//                    // In this case, we still need to do a NoOp so that the buffer still gets filled, otherwise we'd drop audio
//                    trigger.SendAudio(audio, realTime, false);
//                }
//            }

//            //Console.WriteLine();

//            if (returnVal.Triggered)
//            {
//                _triggerTimes.Clear();
//                Triggered(this, new AudioTriggerEventArgs(returnVal, realTime));
//            }

//            return returnVal;
//        }

//        private AudioTriggerResult TryConsensus(AudioChunk audio, IRealTimeProvider realTime)
//        {
//            AudioTriggerResult returnVal = new AudioTriggerResult()
//            {
//                Triggered = true
//            };

//            int count = 1;
//            foreach (IAudioTrigger trigger in _triggers)
//            {
//                // Try each subtrigger
//                if (trigger.SendAudio(audio, realTime).Triggered)
//                {
//                    if (_triggerTimes.ContainsKey(trigger))
//                    {
//                        _triggerTimes.Remove(trigger);
//                    }

//                    _triggerTimes.Add(trigger, currentMillisecond);
//                }

//                // And then evaluate if it has triggered within the rendevous window
//                if (!_triggerTimes.ContainsKey(trigger) || (currentMillisecond - _triggerTimes[trigger]) > _overlapTime)
//                {
//                    returnVal.Triggered = false;
//                    //Console.Write("  ");
//                }
//                else
//                {
//                    //Console.Write(count + " ");
//                }
//                count++;
//            }
//            //Console.WriteLine();

//            if (returnVal.Triggered)
//            {
//                _triggerTimes.Clear();
//                Triggered(this, new AudioTriggerEventArgs(returnVal, realTime));
//            }

//            return returnVal;
//        }

//        public void Measure() { }

//        public void Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!AtomicOperations.ExecuteOnce(ref _disposed))
//            {
//                return;
//            }

//            if (disposing)
//            {
//                foreach (IAudioTrigger trigger in _triggers)
//                {
//                    trigger.Dispose();
//                }
//            }
//        }

//        public void Configure(KeywordSpottingConfiguration config)
//        {
//            throw new NotImplementedException();
//        }

//        public AudioTriggerResult SendAudio(AudioChunk audio, IRealTimeProvider realTime, bool noOp = false)
//        {
//            if (noOp)
//            {
//                foreach (IAudioTrigger trigger in _triggers)
//                {
//                    trigger.SendAudio(audio, realTime, false);
//                }

//                return new AudioTriggerResult()
//                {
//                    Triggered = false,
//                    TriggeredKeyword = string.Empty,
//                    WasPrimaryKeyword = false
//                };
//            }
//            else
//            {
//                currentMillisecond += (long)audio.Length.TotalMilliseconds;

//                if (_behavior == AggregateBehavior.Cascade)
//                {
//                    return TryCascade(audio, realTime);
//                }
//                else
//                {
//                    return TryConsensus(audio, realTime);
//                }
//            }
//        }
//    }
//}
