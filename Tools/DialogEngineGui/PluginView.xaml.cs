﻿using Durandal.Common.Dialog.Web;
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
    /// <summary>
    /// Interaction logic for PluginView.xaml
    /// </summary>
    public partial class PluginView : Page
    {
        private ThreadedDialogWebService _core;

        public PluginView()
        {
            InitializeComponent();

            _core = ((App)App.Current).GetDialogEngine();
        }
    }
}
