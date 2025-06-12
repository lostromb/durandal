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
    using Common.Time;
    using Durandal.Common.Logger;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : Page
    {
        private const int SCROLLBACK_LENGTH = 300;
        
        private ILogger _coreLogger;
        private FilterCriteria _logFilter;
        private SemaphoreSlim _viewUpdateInProgress = new SemaphoreSlim(1, 1);

        private TextBlock _footerElement;
        private FlowDocument _currentDocument;
        
        public LogView()
        {
            InitializeComponent();

            _coreLogger = ((App)App.Current).GetLogger();
            EventOnlyLogger eventLogger = EventOnlyLogger.TryExtractFromAggregate(_coreLogger);
            if (eventLogger != null)
            {
                eventLogger.LogUpdatedEvent.Subscribe(LogEventAsync);
            }

            // FIXME unsubscribe from this event on disposal
            _logFilter = new FilterCriteria()
                {
                    Level = LogLevel.Std | LogLevel.Err | LogLevel.Wrn
                };
        }

        private async Task LogEventAsync(object source, LogUpdatedEventArgs args, IRealTimeProvider realTime)
        {
            await Dispatcher.BeginInvoke(new CommonDelegates.VoidDelegate(UpdateTextBoxSynchronous));
        }

        private void UpdateTextBoxSynchronous()
        {
            EventOnlyLogger eventLogger = EventOnlyLogger.TryExtractFromAggregate(_coreLogger);
            if (eventLogger != null)
            {
                if (_viewUpdateInProgress.Wait(4))
                {
                    try
                    {
                        FlowDocument document = new FlowDocument();
                        document.PageWidth = 2000; // hackish width override to make sure we display everything
                        document.Background = Brushes.Black;
                        document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                        document.PagePadding = new Thickness(1, 3, 1, 3);
                        Stack<LogEvent> recentEvents = new Stack<LogEvent>();
                        int max = SCROLLBACK_LENGTH;
                        foreach (LogEvent e in eventLogger.History.FilterByCriteria(_logFilter, true))
                        {
                            recentEvents.Push(e);
                            if (max-- <= 0)
                                break;
                        }

                        while (recentEvents.Any())
                        {
                            LogEvent e = recentEvents.Pop();
                            TextBlock newTextBlock;
                            Paragraph line = CreateLineFromLogEvent(e, out newTextBlock);
                            document.Blocks.Add(line);
                        }

                        // Add an empty block at the bottom for padding
                        Paragraph footerLine = CreateLineFromLogEvent(null, out _footerElement);
                        document.Blocks.Add(footerLine);

                        if (_currentDocument != null)
                        {
                            _currentDocument.Loaded -= DocumentUpdated;
                        }

                        // Schedule an event to update the viewport when the document changes
                        document.Loaded += DocumentUpdated;
                        logOutput.Document = document;
                        _currentDocument = document;
                    }
                    finally
                    {
                        _viewUpdateInProgress.Release();
                    }
                }
        }
        }

        private void DocumentUpdated(object source, EventArgs arg)
        {
            if (_footerElement != null)
            {
                _footerElement.BringIntoView();
            }
        }

        public static Paragraph CreateLineFromLogEvent(LogEvent e, out TextBlock generatedTextBlock)
        {
            generatedTextBlock = new TextBlock()
                {
                    Text = e == null ? string.Empty : e.ToShortStringLocalTime(),
                    TextWrapping = TextWrapping.NoWrap
                };

            Paragraph para = new Paragraph();
            para.Inlines.Add(generatedTextBlock);
            para.FontFamily = new FontFamily("Lucida Console");
            para.FontSize = 10;
            para.LineHeight = 12;
            para.Margin = new Thickness(0, 0, 0, 2);
            para.TextAlignment = TextAlignment.Left;
            para.Foreground = Brushes.LightGray;

            if (e != null)
            {
                if (e.Level == LogLevel.Vrb)
                {
                    para.Foreground = Brushes.DarkGray;
                }
                else if (e.Level == LogLevel.Err)
                {
                    para.Foreground = Brushes.Red;
                }
                else if (e.Level == LogLevel.Wrn)
                {
                    para.Foreground = Brushes.Yellow;
                }
                else if (e.Level == LogLevel.Ins)
                {
                    para.Foreground = Brushes.Cyan;
                }
            }
            
            return para;
        }
    }
}
