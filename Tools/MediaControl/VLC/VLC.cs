namespace Durandal.Answers.VLCAnswer
{
    using System;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    using Microsoft.Win32;

    using Durandal.Common.Logger;

    public class VLC : IDisposable
    {
        private Socket rcSocket;
        private Thread vlcMonitorThread;
        private volatile bool acceptingCommands;
        // private volatile int playbackState;
        private readonly Encoding STREAM_ENCODING = Encoding.ASCII;

        public static VLC TryConnectOrLaunchNew(string vlcPath, int portNum, ILogger logger)
        {
            if (IsVLCRunning)
                return TryConnectToExisting(portNum, logger);
            else
                return TryLaunchNewInstance(vlcPath, portNum, logger);
        }

        public static VLC TryConnectToExisting(int portNum, ILogger logger)
        {
            VLC returnVal = new VLC();
            if (returnVal.Connect("localhost", portNum, 1000, logger))
                return returnVal;
            return null;
        }

        public static VLC TryLaunchNewInstance(string vlcPath, int portNum, ILogger logger)
        {
            if (string.IsNullOrEmpty(vlcPath))
            {
                var vlcKey = Registry.LocalMachine.OpenSubKey(@"Software\VideoLan\VLC");
                if (vlcKey == null)
                {
                    vlcKey = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\VideoLan\VLC");
                }
                if (vlcKey != null)
                {
                    vlcPath = vlcKey.GetValue(null) as string;
                }

                if (string.IsNullOrEmpty(vlcPath))
                {
                    throw new ApplicationException("Can not find the VLC executable!");
                }
            }

            string args = "";
            logger.Log("Launching new VLC instance...");
            Process t = Process.Start(vlcPath, args);
            Thread.Sleep(1000); // TODO: Find a way to wait for the UI to come up and stuff
            // Now try and get a socket connection to the new instance
            return TryConnectToExisting(portNum, logger);
        }

        private bool Connect(string hostname, int portNum, int retryTimeout, ILogger logger)
        {
            if (this.rcSocket != null && this.rcSocket.Connected)
            {
                logger.Log("Already connected!");
                return true;
            }
            this.rcSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    this.rcSocket.Connect(hostname, portNum);
                    this.vlcMonitorThread = new Thread(this.MonitorRemoteVLC);
                    this.vlcMonitorThread.IsBackground = true;
                    this.vlcMonitorThread.Name = "VLC media player monitor";
                    this.vlcMonitorThread.Start();
                    logger.Log("Connected to VLC");
                    return true;
                }
                catch (SocketException e)
                {
                    logger.Log("Socket exception or timeout: " + e.Message, LogLevel.Err);
                    logger.Log("Destination host: " + hostname + ":" + portNum, LogLevel.Err);
                    Thread.Sleep(retryTimeout);
                }
                catch (Exception e)
                {
                    logger.Log("Unexpected error: " + e.Message, LogLevel.Err);
                    return false;
                }
            }
            return false;
        }

        public void Disconnect()
        {
            if (this.vlcMonitorThread != null && this.vlcMonitorThread.IsAlive)
                this.vlcMonitorThread.Abort();
            if (this.rcSocket != null && this.rcSocket.Connected)
            {
                this.rcSocket.Close();
                this.rcSocket = null;
            }
        }

        private void MonitorRemoteVLC()
        {
            Decoder decoder = this.STREAM_ENCODING.GetDecoder();
            StringBuilder currentInput = new StringBuilder();
            byte[] recvBuffer = new byte[1024];
            char[] charBuffer = new char[1024];
            int bytesUsed;
            int charsUsed;
            bool completed;
            int leftoverBytes = 0;
            this.acceptingCommands = true;
            do
            {
                // Listen for messages from the remote socket
                int readSize = 0;
                while (readSize == 0)
                {
                    Thread.Sleep(10);
                    // Read bytes from the socket
                    readSize = this.rcSocket.Receive(recvBuffer, leftoverBytes, 1024 - leftoverBytes, SocketFlags.None);
                }

                // Convert bytes into chars using the decoder (technically overkill since we're using ascii)
                decoder.Convert(recvBuffer, 0, readSize, charBuffer, 0, 1024, false, out bytesUsed, out charsUsed, out completed);
                // Save the leftover bytes
                leftoverBytes = readSize - bytesUsed;
                for (int c = bytesUsed; c < readSize; c++)
                    recvBuffer[c - bytesUsed] = recvBuffer[c];
                if (charsUsed > 0)
                {
                    char[] chunk = new char[charsUsed];
                    Array.Copy(charBuffer, chunk, charsUsed);
                    currentInput.Append(chunk);
                }
                string all = currentInput.ToString();
                if (all.Contains("\r\n"))
                {
                    string nextLine = all.Substring(0, all.IndexOf("\r"));
                    string theRest = all.Substring(all.IndexOf("\n") + 1);
                    currentInput.Clear();
                    currentInput.Append(theRest);
                    this.ParseVLCResponse(nextLine);
                    this.acceptingCommands = true;
                }
            }
            while (this.rcSocket.Connected);
        }

        private void ParseVLCResponse(string response)
        {
            // TODO: Implement this
        }

        private static bool IsVLCRunning
        {
            get
            {
                // Checks to see if there is a running process named vlc.exe on the computer
                return Process.GetProcessesByName("vlc").Length > 0;
            }
        }

        private void SendCommand(string command)
        {
            while (!this.acceptingCommands)
                Thread.Sleep(10);
            byte[] data = this.STREAM_ENCODING.GetBytes(command + "\r\n");
            this.acceptingCommands = false;
            this.rcSocket.Send(data);
            Thread.Sleep(1000); // Pause a bit, just for safety
        }

        /*public void Play()
        {
            SendCommand("play");
        }*/

        public void TogglePause()
        {
            this.SendCommand("pause");
        }

        public void SetFullscreen(bool value)
        {
            if (value)
                this.SendCommand("f on");
            else
                this.SendCommand("f off");
        }

        public void OpenAndPlayFile(string fileName)
        {
            this.SendCommand("clear");
            this.SendCommand("add " + fileName);
        }

        // Hooks into IDisposable to make sure threads/connections are safely closed
        // even in the event that DialogEngine kills us
        public void Dispose()
        {
            this.Disconnect();
        }
    }
}
