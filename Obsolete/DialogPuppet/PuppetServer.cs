namespace DialogPuppet
{
    using System;
    using System.Collections.Generic;

    using Durandal.Common;

    using Stromberg.Logger;
    using Stromberg.Net;

    using HttpServer = Stromberg.Net.HttpServer;
    using System.IO;
    using Stromberg.Utils.IO;
    using Durandal.Common.Packages;
    using Durandal.Common.Net;
    using Stromberg.Config;
    using System.Text;
    using System.Threading;
    using System.Net.Sockets;
    using System.Diagnostics;

    public class PuppetServer : HttpSocketServer
    {
        private IResourceManager _deResourceManager;
        private DialogHttpClient _dialogClient;
        private Configuration _localConfig;
        private string _dialogPath;
        
        public PuppetServer(int portNum, ILogger logger, Configuration config)
            : base(portNum, logger)
        {
            _localConfig = config;
            _dialogClient = new DialogHttpClient(new HttpSocketClient("localhost", 62292, _logger.Clone("DialogHttpClient")), _logger.Clone("DialogHttpClient"));
            _dialogPath = _localConfig.GetString("dialogEngineDir");
            _deResourceManager = new FileResourceManager(_logger.Clone("DEResourceManager"), _dialogPath);
        }

        private void KillDialog()
        {
            try
            {
                _dialogClient.Shutdown();
            }
            catch (SocketException) { }
            Thread.Sleep(2000);
        }

        private Configuration GetDialogConfig()
        {
            return new IniFileConfiguration(_logger, new ResourceName("DialogEngine_config"), _deResourceManager, true);
        }

        private bool IsDialogAlive()
        {
            return _dialogClient.GetStatus() != null;
        }

        private void RestartDialog()
        {
            ProcessStartInfo info = new ProcessStartInfo()
            {
                WorkingDirectory = _dialogPath,
                FileName = "DialogEngineGui.exe",
                UseShellExecute = true
            };
            Process.Start(info);
        }

        private void SendPackageToLu(FileInfo file)
        {
            try
            {
                byte[] payload = File.ReadAllBytes(file.FullName);
                HttpSocketClient client = new HttpSocketClient("localhost", 62291, _logger);
                client.SetReadTimeout(60000);
                HttpRequest httpRequest = new HttpRequest();
                httpRequest.RequestMethod = "GET";
                httpRequest.RequestFile = "/install";
                httpRequest.ProtocolVersion = "HTTP/1.0";
                httpRequest.PayloadData = payload;
                client.SendRequest(httpRequest);
            }
            catch (Exception e)
            {
                _logger.Log("Uncaught exception in SendPackageToLu: " + e.Message);
            }
        }

        private void InstallPackageOnThread(FileInfo file)
        {
            new Thread(() =>
                {
                    try
                    {
                        PackageFile package = new PackageFile(file, _logger);

                        // Kill Dialog
                        KillDialog();
                        Configuration dialogConfig = GetDialogConfig();

                        // Install on Dialog
                        PackageInstaller.InstallDialog(dialogConfig, package, _dialogPath, _deResourceManager, _logger.Clone("PackageInstaller"));

                        package.Close();

                        // Install on LU
                        SendPackageToLu(file);

                        file.Delete();

                        RestartDialog();
                    }
                    catch (AggregateException packageException)
                    {
                        _logger.Log(packageException.Message, LogLevel.Err);
                    }
                    
                }).Start();
        }

        public override HttpResponse HandleConnection(HttpRequest request)
        {
            try
            {
                HttpResponse response = HttpResponse.BadRequestResponse();
                if (request.RequestFile.Equals("/install") && request.PayloadData.Length > 0)
                {
                    // Read the payload
                    FileInfo fileName = new FileInfo(Guid.NewGuid().ToString("N") + ".pkg");
                    File.WriteAllBytes(fileName.FullName, request.PayloadData);
                    
                    // See if Dialog is alive
                    if (!IsDialogAlive())
                    {
                        response = HttpResponse.ServerErrorResponse();
                        response.PayloadData = Encoding.UTF8.GetBytes("The dialog server did not respond to the upgrade request");
                    }
                    else
                    {
                        InstallPackageOnThread(fileName);
                        response = HttpResponse.OKResponse();
                    }
                }
                return response;
            }
            catch (Exception e)
            {
                _logger.Log("Caught unhandled exception while processing LU request", LogLevel.Err);
                _logger.Log(e.Message, LogLevel.Err);
                _logger.Log(e.StackTrace, LogLevel.Err);
                return HttpResponse.ServerErrorResponse();
            }
        }
    }
}
