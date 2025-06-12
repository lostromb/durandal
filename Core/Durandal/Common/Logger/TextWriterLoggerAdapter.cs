using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Logger
{
    /// <summary>
    /// Implements a TextWriter that outputs whole lines to a given ILogger
    /// </summary>
    public class TextWriterLoggerAdapter : TextWriter
    {
        private const int MAX_LINE_LENGTH = 16 * 1024;
        private readonly ILogger _target;
        private readonly LogLevel _targetLogLevel;
        private PooledStringBuilder _buffer;

        public TextWriterLoggerAdapter(ILogger target, LogLevel targetLogLevel = LogLevel.Std)
        {
            _target = target;
            _targetLogLevel = targetLogLevel;
            _buffer = StringBuilderPool.Rent();
        }

        public override Encoding Encoding => StringUtils.UTF8_WITHOUT_BOM;

        public override void Write(char value)
        {
            if (value == '\n' || _buffer.Builder.Length >= MAX_LINE_LENGTH)
            {
                if (_buffer.Builder.Length > 0)
                {
                    _target.Log(
                        PooledLogEvent.Create(
                            _target.ComponentName,
                            _buffer,
                            _targetLogLevel,
                            HighPrecisionTimer.GetCurrentUTCTime(),
                            _target.TraceId));
                    _buffer = StringBuilderPool.Rent();
                }
            }
            else if (value != '\r')
            {
                _buffer.Builder.Append(value);
            }
        }
    }
}
