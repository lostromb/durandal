using Durandal.Common.Packages;
using Stromberg.Config;
using Stromberg.Logger;
using Stromberg.Net;
using Stromberg.Utils.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GuiPackager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _dialogPath;
        private string _luPath;
        private string _pluginFileName;
        private string _targetServer;
        private ILogger _logger = new ConsoleLogger();
        private Configuration _config;
        
        public MainWindow()
        {
            _config = new IniFileConfiguration(
                _logger,
                new ResourceName("directories"),
                new FileResourceManager(_logger),
                false);
            _dialogPath = _config.GetString("dePath", "C:\\");
            _luPath = _config.GetString("luPath", "C:\\");
            _pluginFileName = _config.GetString("lastPlugin", "C:\\");
            _targetServer = _config.GetString("targetServer", "durandalprod.cloudapp.net");

            InitializeComponent();

            DialogDirField.Text = _dialogPath;
            LuDirField.Text = _luPath;
            PluginFileField.Text = _pluginFileName;
            DeployServerField.Text = _targetServer;
        }

        private void SelectDialogDirButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowDialog();

            if (!string.IsNullOrEmpty(dialog.SelectedPath))
            {
                _dialogPath = dialog.SelectedPath;
                DialogDirField.Text = _dialogPath;
            }
        }

        private void SelectLuDirButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowDialog();

            if (!string.IsNullOrEmpty(dialog.SelectedPath))
            {
                _luPath = dialog.SelectedPath;
                LuDirField.Text = _luPath;
            }
        }

        private void SelectPluginButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Dialog Plugin Files|*.dll";
            dialog.ShowDialog();
            if (!string.IsNullOrEmpty(dialog.FileName))
            {
                _pluginFileName = dialog.FileName;
                PluginFileField.Text = _pluginFileName;
            }
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            _dialogPath = DialogDirField.Text;
            _luPath = LuDirField.Text;
            _pluginFileName = PluginFileField.Text;
            
            //Validate config
            if (!Directory.Exists(_dialogPath))
            {
                System.Windows.Forms.MessageBox.Show("The path \"" + _dialogPath + "\" does not exist!");
                return;
            }
            if (!Directory.Exists(_luPath))
            {
                System.Windows.Forms.MessageBox.Show("The path \"" + _luPath + "\" does not exist!");
                return;
            }
            if (!File.Exists(_pluginFileName))
            {
                System.Windows.Forms.MessageBox.Show("The plugin file \"" + _pluginFileName + "\" does not exist!");
                return;
            }
            
            _config.Set("dePath", _dialogPath);
            _config.Set("luPath", _luPath);
            _config.Set("lastPlugin", _pluginFileName);
            _config.Set("targetServer", _targetServer);

            FileInfo generatedPackage = BuildPackage(_luPath, _dialogPath, _pluginFileName);
            SendPackage(generatedPackage, _targetServer);
        }

        private FileInfo BuildPackage(string luPath, string dialogPath, string inputPluginFile)
        {
            ILogger testLogger = new ConsoleLogger();

            DirectoryInfo luDir = new DirectoryInfo(luPath);
            DirectoryInfo deDir = new DirectoryInfo(dialogPath);
            FileInfo pluginFile = new FileInfo(inputPluginFile);

            ManifestFactory manifestBuilder = new ManifestFactory(testLogger, deDir, luDir);

            IList<FileInfo> pluginFiles = new List<FileInfo>();
            pluginFiles.Add(pluginFile);

            PackageManifest manifest = manifestBuilder.BuildManifest(pluginFiles);

            string packageFileName = ConvertPluginFileToPackageFileName(pluginFile);

            DirectoryInfo packageDir = new DirectoryInfo("packages");
            if (!packageDir.Exists)
            {
                packageDir.Create();
            }
            FileInfo testPackageFile = new FileInfo(packageDir.FullName + "\\" + packageFileName);

            PackageFactory factory = new PackageFactory(testLogger, deDir, luDir);
            factory.BuildPackage(manifest, testPackageFile);

            return testPackageFile;
        }

        private static string ConvertPluginFileToPackageFileName(FileInfo pluginFile)
        {
            string fileName = pluginFile.Name;
            if (fileName.Contains(pluginFile.Extension))
            {
                fileName = fileName.Substring(0, fileName.IndexOf(pluginFile.Extension));
            }
            fileName += ".pkg";
            return fileName;
        }

        private void SendPackage(FileInfo packageFile, string targetServer)
        {
            HttpSocketClient client = new HttpSocketClient(targetServer, 62294, new ConsoleLogger());
            HttpRequest request = new HttpRequest();
            request.RequestMethod = "POST";
            request.RequestFile = "/install";
            request.PayloadData = File.ReadAllBytes(packageFile.FullName);
            NetworkResponseInstrumented<HttpResponse> response = client.SendRequest(request);
            if (response.Response.ResponseCode == 200)
            {
                System.Windows.Forms.MessageBox.Show("The package was installed successfully!");
            }
            else if (response.Response.ResponseCode == 404)
            {
                System.Windows.Forms.MessageBox.Show("The target server was not found or did not respond");
            }
            else if (response.Response.ResponseCode == 500)
            {
                System.Windows.Forms.MessageBox.Show("The target server reported an error. The package was not installed");
            }
        }
    }
}
