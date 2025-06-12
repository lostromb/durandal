namespace MediaControl.Winamp
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    using Microsoft.Win32;

    using Durandal.Common.Logger;
    using System.Threading.Tasks;
    using Durandal.MediaProtocol;
    using System.Collections.Generic;

    /// <summary>
    /// Class for controlling Winamp
    /// </summary>
    public class WinampInstance
    {
        private readonly ILogger _logger;
        private IntPtr _windowHandle = IntPtr.Zero;
        private Process _winampProcess = null;
        private bool _exited;

        /// <summary>
        /// Attempts to retrieve a singleton handle to a currently running Winamp instance.
        /// If no handle is found, this method returns NULL.
        /// </summary>
        /// <returns>A handle to Winamp, or null</returns>
        public static async Task<WinampInstance> Get(bool startIfNecessary, ILogger logger)
        {
            WinampInstance returnVal = null;
            try
            {
                returnVal = new WinampInstance(startIfNecessary, logger);
                await returnVal.Initialize(startIfNecessary);
            }
            catch (ApplicationException e)
            {
                logger.Log("ApplicationException: " + e.Message, LogLevel.Err);
                returnVal = null;
            }
            catch (NullReferenceException e)
            {
                logger.Log("NullReferenceException: " + e.Message, LogLevel.Err);
                returnVal = null;
            }

            return returnVal;
        }

        /// <summary>
        /// Creates a Winamp object and binds it to a running instance of winamp. 
        /// Throws an InvalidOperationException if no winamp instance is running.
        /// </summary>
        private WinampInstance(bool startIfNecessary, ILogger logger)
        {
            _logger = logger;
        }

        public async Task Initialize(bool startIfNecessary)
        {
            if (!IsWinampRunning() && startIfNecessary)
            {
                bool successfulLaunch = await LaunchNewInstance();
                if (!successfulLaunch)
                {
                    throw new ApplicationException("Could not start winamp");
                }
            }

            bool successfulAttach = AttachToRunningWinampProc();
            if (!successfulAttach)
            {
                throw new ApplicationException("Could not attach to running Winamp instance");
            }
        }

        private bool IsWinampRunning()
        {
            return (IsWinampRunning(Win32Helpers.FindWindow("Winamp v1.x", null)));
        }

        private bool IsWinampRunning(IntPtr hWnd)
        {
            return (hWnd.ToInt32() != 0);
        }

        private async Task<bool> LaunchNewInstance()
        {
            string regKey = ((string)Registry.GetValue("HKEY_CLASSES_ROOT\\Winamp.File\\Shell\\open\\command", null, null));
            if (regKey == null || !regKey.Contains("exe\""))
            {
                return false;
            }

            string winampPath = regKey.Split('\"')[1];
            Process.Start(winampPath);
            int startupTime = 4000;
            do
            {
                // Wait until the program responds
                await Task.Delay(100);
                startupTime -= 100;
            } while (!IsWinampRunning() && startupTime > 0);

            if (startupTime <= 0) // Timeout occurred; return false
            {
                return false;
            }

            // Let it load for a bit
            await Task.Delay(1000);

            return true;
        }

        private bool AttachToRunningWinampProc()
        {
            IntPtr hWnd = Win32Helpers.FindWindow("Winamp v1.x", null);
            if (!IsWinampRunning(hWnd))
            {
                return false;
            }
            
            _winampProcess = Process.GetProcessesByName("Winamp")[0];
            _winampProcess.EnableRaisingEvents = true;
            _winampProcess.Exited += WinampProcess_Exited;

            _windowHandle = hWnd;
            _exited = false;
            return true;
        }

        private void WinampProcess_Exited(object sender, EventArgs e)
        {
            _windowHandle = IntPtr.Zero;
            _winampProcess = null;
            _exited = true;
        }

        #region Low-level commands

        private int SendCommandToWinamp(IntPtr hWnd, WM_COMMAND_MSGS Command, uint lParam)
        {
            if (_windowHandle == IntPtr.Zero || _exited)
            {
                throw new ApplicationException("Winamp has closed");
            }

            return Win32Helpers.SendMessage(hWnd, Win32Helpers.WM_COMMAND, (int)Command, lParam);
        }

        private static int ToInt32(bool b)
        {
            return b ? 1 : 0;
        }

        private static bool ToBool(int i)
        {
            return i == 1;
        }

        private int SendToWinamp(int nData, WM_USER_MSGS msg)
        {
            if (_windowHandle == IntPtr.Zero || _exited)
            {
                throw new ApplicationException("Winamp has closed");
            }

            return SendToWinamp(WA_MsgTypes.WM_USER, nData, (int)msg);
        }

        private int SendToWinamp(int nData, WM_COMMAND_MSGS msg)
        {
            if (_windowHandle == IntPtr.Zero || _exited)
            {
                throw new ApplicationException("Winamp has closed");
            }

            return SendToWinamp(WA_MsgTypes.WM_COMMAND, (int)msg, 0);
        }

        private int SendToWinamp(ref Win32Helpers.COPYDATASTRUCT data)
        {
            if (_windowHandle == IntPtr.Zero || _exited)
            {
                throw new ApplicationException("Winamp has closed");
            }

            //not sure about this one, i think the only message you can send for copydata is 0
            return Win32Helpers.SendMessageA(_windowHandle, Win32Helpers.WM_COPYDATA, 0, ref data);
        }

        private int SendToWinamp(WA_MsgTypes msgType, int nData, int nMsg)
        {
            if (_windowHandle == IntPtr.Zero || _exited)
            {
                throw new ApplicationException("Winamp has closed");
            }

            return Win32Helpers.SendMessage(_windowHandle, (int)msgType, nData, (uint)nMsg);
        }
        
        /*private int SendToWinamp(int nData, WA_IPC msgType)
        {
            return Win32.SendMessage(Handle,WM_WA_IPC,(parameter),IPC_*);
            return Win32.SendMessage(Handle, (int)msgType, nData, (uint)nMsg);
        }*/

        #endregion

        #region High-level commands

        public void AppendToPlaylist(string fileName)
        {
            Win32Helpers.COPYDATASTRUCT cds;
            cds.dwData = (IntPtr)WA_IPC.IPC_ENQUEUEFILE;
            cds.lpData = fileName;
            cds.cbData = fileName.Length + 1;
            SendToWinamp(ref cds);
        }

        public void AppendToPlaylist(IEnumerable<string> fileNames)
        {
            foreach (string sFile in fileNames)
            {
                AppendToPlaylist(sFile);
            }
        }

        public void AppendToPlaylist(FileInfo fileName)
        {
            AppendToPlaylist(fileName.FullName);
        }

        public void AppendToPlaylist(IEnumerable<FileInfo> fileNames)
        {
            foreach (FileInfo fileName in fileNames)
            {
                AppendToPlaylist(fileName);
            }
        }

        public void ClearPlaylist()
        {
            SendToWinamp(0, WM_USER_MSGS.WA_CLEAR_PLAYLIST);
        }

        public void Play()
        {
            SendToWinamp(0, WM_COMMAND_MSGS.WINAMP_BUTTON2);
        }

        public bool Playing
        {
            get
            {
                int returnVal = SendToWinamp(0, WM_USER_MSGS.WA_GET_PLAYSTATUS);
                return returnVal == 1;
            }
        }

        public void Pause()
        {
            SendToWinamp(0, WM_COMMAND_MSGS.WINAMP_BUTTON3);
        }

        public bool Paused
        {
            get
            {
                int returnVal = SendToWinamp(0, WM_USER_MSGS.WA_GET_PLAYSTATUS);
                return returnVal == 3;
            }
        }

        public void PlayPause()
        {
            if (!Playing)
                Play();
            else
                Pause();
        }

        public void Stop()
        {
            SendToWinamp(0, WM_COMMAND_MSGS.WINAMP_BUTTON4);
        }

        public bool Stopped
        {
            get
            {
                int returnVal = SendToWinamp(0, WM_USER_MSGS.WA_GET_PLAYSTATUS);
                return returnVal == 0;
            }
        }

        public void Next()
        {
            SendToWinamp(0, WM_COMMAND_MSGS.WINAMP_BUTTON5);
        }

        public void Previous()
        {
            SendToWinamp(0, WM_COMMAND_MSGS.WINAMP_BUTTON1);
        }

        public int CurrentPlaybackMs
        {
            get
            {
                return SendToWinamp(0, WM_USER_MSGS.WA_GET_PLAYBACK_INFO);
            }
        }

        public bool Shuffle
        {
            get
            {
                int returnVal = SendToWinamp(0, WM_USER_MSGS.WA_GET_SHUFFLE);
                return ToBool(returnVal);
            }
            set
            {
                int status = ToInt32(value);
                SendToWinamp(status, WM_USER_MSGS.WA_SET_SHUFFLE);
            }
        }

        public RepeatMode Repeat
        {
            get
            {
                int returnVal = SendToWinamp(0, WM_USER_MSGS.WA_GET_REPEAT);
                if (returnVal == 0)
                {
                    return RepeatMode.NoRepeat;
                }
                else
                {
                    return RepeatMode.RepeatPlaylist;
                }
            }
            set
            {
                int status = value == RepeatMode.NoRepeat ? 0 : 1;
                SendToWinamp(status, WM_USER_MSGS.WA_SET_REPEAT);
            }
        }

        /// <summary>
        /// Player volume, between 0 and 1
        /// </summary>
        public float Volume
        {
            get
            {
                int rawVal = SendToWinamp(-666, WM_USER_MSGS.WA_SET_VOL);
                return (float)rawVal / 255;
            }
            set
            {
                int vol = (int)(value * 255);
                if (vol < 0)
                {
                    vol = 0;
                }
                else if (vol > 255)
                {
                    vol = 255;
                }
                SendToWinamp(vol, WM_USER_MSGS.WA_SET_VOL);
            }
        }

        #endregion
    }
}
