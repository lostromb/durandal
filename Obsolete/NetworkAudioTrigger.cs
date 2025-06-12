using Durandal.Common.Audio;
using Stromberg.Logger;
using Stromberg.Net;
using Stromberg.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class NetworkAudioTrigger :IAudioTrigger
    {
        private const int KEEP_ALIVE_TIME = 30000;
        private const int QUERY_INTERVAL = 100;
        private const double VOLUME_SENSITIVITY = 1.8;
        private const int MAX_COUNTER = 30;
        private const int COUNTER_INCREMENT = 5;
        
        private readonly ILogger _logger;
        private readonly string _clientName;
        private readonly string _remoteHost;
        private readonly int _remotePort;

        private MovingAverage _averageVolume = new MovingAverage(100, 0);
        private BasicBuffer<AudioChunk> _audioBacklog = new BasicBuffer<AudioChunk>(10);
        private int _counter = 0;
        private int _keepAliveCounter = 0;
        private MovingAverage _dataRate = new MovingAverage(300, 0);
        private MovingAverage _errorRate = new MovingAverage(300, 0);
        
        public NetworkAudioTrigger(ILogger logger, string clientName, string remoteHost, int remotePort = 62290)
        {
            _logger = logger;
            _clientName = clientName;
            _remoteHost = remoteHost;
            _remotePort = remotePort;

            // Send the init signal now
            HttpSocketClient client = new HttpSocketClient(_remoteHost, _remotePort, _logger);
            _logger.Log("Initializing network audio trigger");
            HttpRequest a = new HttpRequest();
            a.RequestFile = "/?c=" + _clientName;
            a.RequestMethod = "POST";
            try
            {
                NetworkResponseInstrumented<HttpResponse> initResponse = client.SendRequest(a);
                if (initResponse == null || initResponse.Response == null || initResponse.Response.ResponseCode != 200)
                {
                    _logger.Log("Remote trigger service returned in invalid result", LogLevel.Err);
                }
            }
            catch (SocketException)
            {
                _logger.Log("Socket exception while connecting to remote trigger service!", LogLevel.Err);
            }
        }
        
        public TimeSpan GetExpectedSliceSize()
        {
            return TimeSpan.FromMilliseconds(QUERY_INTERVAL);
        }

        public void NoOp(AudioChunk audio)
        {
            throw new NotImplementedException("Todo: implement noop on network audio trigger");
        }

        public bool Try(AudioChunk audio, bool isBatchRequest)
        {
            return Try(audio);
        }

        public void Reset()
        {
            // todo implement
        }

        public bool Try(AudioChunk audioData)
        {
            HttpSocketClient client = new HttpSocketClient(_remoteHost, _remotePort, _logger);

            bool triggered = false;

            double curVolume = audioData.Volume();
            _averageVolume.Add(curVolume);
            if (curVolume > _averageVolume.Average * VOLUME_SENSITIVITY)
            {
                _counter += COUNTER_INCREMENT;
            }

            if (_counter > MAX_COUNTER)
            {
                _counter = MAX_COUNTER;
            }

            if (_counter > 0)
            {
                AudioChunk finalAudio = new AudioChunk(new short[0], audioData.SampleRate);
                while (_audioBacklog.Available() > 0)
                {
                    finalAudio = finalAudio.Concatenate(_audioBacklog.Read());
                }
                finalAudio = finalAudio.Concatenate(audioData);
                
                HttpRequest thisRequest = new HttpRequest();
                thisRequest.RequestFile = "/?c=" + _clientName;
                thisRequest.RequestMethod = "POST";
                thisRequest.PayloadData = finalAudio.GetDataAsBytes();
                try
                {
                    NetworkResponseInstrumented<HttpResponse> response = client.SendRequest(thisRequest);
                    if (response.Response != null && response.Response.ResponseCode == 200)
                    {
                        if (response != null &&
                            response.Response.ResponseHeaders.ContainsKey("Triggered") &&
                            response.Response.ResponseHeaders["Triggered"].Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            triggered = true;
                        }
                        _errorRate.Add(0);
                        _dataRate.Add(thisRequest.PayloadData.Length);
                    }
                    else
                    {
                        _logger.Log("Trigger service timed out", LogLevel.Wrn);
                        _errorRate.Add(100);
                    }

                }
                catch (SocketException)
                {
                    //_logger.Log("Socket exception from trigger service", LogLevel.Err);
                    _errorRate.Add(100);
                }
                
                _counter--;
            }
            else
            {
                _audioBacklog.Write(audioData);
                _dataRate.Add(0);
            }

            _keepAliveCounter += QUERY_INTERVAL;
            if (_keepAliveCounter > KEEP_ALIVE_TIME)
            {
                // Send a keepalive query every 30 seconds
                _keepAliveCounter = 0;
                double dataRateKbps = _dataRate.Average / 1024 * 1000 / QUERY_INTERVAL;
                _logger.Log(string.Format("Sending keepalive: Data rate {0:F4} KB/s Error rate {1:F4}%", dataRateKbps, _errorRate.Average), LogLevel.Vrb);
                HttpRequest thisRequest = new HttpRequest();
                thisRequest.RequestFile = "/?c=" + _clientName;
                thisRequest.RequestMethod = "POST";
                client.SendRequest(thisRequest);
            }

            return triggered;
        }

        public void Measure() { }

        public void Dispose() { }
    }
}
