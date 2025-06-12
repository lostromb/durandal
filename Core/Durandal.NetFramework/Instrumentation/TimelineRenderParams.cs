using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    public class TimelineRenderParams
    {
        public int MaxSpansToPlot { get; set; }
        public int OutputGraphWidthPx { get; set; }
        public int RowHeightPx { get; set; }
        public int RowFontOffsetPx { get; set; }
        public int GutterWidtPx { get; set; }
        public float FontSizeEm { get; set; }

        public TimelineRenderParams()
        {
            MaxSpansToPlot = 100;
            OutputGraphWidthPx = 3000;
            RowHeightPx = 30;
            RowFontOffsetPx = 10;
            GutterWidtPx = 400;
            FontSizeEm = 10.0f;
        }
    }
}
