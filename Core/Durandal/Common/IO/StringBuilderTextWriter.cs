using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a <see cref="TextWriter"/> which writes its output to a <see cref="StringBuilder"/>.
    /// </summary>
    public class StringBuilderTextWriter : TextWriter
    {
        private readonly IFormatProvider _formatProvider;
        private readonly StringBuilder _builder;

        /// <summary>
        /// Constructs a new <see cref="StringBuilderTextWriter"/> with the specified inner string builder.
        /// </summary>
        /// <param name="inner">The string builder to append to.</param>
        public StringBuilderTextWriter(StringBuilder inner)
            : this(inner, CultureInfo.InvariantCulture)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="StringBuilderTextWriter"/> with the specified inner string builder and format provider.
        /// </summary>
        /// <param name="inner">The string builder to append to.</param>
        /// <param name="formatProvider">The format provider to use for formatted string calls.</param>
        public StringBuilderTextWriter(
            StringBuilder inner,
            IFormatProvider formatProvider)
        {
            _builder = inner.AssertNonNull(nameof(inner));
            _formatProvider = formatProvider.AssertNonNull(nameof(formatProvider));
        }

        /// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;

        /// <inheritdoc />
        public override void Write(string format, params object[] arg)
        {
            _builder.AppendFormat(_formatProvider, format, arg);
        }

        /// <inheritdoc />
        public override void Write(string value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(char value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(bool value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(char[] buffer)
        {
            _builder.Append(buffer);
        }

        /// <inheritdoc />
        public override void Write(char[] buffer, int index, int count)
        {
            _builder.Append(buffer, index, count);
        }

        /// <inheritdoc />
        public override void Write(decimal value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(double value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(float value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(int value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(long value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(object value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(uint value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void Write(ulong value)
        {
            _builder.Append(value);
        }

        /// <inheritdoc />
        public override void WriteLine()
        {
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(decimal value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(double value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(float value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(int value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(long value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(object value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(uint value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(ulong value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(bool value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(char value)
        {
            _builder.Append(value);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(char[] buffer)
        {
            _builder.Append(buffer);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(char[] buffer, int index, int count)
        {
            _builder.Append(buffer, index, count);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(string format, params object[] arg)
        {
            _builder.AppendFormat(_formatProvider, format, arg);
            _builder.AppendLine();
        }

        /// <inheritdoc />
        public override void WriteLine(string value)
        {
            _builder.AppendLine(value);
        }
    }
}
