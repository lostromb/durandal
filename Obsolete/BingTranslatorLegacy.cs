namespace Durandal.Common.Utils
{
    using System;
    using System.Speech.AudioFormat;
    using System.Speech.Synthesis;
    using Durandal.API.Utils;
    using Durandal.Common.Audio;
    using System.IO;
    using System.Net;

    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Web;
    using System.Threading;
    using Durandal.Common.Logger;

    public class BingTranslatorLegacy
    {
        /// <summary>
        /// Lazily-instantiated auth object for Azure Data Market (for translation API)
        /// </summary>
        private static TranslatorAdmAuthentication _admAuth = null;
        private static Object _mutex = new Object();

        public static bool IsSsml(string input)
        {
            return input.Contains("<speak");
        }

        /// <summary>
        /// Uses Bing Translate API to translate text between languages
        /// </summary>
        /// <param name="text"></param>
        /// <param name="logger"></param>
        /// <param name="targetLang"></param>
        /// <param name="sourceLang"></param>
        /// <returns></returns>
        public static string TranslateText(string text, ILogger logger, string targetLang, string sourceLang = "")
        {
            AdmAccessToken admToken = GetAccessToken(logger);
            if (admToken == null)
            {
                logger.Log("Could not retrieve access token for Translate request!", LogLevel.Err);
                return text;
            }

            string uri;
            if (string.IsNullOrEmpty(sourceLang))
            {
                // Let the API autodetect the input language
                uri = string.Format("http://api.microsofttranslator.com/v2/Http.svc/Translate?text={0}&to={1}", System.Web.HttpUtility.UrlEncode(text), targetLang);
            }
            else
            {
                uri = string.Format("http://api.microsofttranslator.com/v2/Http.svc/Translate?text={0}&to={1}&from={2}", System.Web.HttpUtility.UrlEncode(text), targetLang, sourceLang);
            }

            string authToken = "Bearer" + " " + admToken.access_token;

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add("Authorization", authToken);

            WebResponse response = null;
            try
            {
                response = httpWebRequest.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    DataContractSerializer dcs = new DataContractSerializer(Type.GetType("System.String"));
                    string translation = (string)dcs.ReadObject(stream);
                    return translation;
                }
            }
            catch (Exception e)
            {
                logger.Log("Translation call failed! " + e.Message, LogLevel.Err);
                return text;
            }
        }

        /// <summary>
        /// Uses Bing Translate API to detect the language of an arbitrary text string
        /// </summary>
        /// <param name="text"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static string DetectLanguage(string text, ILogger logger)
        {
            AdmAccessToken admToken = GetAccessToken(logger);
            if (admToken == null)
            {
                logger.Log("Could not retrieve access token for LanguageDetect request!", LogLevel.Err);
                return text;
            }

            string uri = string.Format("http://api.microsofttranslator.com/v2/Http.svc/Detect?text={0}", System.Web.HttpUtility.UrlEncode(text));

            string authToken = "Bearer" + " " + admToken.access_token;

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add("Authorization", authToken);

            WebResponse response = null;
            try
            {
                response = httpWebRequest.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    DataContractSerializer dcs = new DataContractSerializer(Type.GetType("System.String"));
                    string translation = (string)dcs.ReadObject(stream);
                    return translation;
                }
            }
            catch (Exception e)
            {
                logger.Log("Language detect call failed! " + e.Message, LogLevel.Err);
                return text;
            }
        }

        private static AdmAccessToken GetAccessToken(ILogger logger)
        {
            AdmAccessToken admToken;
            try
            {
                lock (_mutex)
                {
                    // Get Client Id and Client Secret from https://datamarket.azure.com/developer/applications/
                    // Refer obtaining AccessToken (http://msdn.microsoft.com/en-us/library/hh454950.aspx) 
                    TranslatorAdmAuthentication admAuth = GetAdmAuthentication(logger);
                    admToken = admAuth.GetAccessToken();
                    return admToken;
                }
            }
            catch (Exception e)
            {
                logger.Log("Error while retrieving ADM access token", LogLevel.Err);
                logger.Log(e.ToString(), LogLevel.Err);
                return null;
            }
        }

        /// <summary>
        /// Use lazy instantiate to get an ADM authorization object
        /// </summary>
        private static TranslatorAdmAuthentication GetAdmAuthentication(ILogger logger)
        {
            if (_admAuth == null)
            {
                // This uses Logan's test API key
                _admAuth = new TranslatorAdmAuthentication("Durandal", "kjSIKTm0CI2rmYPvJG6CcdGsZPPrjBKpck4zI30m0Zo=", logger);
            }
            return _admAuth;
        }
    }

    #region Low-level token junk
    internal class TranslatorAdmAuthentication
    {
        public static readonly string DatamarketAccessUri = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
        private string clientId;
        private string clientSecret;
        private string request;
        private volatile AdmAccessToken token;
        private Timer accessTokenRenewer;
        //Access token expires every 10 minutes. Renew it every 9 minutes only.
        private const int RefreshTokenDuration = 9;
        private ILogger _logger;

        public TranslatorAdmAuthentication(string clientId, string clientSecret, ILogger logger)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this._logger = logger;
            //If clientid or client secret has special characters, encode before sending request
            this.request = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}&scope=http://api.microsofttranslator.com", HttpUtility.UrlEncode(clientId), HttpUtility.UrlEncode(clientSecret));
            this.token = HttpPost(DatamarketAccessUri, this.request);
            // Renew the token every specfied minutes in the background
            accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback), this, TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
        }
        public AdmAccessToken GetAccessToken()
        {
            return this.token;
        }
        private void RenewAccessToken()
        {
            AdmAccessToken newAccessToken = HttpPost(DatamarketAccessUri, this.request);
            // Swap the new token with old one - since this is on the timer's thread it shouldn't cause delay to any existing requests
            this.token = newAccessToken;
            _logger.Log(string.Format("Renewed token for user: {0} is: {1}", this.clientId, this.token.access_token), LogLevel.Vrb);
        }
        private void OnTokenExpiredCallback(object stateInfo)
        {
            try
            {
                RenewAccessToken();
            }
            catch (Exception ex)
            {
                _logger.Log(string.Format("Failed renewing access token. Details: {0}", ex.Message), LogLevel.Err);
            }
            finally
            {
                try
                {
                    accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
                }
                catch (Exception ex)
                {
                    _logger.Log(string.Format("Failed to reschedule the timer to renew access token. Details: {0}", ex.Message), LogLevel.Err);
                }
            }
        }
        private AdmAccessToken HttpPost(string DatamarketAccessUri, string requestDetails)
        {
            //Prepare OAuth request 
            WebRequest webRequest = WebRequest.Create(DatamarketAccessUri);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(requestDetails);
            webRequest.ContentLength = bytes.Length;
            using (Stream outputStream = webRequest.GetRequestStream())
            {
                outputStream.Write(bytes, 0, bytes.Length);
            }
            using (WebResponse webResponse = webRequest.GetResponse())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AdmAccessToken));
                //Get deserialized object from JSON stream
                AdmAccessToken token = (AdmAccessToken)serializer.ReadObject(webResponse.GetResponseStream());
                return token;
            }
        }
    }
    #endregion
}
