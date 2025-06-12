using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// A static class for generating timeline images from tables of event span data
    /// </summary>
    public static class TimelineRenderer
    {
        public static byte[] RenderTraceTimelineToPng(UnifiedTrace trace, ILogger logger = null, TimelineRenderParams renderParams = null)
        {
            List<TimeLineEntry> entries = new List<TimeLineEntry>();
            foreach (KeyValuePair<string, UnifiedTraceLatencyCollection> latencyCollection in trace.Latencies)
            {
                foreach (UnifiedTraceLatencyEntry latencyEntry in latencyCollection.Value.Values)
                {
                    if (latencyEntry.StartTime.HasValue)
                    {
                        if (string.IsNullOrEmpty(latencyEntry.Id))
                        {
                            entries.Add(new TimeLineEntry()
                            {
                                SpanName = latencyCollection.Key,
                                DurationMs = latencyEntry.Value,
                                StartTime = latencyEntry.StartTime.Value,
                                EndTime = latencyEntry.StartTime.Value + TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(latencyEntry.Value)
                            });
                        }
                        else
                        {
                            entries.Add(new TimeLineEntry()
                            {
                                SpanName = latencyCollection.Key + "-" + latencyEntry.Id,
                                DurationMs = latencyEntry.Value,
                                StartTime = latencyEntry.StartTime.Value,
                                EndTime = latencyEntry.StartTime.Value + TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(latencyEntry.Value)
                            });
                        }
                    }
                }
            }

            return RenderTimelineToPng(entries, logger, renderParams);
        }

        /// <summary>
        /// Generates a timeline from the input data and returns a byte array containing encoded PNG image data.
        /// </summary>
        /// <param name="timelineEntries">The list of entries to process for making the timeline</param>
        /// <param name="logger">A logger for status</param>
        /// <param name="renderParams">Optional rendering parameters</param>
        /// <returns>An encoded PNG image as a byte array</returns>
        public static byte[] RenderTimelineToPng(List<TimeLineEntry> timelineEntries, ILogger logger = null, TimelineRenderParams renderParams = null)
        {
            timelineEntries = timelineEntries.AssertNonNull(nameof(timelineEntries));
            renderParams = renderParams ?? new TimelineRenderParams();
            logger = logger ?? NullLogger.Singleton;

            // Sort entries by length descending
            logger.Log("Processing " + timelineEntries.Count.ToString() + " timeline entries");
            timelineEntries.Sort((a, b) => b.DurationMs.CompareTo(a.DurationMs));

            // What's the entire span of this timeline?
            DateTimeOffset spanStart = timelineEntries[0].StartTime;
            DateTimeOffset spanEnd = timelineEntries[0].EndTime;

            for (int spanIdx = 0; spanIdx < renderParams.MaxSpansToPlot && spanIdx < timelineEntries.Count; spanIdx++)
            {
                TimeLineEntry entry = timelineEntries[spanIdx];
                if (entry.StartTime < spanStart)
                {
                    spanStart = entry.StartTime;
                }
                if (entry.EndTime > spanEnd)
                {
                    spanEnd = entry.EndTime;
                }
            }

            double totalSpanLength = (spanEnd - spanStart).TotalMilliseconds;
            logger.Log("Total span length is " + totalSpanLength + "ms");

            List<TimelineSpan> spans = new List<TimelineSpan>();

            // Now prune the top N entries
            for (int c = 0; c < renderParams.MaxSpansToPlot && c < timelineEntries.Count; c++)
            {
                TimeLineEntry entry = timelineEntries[c];
                TimelineSpan newSpan = new TimelineSpan()
                {
                    SpanName = entry.SpanName,
                    StartTimeMs = (entry.StartTime - spanStart).TotalMilliseconds,
                    EndTimeMs = (entry.EndTime - spanStart).TotalMilliseconds
                };
                spans.Add(newSpan);
            }

            // Sort by start time ascending
            spans.Sort((a, b) => a.StartTimeMs.CompareTo(b.StartTimeMs));

            // Now plot them onto a bitmap
            int outputImageHeight = renderParams.RowHeightPx * (spans.Count + 1);
            double xScale = (double)renderParams.OutputGraphWidthPx / totalSpanLength;

            logger.Log("Calculated image height is " + outputImageHeight + "px");

            using (Image image = new Bitmap(renderParams.OutputGraphWidthPx + renderParams.GutterWidtPx, outputImageHeight, PixelFormat.Format32bppArgb))
            {
                Graphics g = Graphics.FromImage(image);
                using (Font font = new Font(FontFamily.GenericMonospace, renderParams.FontSizeEm))
                using (Brush fontBrush = new SolidBrush(Color.Black))
                {
                    Random rand = new Random();

                    g.Clear(Color.White);

                    int row = renderParams.RowHeightPx;
                    foreach (TimelineSpan span in spans)
                    {
                        using (Brush barBrush = new SolidBrush(Color.FromArgb(255, rand.Next(100, 230), rand.Next(100, 230), rand.Next(100, 230))))
                        {
                            int x = (int)(span.StartTimeMs * xScale);
                            double spanMs = span.EndTimeMs - span.StartTimeMs;
                            int width = Math.Max(1, (int)(spanMs * xScale));
                            int y = row;
                            g.FillRectangle(barBrush, x, y, width, renderParams.RowHeightPx);
                            g.DrawString(string.Format("{0}: {1:F2}ms", span.SpanName, spanMs), font, fontBrush, x, y + renderParams.RowFontOffsetPx);
                            row = row + renderParams.RowHeightPx;
                        }
                    }

                    // Draw tick marks transparent on top

                    int msPerTickMark = 10;
                    if (totalSpanLength > 500)
                    {
                        msPerTickMark = 100;
                    }
                    if (totalSpanLength > 5000)
                    {
                        msPerTickMark = 1000;
                    }

                    using (Pen tickPen = new Pen(Color.FromArgb(128, 0, 0, 0), 1))
                    {
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        int tickCount = 0;
                        for (double tickX = 0; tickX < renderParams.OutputGraphWidthPx; tickX += msPerTickMark * xScale)
                        {
                            g.DrawLine(tickPen, (int)tickX, 0, (int)tickX, outputImageHeight);
                            g.DrawString(string.Format("{0}ms", tickCount), font, fontBrush, (int)tickX, 0);
                            tickCount += msPerTickMark;
                        }
                    }
                }

                using (RecyclableMemoryStream fileStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    image.Save(fileStream, ImageFormat.Png);
                    return fileStream.ToArray();
                }
            }
        }

        private class TimelineSpan
        {
            public double StartTimeMs { get; set; }
            public double EndTimeMs { get; set; }
            public string SpanName { get; set; }
        }
    }
}
