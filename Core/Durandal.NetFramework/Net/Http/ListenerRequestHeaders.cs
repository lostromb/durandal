using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using System.Collections;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// IHttpHeaders wrapper for a NameValueCollection of headers, used in http.sys and legacy ASP.
    /// Wraps the inner collection directly without creating a copy.
    /// </summary>
    internal class ListenerRequestHeaders : IHttpHeaders
    {
        private readonly NameValueCollection _innerHeaders;

        public ListenerRequestHeaders(NameValueCollection innerHeaders)
        {
            _innerHeaders = innerHeaders.AssertNonNull(nameof(innerHeaders));
        }

        public string this[string headerName]
        {
            get
            {
                headerName.AssertNonNullOrEmpty(nameof(headerName));
                return _innerHeaders.Get(headerName);
            }
            set
            {
                throw new NotSupportedException("Request headers are read-only");
            }
        }

        public int KeyCount
        {
            get
            {
                return _innerHeaders.Keys.Count;
            }
        }

        public void Add(string headerName, string headerValue)
        {
            throw new NotSupportedException("Request headers are read-only");
        }

        public bool ContainsKey(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            return _innerHeaders[headerName] != null; // string.Empty is an allowable header value
        }

        public bool ContainsValue(string headerName, string expectedValue, StringComparison stringComparison)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            expectedValue.AssertNonNullOrEmpty(nameof(expectedValue));
            string[] values = _innerHeaders.GetValues(headerName);
            foreach (string value in values)
            {
                if (string.Equals(value, expectedValue, stringComparison))
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<string> EnumerateValueList(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            return _innerHeaders.GetValues(headerName);
        }

        public IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>> GetEnumerator()
        {
            return new HeaderEnumerator(_innerHeaders);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotSupportedException("Please upgrade from .Net Framework 1.0");
        }

        public void Remove(string headerName)
        {
            throw new NotSupportedException("Request headers are read-only");
        }

        public void Set(string headerName, string headerValue)
        {
            throw new NotSupportedException("Request headers are read-only");
        }

        public bool TryGetValue(string headerName, out string headerValue)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            headerValue = _innerHeaders[headerName];
            return headerValue != null; // string.Empty is an allowable header value
        }

        private class HeaderEnumerator : IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>>
        {
            private readonly IEnumerator _keyEnumerator;
            private readonly NameValueCollection _values;

            public HeaderEnumerator(NameValueCollection innerValues)
            {
                _values = innerValues.AssertNonNull(nameof(innerValues));
                _keyEnumerator = innerValues.GetEnumerator();
                Current = default(KeyValuePair<string, IReadOnlyCollection<string>>);
            }

            public KeyValuePair<string, IReadOnlyCollection<string>> Current
            {
                get; private set;
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_keyEnumerator.MoveNext())
                {
                    string key = _keyEnumerator.Current as string;
                    IReadOnlyCollection<string> values = _values.GetValues(key);
                    Current = new KeyValuePair<string, IReadOnlyCollection<string>>(key, values);
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                _keyEnumerator.Reset();
            }
        }
    }
}
