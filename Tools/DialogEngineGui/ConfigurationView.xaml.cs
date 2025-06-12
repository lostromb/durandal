using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Durandal
{
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Config;

    /// <summary>
    /// Interaction logic for ConfigurationView.xaml
    /// </summary>
    public partial class ConfigurationView : Page
    {
        private ThreadedDialogWebService _core;
        private DialogWebConfiguration _coreConfig;

        public ConfigurationView()
        {
            InitializeComponent();

            _core = ((App)App.Current).GetDialogEngine();
            _coreConfig = _core.GetConfiguration();

            foreach (var configValue in _coreConfig.GetBase().Value.GetAllValues())
            {
                var newControl = ConfigurationControlFactory.CreateControl(configValue.Value);
                if (newControl != null)
                {
                    this.mainPanel.Children.Add(newControl);
                }
            }
        }
    }
}
