using QuickFont.Configuration;
using QuickFont;
using System.IO;
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
using System.Xml.Linq;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Threading;
using OpenTK.Wpf;
using Durandal.Common.MathExt;
using Durandal.Common.Audio.Beamforming;
using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Extensions.NAudio.Devices;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Hardware;
using NAudio.CoreAudioApi;
using Durandal.Extensions.NAudio;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Visualizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly object _mutex = new object();

        private QFont? _font;
        private QFontDrawing? _drawing;
        private QFontRenderOptions? _renderOptions;

        private bool _initializedResources = false;
        private bool _isHoldingRightMouse = false;
        private bool _isHoldingLeftMouse = false;
        private Point _mouseDragStartCanvasCenterPoint = new Point();

        private float _cameraOrbitDistance = 2.0f;
        private float _cameraOrbitHoriz = -0.75f * FastMath.PI;
        private float _cameraOrbitVert = 0.11f * FastMath.PI;

        // used for dragging the view
        private float _cameraOrbitDistanceStart;
        private float _cameraOrbitHorizStart;
        private float _cameraOrbitVertStart;

        // The thing we're actually trying to visualize
        private ILogger _logger;

        private ArrayMicrophoneGeometry _micGeometry;

        private IAudioGraph _audioGraph;
        private AudioDecoder _musicSource;
        private Basic3DProjector _projector;
        private BeamFormer _beamformer;
        private IAudioRenderDevice _speakers;

        private RateCounter _fpsCounter;

        public MainWindow()
        {
            _logger = new DebugLogger();
            _fpsCounter = new RateCounter(TimeSpan.FromSeconds(3));
            _audioGraph = new AudioGraph(AudioGraphCapabilities.Concurrent);
            _musicSource = new OggOpusDecoder(new WeakPointer<IAudioGraph>(_audioGraph), "MusicSource", _logger, TimeSpan.FromMilliseconds(50));
            _musicSource.Initialize(
                new FileStream(@"C:\Code\Durandal\Data\big arm.opus", FileMode.Open, FileAccess.Read),
                ownsStream: true,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton).Await();

            _micGeometry = new ArrayMicrophoneGeometry(new Vector3f[]
                {
                    new Vector3f(-100, 0, 0),
                    new Vector3f(100, 0, 0),
                },
                new Tuple<int, int>(0, 1));

            _projector = new Basic3DProjector(new WeakPointer<IAudioGraph>(_audioGraph), 48000, MultiChannelMapping.Packed_2Ch, _micGeometry, "Projector");
            _projector.SourcePositionMeters = new Vector3f(0.5f, -0.5f, 0);
            _beamformer = new BeamFormer(
                new WeakPointer<IAudioGraph>(_audioGraph),
                _logger.Clone("Beamformer"),
                48000,
                MultiChannelMapping.Packed_2Ch,
                _micGeometry,
                AttentionPattern.SemiCircle(Vector3f.Zero, new Vector3f(0, -1, 0), new Vector3f(0, 0, 1), 1000.0f, 180, 10),
                "BeamFormer");
            _beamformer.FocusPositionMeters = new Vector3f(0.5f, -0.5f, 0);

            IAudioDriver audioDriver = new WasapiDeviceDriver(_logger.Clone("Speakers"));
            _speakers = audioDriver.OpenRenderDevice(null, new WeakPointer<IAudioGraph>(_audioGraph), AudioSampleFormat.Mono(48000), "Speakers");

            AudioConformer inputConformer = new AudioConformer(new WeakPointer<IAudioGraph>(_audioGraph), _musicSource.OutputFormat, _projector.InputFormat, "Conformer", _logger.Clone("Conformer"));
            _musicSource.ConnectOutput(inputConformer);
            inputConformer.ConnectOutput(_projector);
            _projector.ConnectOutput(_beamformer);
            _beamformer.ConnectOutput(_speakers);
            _speakers.StartPlayback(DefaultRealTimeProvider.Singleton).Await();

            InitializeComponent();

            var settings = new GLWpfControlSettings();
            settings.MajorVersion = 2;
            settings.MinorVersion = 0;
            settings.RenderContinuously = true;
            Canvas.Start(settings);
        }

        private void InitializeGlResources()
        {
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Fastest);

            QFontBuilderConfiguration fontBuilderConfig = new QFontBuilderConfiguration(true)
            {
                TextGenerationRenderHint = TextGenerationRenderHint.SizeDependent,
                Characters = CharacterSet.General
            };

            _font = new QFont(".\\Resources\\consola.ttf", 10, fontBuilderConfig);
            _drawing = new QFontDrawing();

            _renderOptions = new QFontRenderOptions()
            {
                UseDefaultBlendFunction = true,
                CharacterSpacing = 0.2f,
                Colour = System.Drawing.Color.White,
                LockToPixel = true,
                //TransformToViewport = new Viewport(0, 0, (float)Canvas.Width, (float)Canvas.Height)
            };
        }

        private void InvalidateCanvas()
        {
            Canvas?.InvalidateVisual();
        }

        private void Canvas_OnRender(TimeSpan delta)
        {
            // FIXME need to load this one time after the GL canvas has started
            if (!_initializedResources)
            {
                _initializedResources = true;
                InitializeGlResources();
            }

            if (Canvas.ActualHeight <= 0 ||
                Canvas.ActualWidth <= 0)
            {
                // Nothing to render
                return;
            }

            Monitor.Enter(_mutex);
            try
            {
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                // Projection
                GL.MatrixMode(MatrixMode.Projection);
                float aspectRatio = (float)(Canvas.ActualWidth / Canvas.ActualHeight);
                //GL.Ortho(0, aspectRatio, 1, 0, -1, 1);
                Matrix4 projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(2, aspectRatio, 0.01f, 10.0f);
                GL.LoadMatrix(ref projectionMatrix);

                // Modelview
                GL.MatrixMode(MatrixMode.Modelview);
                Matrix4 modelviewMatrix = Matrix4.Identity;
                modelviewMatrix *= Matrix4.CreateRotationZ(_cameraOrbitHoriz);
                modelviewMatrix *= Matrix4.CreateRotationX(_cameraOrbitVert);
                modelviewMatrix *= Matrix4.CreateTranslation(new Vector3(0, _cameraOrbitDistance, 0));
                modelviewMatrix *= Matrix4.CreateRotationX(FastMath.PI / -2.0f);
                GL.LoadMatrix(ref modelviewMatrix);

                GL.Disable(EnableCap.DepthTest);
                GL.ShadeModel(ShadingModel.Smooth);
                GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                // Clear font pixel buffer
                _drawing.ProjectionMatrix = Matrix4.CreateOrthographicOffCenter(0, (float)Canvas.ActualWidth, 0, (float)Canvas.ActualHeight, -1, 1);
                _drawing.DrawingPrimitives.Clear();

                _drawing.Print(_font,
                    $"Camera orbit {FastMath.RadsToDegrees(_cameraOrbitHoriz):F1} {FastMath.RadsToDegrees(_cameraOrbitVert):F1}",
                    new Vector3(3f, 20f, 0),
                    QFontAlignment.Left,
                    _renderOptions);

                _drawing.Print(_font,
                    $"FPS {_fpsCounter.Rate:F1}",
                    new Vector3(3f, 40f, 0),
                    QFontAlignment.Left,
                    _renderOptions);

                GL.Disable(EnableCap.Texture2D);
                GL.Disable(EnableCap.Blend);
                GL.UseProgram(0);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.BindTexture(TextureTarget.Texture2D, 0);

                // 1 WORLD UNIT == 1 METER

                // Floor
                GL.Begin(PrimitiveType.Lines);
                {
                    GL.Color4(0.2f, 0.2f, 0.2f, 1.0f);
                    // Grid lines every 10 cm extending 2m to each side
                    for (int iter = -20; iter <= 20; iter++)
                    {
                        float val = (float)iter / 10.0f;

                        GL.Vertex3(val, -2.0f, 0);
                        GL.Vertex3(val, 2.0f, 0);
                        GL.Vertex3(2.0f, val, 0);
                        GL.Vertex3(-2.0f, val, 0);
                    }
                }
                GL.End();

                foreach (Vector3f micElement in _beamformer.MicGeometry.MicrophonePositions)
                {
                    DrawCube(ConvertMicPositionToRenderPosition(micElement), 0.01f, Color4.Yellow);
                }

                foreach (Vector3f attentionMarker in _beamformer.AttentionPattern.Positions)
                {
                    DrawCube(ConvertMicPositionToRenderPosition(attentionMarker), 0.01f, Color4.Blue);
                }

                // Simulated origin
                DrawCube(_projector.SourcePositionMeters, 0.02f, Color4.Green);

                // Beamformer steering vector
                DrawCube(_beamformer.FocusPositionMeters, 0.03f, Color4.Red);

                //GL.Begin(PrimitiveType.Quads);
                //{
                //    GL.Vertex3(-4.0f, -4.0f, -4.0f);
                //    GL.Color4(0.0f, 1.0f, 0.0f, 1.0f);
                //    GL.Vertex3(-4.0f, 4.0f, -4.0f);
                //    GL.Color4(0.0f, 0.0f, 1.0f, 1.0f);
                //    GL.Vertex3(4.0f, 4.0f, -4.0f);
                //    GL.Color4(1.0f, 0.0f, 1.0f, 1.0f);
                //    GL.Vertex3(4.0f, -4.0f, -4.0f);
                //}
                //GL.End();

                // Axis markers
                GL.Begin(PrimitiveType.Lines);
                {
                    // Positive X
                    GL.Color4(1.0f, 0.0f, 0.0f, 1.0f);
                    GL.Vertex3(0, 0, 0);
                    GL.Vertex3(1, 0, 0);
                    // Positive Y
                    GL.Color4(0.0f, 1.0f, 0.0f, 1.0f);
                    GL.Vertex3(0, 0, 0);
                    GL.Vertex3(0, 1, 0);
                    // Positive Z
                    GL.Color4(0.0f, 0.0f, 1.0f, 1.0f);
                    GL.Vertex3(0, 0, 0);
                    GL.Vertex3(0, 0, 1);
                }
                GL.End();

                //Draw font pixel buffer over the top of everything
                GL.Enable(EnableCap.Texture2D);
                GL.Enable(EnableCap.Blend);
                _drawing.RefreshBuffers();
                _drawing.Draw();
                GL.UseProgram(0);
                GL.Disable(EnableCap.Texture2D);
                GL.Disable(EnableCap.Blend);

                GL.Finish();
            }
            finally
            {
                Monitor.Exit(_mutex);
                _fpsCounter.Increment();
            }
        }

        private void DrawCube(Vector3f position, float size, Color4 color)
        {
            GL.Begin(PrimitiveType.Lines);
            {
                GL.Color4(color);
                GL.Vertex3(position.X - size, position.Y - size, position.Z - size);
                GL.Vertex3(position.X + size, position.Y - size, position.Z - size);
                GL.Vertex3(position.X + size, position.Y - size, position.Z - size);
                GL.Vertex3(position.X + size, position.Y + size, position.Z - size);
                GL.Vertex3(position.X + size, position.Y + size, position.Z - size);
                GL.Vertex3(position.X - size, position.Y + size, position.Z - size);
                GL.Vertex3(position.X - size, position.Y + size, position.Z - size);
                GL.Vertex3(position.X - size, position.Y - size, position.Z - size);

                GL.Vertex3(position.X - size, position.Y - size, position.Z + size);
                GL.Vertex3(position.X + size, position.Y - size, position.Z + size);
                GL.Vertex3(position.X + size, position.Y - size, position.Z + size);
                GL.Vertex3(position.X + size, position.Y + size, position.Z + size);
                GL.Vertex3(position.X + size, position.Y + size, position.Z + size);
                GL.Vertex3(position.X - size, position.Y + size, position.Z + size);
                GL.Vertex3(position.X - size, position.Y + size, position.Z + size);
                GL.Vertex3(position.X - size, position.Y - size, position.Z + size);

                GL.Vertex3(position.X - size, position.Y - size, position.Z - size);
                GL.Vertex3(position.X - size, position.Y - size, position.Z + size);
                GL.Vertex3(position.X + size, position.Y - size, position.Z - size);
                GL.Vertex3(position.X + size, position.Y - size, position.Z + size);
                GL.Vertex3(position.X - size, position.Y + size, position.Z - size);
                GL.Vertex3(position.X - size, position.Y + size, position.Z + size);
                GL.Vertex3(position.X + size, position.Y + size, position.Z - size);
                GL.Vertex3(position.X + size, position.Y + size, position.Z + size);
            }
            GL.End();
        }

        private Vector3f ConvertMicPositionToRenderPosition(Vector3f input)
        {
            return new Vector3f(
                input.X * 0.001f,
                input.Y * 0.001f,
                input.Z * 0.001f
                );
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isHoldingLeftMouse)
            {
                return;
            }

            // Start click and dragging around the canvas
            Point clickPosition = e.GetPosition(Canvas);
            _mouseDragStartCanvasCenterPoint = clickPosition;
            _isHoldingRightMouse = true;
            Mouse.PrimaryDevice.Capture(Canvas, CaptureMode.Element);

            _cameraOrbitHorizStart = _cameraOrbitHoriz;
            _cameraOrbitVertStart = _cameraOrbitVert;

            //InvalidateCanvas();
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isHoldingRightMouse)
            {
                return;
            }

            Point clickPosition = e.GetPosition(Canvas);

            // Run hit detection.

            _mouseDragStartCanvasCenterPoint = clickPosition;
            _isHoldingLeftMouse = true;
            Mouse.PrimaryDevice.Capture(Canvas, CaptureMode.Element);

            //InvalidateCanvas();
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isHoldingLeftMouse = false;
            Mouse.PrimaryDevice.Capture(Canvas, CaptureMode.None);
            //InvalidateCanvas();
        }

        private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isHoldingRightMouse = false;
            Mouse.PrimaryDevice.Capture(Canvas, CaptureMode.None);
            //InvalidateCanvas();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point clickPosition = e.GetPosition(Canvas);

            double deltaX = (clickPosition.X - _mouseDragStartCanvasCenterPoint.X) / Canvas.ActualHeight;
            double deltaY = (clickPosition.Y - _mouseDragStartCanvasCenterPoint.Y) / Canvas.ActualHeight;

            if (_isHoldingRightMouse)
            {
                // User is dragging around with a right-click
                // Rotate the camera
                float scale = 0.5f;
                _cameraOrbitHoriz = _cameraOrbitHorizStart + ((float)deltaX * scale);
                _cameraOrbitVert = _cameraOrbitVertStart + ((float)deltaY * scale);

                //InvalidateCanvas();
            }
            else if (_isHoldingLeftMouse)
            {
                // User is dragging around with a left-click
                
                //InvalidateCanvas();
            }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // positive delta == scrolling up
            _cameraOrbitDistance = _cameraOrbitDistance * (1 - ((float)e.Delta / 800));
            
            //InvalidateCanvas();
        }

        private void Window_Loaded(object sender, EventArgs e)
        {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _speakers.StopPlayback().Await();
            _speakers.Dispose();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            InvalidateCanvas();
        }
    }
}