using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Webkit;
using static Android.Views.View;
using System.Diagnostics;
using Durandal.Common.Client;
using Durandal.Common.Config;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Dialog.Web;
using Durandal.AndroidClient.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Extensions.BassAudio;
using Durandal.Common.Audio.Hardware;
using Java.IO;
using System.IO;
using Durandal.Common.Audio.Codecs;

namespace Durandal.AndroidClient
{
    /// <summary>
    /// Activity which manages the main GUI of the client, mainly accepting text + microphone queries and showing the results.
    /// </summary>
    [Activity(Label = "Durandal", Icon = "@drawable/icon", Theme = "@android:style/Theme.NoTitleBar")]
    public class ClientActivity : Activity
    {
        private WebView canvas;
        private EditText _inputTextBox;
        private TextView _outputTextBox;
        private Button _submitButton;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);
            
            _inputTextBox = FindViewById<EditText>(Resource.Id.inputTextBox);
            _submitButton = FindViewById<Button>(Resource.Id.submitButton);
            _outputTextBox = FindViewById<TextView>(Resource.Id.outputTextBox);

            _submitButton.Click += SubmitButton_Clicked;
            //_inputTextBox.KeyPress += InputTextBox_KeyPressed;

            // canvas = FindViewById<WebView>(Resource.Id.canvas);
            // canvas.Settings.JavaScriptEnabled = true;
            // canvas.AddJavascriptInterface(new JavascriptBridge(), "window");
            // canvas.LoadUrl("http://durandal.dnsalias.net:62292");
        }

        public async void SubmitButton_Clicked(object source, EventArgs args)
        {
            MainActivity.Logger.Log("Button pressed!");
            _outputTextBox.Text = "Playing some audio";

            try
            {
                OggOpusCodecFactory codecFactory = new OggOpusCodecFactory();
                BassDeviceDriver audioDriver = new BassDeviceDriver(MainActivity.Logger.Clone("BassDriver"));
                using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
                using (Stream audioInStream = Assets.Open("Reunion.opus"))
                    using (AudioDecoder decoder = OggOpusCodecFactory)
                using (IAudioRenderDevice audioPlayer = audioDriver.OpenRenderDevice(null, audioGraph, audioFormat, "BassRender"))
                {
                    sineWave.ConnectOutput(audioPlayer);
                    await audioPlayer.StartPlayback(DefaultRealTimeProvider.Singleton).ConfigureAwait(true);
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                    await audioPlayer.StopPlayback().ConfigureAwait(true);
                    _outputTextBox.Text = "Done playing audio";
                }
            }
            catch (Exception e)
            {

                MainActivity.Logger.Log(e);
                _outputTextBox.Text = e.Message;
            }
        }

        public async void InputTextBox_KeyPressed(object source, KeyEventArgs args)
        {
            await DurandalTaskExtensions.NoOpTask;
            MainActivity.Logger.Log(args.KeyCode);
            // _outputTextBox.Text = _inputTextBox.Text;

            //if (args.KeyCode == Keycode.Enter)
            //{
            //    outputTextBox.Text = inputTextBox.Text;
            //}
        }
    }
}