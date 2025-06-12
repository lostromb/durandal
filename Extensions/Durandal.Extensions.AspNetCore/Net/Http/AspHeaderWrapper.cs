using Durandal.Common.Collections;
using Durandal.Common.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    internal class AspHeaderWrapper : IHttpHeaders
    {
        private readonly IHeaderDictionary _innerDictionary;

        public AspHeaderWrapper(IHeaderDictionary innerDictionary)
        {
            _innerDictionary = innerDictionary.AssertNonNull(nameof(innerDictionary));
        }

        public string this[string headerName]
        {
            get => _innerDictionary[headerName];
            set => throw new InvalidOperationException("Incoming HTTP headers are read-only");
        }

        public int KeyCount => _innerDictionary.Keys.Count;

        public void Add(string headerName, string headerValue)
        {
            throw new InvalidOperationException("Incoming HTTP headers are read-only");
        }

        public bool ContainsKey(string headerName)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            return _innerDictionary.ContainsKey(headerName);
        }

        public bool ContainsValue(string headerName, string expectedValue, StringComparison stringComparison)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            expectedValue.AssertNonNullOrEmpty(nameof(expectedValue));
            foreach (string value in _innerDictionary.GetCommaSeparatedValues(headerName))
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
            return _innerDictionary.GetCommaSeparatedValues(headerName);
        }

        public IEnumerator<KeyValuePair<string, IReadOnlyCollection<string>>> GetEnumerator()
        {
            return new CastingEnumerator<
                KeyValuePair<string, StringValues>,
                KeyValuePair<string, IReadOnlyCollection<string>>>(
                    _innerDictionary.GetEnumerator(),
                    (svKvp) => new KeyValuePair<string, IReadOnlyCollection<string>>(svKvp.Key, svKvp.Value));
        }

        public void Remove(string headerName)
        {
            throw new InvalidOperationException("Incoming HTTP headers are read-only");
        }

        public void Set(string headerName, string headerValue)
        {
            throw new InvalidOperationException("Incoming HTTP headers are read-only");
        }

        public bool TryGetValue(string headerName, out string headerValue)
        {
            headerName.AssertNonNullOrEmpty(nameof(headerName));
            StringValues returnVal;
            if (_innerDictionary.TryGetValue(headerName, out returnVal))
            {
                headerValue = returnVal.ToString();
                return true;
            }

            headerValue = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CastingEnumerator<
                KeyValuePair<string, StringValues>,
                KeyValuePair<string, IReadOnlyCollection<string>>>(
                    _innerDictionary.GetEnumerator(),
                    (svKvp) => new KeyValuePair<string, IReadOnlyCollection<string>>(svKvp.Key, svKvp.Value));
        }
    }
}
