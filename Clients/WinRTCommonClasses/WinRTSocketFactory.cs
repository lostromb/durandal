using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;
using System.Diagnostics;
using Durandal.Common.Time;

namespace DurandalWinRT
{
    public class WinRTSocketFactory : ISocketFactory
    {
        private const int CONNECT_TIMEOUT = 3000;
        private ILogger _logger;
        private SocketProtectionLevel _defaultSslLevel;
        private SocketQualityOfService _defaultQos;
        private bool _syncReadHack = false;

        public WinRTSocketFactory(ILogger logger, SocketProtectionLevel defaultSslLevel = SocketProtectionLevel.Tls12, SocketQualityOfService qos = SocketQualityOfService.Normal, bool syncReadHack = false)
        {
            _logger = logger;
            _defaultSslLevel = defaultSslLevel;
            _defaultQos = qos;
            _syncReadHack = syncReadHack;
        }

        public Task<ISocket> Connect(string hostname, int port, bool secure, ILogger traceLogger = null, IRealTimeProvider realTime = null)
        {
            TcpConnectionConfiguration config = new TcpConnectionConfiguration(hostname, port, secure);
            return Connect(config, traceLogger, realTime);
        }

        public async Task<ISocket> Connect(TcpConnectionConfiguration connectionConfig, ILogger traceLogger = null, IRealTimeProvider realTime = null)
        {
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                _logger.Log("Starting to open socket connection");
                Windows.Networking.Sockets.StreamSocket socket = new Windows.Networking.Sockets.StreamSocket();
                socket.Control.NoDelay = connectionConfig.NoDelay.GetValueOrDefault(true); // Disable nagling
                socket.Control.QualityOfService = _defaultQos;

                if (connectionConfig.UseSSL)
                {
                    string sslHostname = string.IsNullOrEmpty(connectionConfig.SslHostname) ? connectionConfig.DnsHostname : connectionConfig.SslHostname;
                    Task connectTask = socket.ConnectAsync(new HostName(sslHostname), connectionConfig.Port.ToString(), _defaultSslLevel).AsTask();
                    /*if (!(await connectTask.Timeout(CONNECT_TIMEOUT).ConfigureAwait(false)))
                    {
                        _logger.Log("Timed out opening socket connection", LogLevel.Err);
                        return null;
                    }*/
                    await connectTask;
                }
                else
                {
                    Task connectTask = socket.ConnectAsync(new HostName(connectionConfig.DnsHostname), connectionConfig.Port.ToString(), SocketProtectionLevel.PlainSocket).AsTask();
                    /*if (!(await connectTask.Timeout(CONNECT_TIMEOUT).ConfigureAwait(false)))
                    {
                        _logger.Log("Timed out opening socket connection", LogLevel.Err);
                        return null;
                    }*/
                    await connectTask;
                }

                timer.Stop();
                _logger.Log("Finished socket connect in " + timer.ElapsedMilliseconds + "ms");
                return new WinRTSocket(socket, _logger, _syncReadHack);
            }
            catch (Exception e)
            {
                _logger.Log("Exception while opening socket connection: " + e.Message, LogLevel.Err);
                return null;
            }
        }

        public void Dispose() { }
    }
}
