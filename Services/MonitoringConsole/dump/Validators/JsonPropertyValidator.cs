using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net.Http;
using Photon.Common.Schemas;
using Newtonsoft.Json.Linq;
using Durandal.Common.Utils.Tasks;

namespace Photon.Common.Validators
{
    /// <summary>
    /// Validator which interprets the entire HTTP response as a json value, and asserts that a particular token matches a specific value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class JsonPropertyValidator<T> : IHttpResponseValidator
    {
        private string _jPath;
        private T _expectedValue;
        private JsonPropertyValidationType _validationType;

        public JsonPropertyValidator(string jPath, T expectedValue, JsonPropertyValidationType validationType = JsonPropertyValidationType.Equals)
        {
            if ((validationType == JsonPropertyValidationType.Contains ||
                validationType == JsonPropertyValidationType.NotContains) &&
                typeof(T) != typeof(string))
            {
                throw new ArgumentException("Validation type of \"contains\" can only be used for string tokens");
            }

            // Execute the jpath against a dummy object to ensure it is sane
            try
            {
                new JObject().SelectToken(jPath);
            }
            catch (Exception e)
            {
                throw new ArgumentException("The JPath string \"" + jPath + "\" is invalid. " + e.Message);
            }

            _jPath = jPath;
            _expectedValue = expectedValue;
            _validationType = validationType;
        }

        public async Task<SingleTestResult> Validate(HttpResponse httpResponse)
        {
            string responseString = httpResponse.GetPayloadAsString();
            JToken obj = JToken.Parse(responseString);
            JToken token = obj.SelectToken(_jPath);
            if (token == null)
            {
                if (_expectedValue == null && _validationType == JsonPropertyValidationType.Equals)
                {
                    return new SingleTestResult()
                    {
                        Success = true
                    };
                }
                else if (_expectedValue != null && _validationType == JsonPropertyValidationType.NotEquals)
                {
                    return new SingleTestResult()
                    {
                        Success = true
                    };
                }
                else
                {
                    await DurandalTaskExtensions.NoOpTask;
                    return new SingleTestResult()
                    {
                        Success = false,
                        ErrorMessage = "JSON token at JPath \"" + _jPath + "\" was not found! Response follows:\r\n" + responseString
                    };
                }
            }

            T val = token.Value<T>();
            if (_validationType == JsonPropertyValidationType.Equals && !_expectedValue.Equals(val))
            {
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "JSON token at JPath \"" + _jPath + "\" did not match expected value. Expected \"" + _expectedValue + "\", got \"" + val + "\". Response follows:\r\n" + responseString
                };
            }
            else if (_validationType == JsonPropertyValidationType.NotEquals && _expectedValue.Equals(val))
            {
                return new SingleTestResult()
                {
                    Success = false,
                    ErrorMessage = "JSON token at JPath \"" + _jPath + "\" did not match expected value. Expected anything EXCEPT \"" + _expectedValue + "\", got \"" + val + "\". Response follows:\r\n" + responseString
                };
            }
            else if (typeof(T) == typeof(string))
            {
                string expectedString = _expectedValue as string;
                string actualString = val as string;
                if (_validationType == JsonPropertyValidationType.Contains && !actualString.Contains(expectedString))
                {
                    return new SingleTestResult()
                    {
                        Success = false,
                        ErrorMessage = "JSON token at JPath \"" + _jPath + "\" did not match expected value. Expected string to contain \"" + expectedString + "\", got \"" + actualString + "\". Response follows:\r\n" + responseString
                    };
                }
                else if (_validationType == JsonPropertyValidationType.NotContains && actualString.Contains(expectedString))
                {
                    return new SingleTestResult()
                    {
                        Success = false,
                        ErrorMessage = "JSON token at JPath \"" + _jPath + "\" did not match expected value. Expected string to NOT contain \"" + expectedString + "\", got \"" + actualString + "\". Response follows:\r\n" + responseString
                    };
                }
            }

            return new SingleTestResult()
            {
                Success = true
            };
        }
    }
}
