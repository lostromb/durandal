using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace Dorado.Monitoring.StatusReporter.Controllers
{
    public class ReproController : ApiController
    {
        [HttpGet]
        [Route("api/async_test")]
        public async Task<IHttpActionResult> ControllerFunction()
        {
            NameValueCollection getParams = HttpContext.Current.Request.QueryString;
            bool configureAwaitTrueInController = !string.IsNullOrEmpty(getParams.Get("controller"));
            bool configureAwaitTrueInHelper = !string.IsNullOrEmpty(getParams.Get("helper"));
            bool configureAwaitTrueInLibrary = !string.IsNullOrEmpty(getParams.Get("library"));

            for (int c = 0; c < 10; c++)
            {
                await HelperFunction(configureAwaitTrueInHelper, configureAwaitTrueInLibrary).ConfigureAwait(configureAwaitTrueInController);
            }

            return this.Ok();
        }

        [HttpGet]
        [Route("api/async_test_closure")]
        public async Task<IHttpActionResult> ControllerFunctionWithClosure()
        {
            NameValueCollection getParams = HttpContext.Current.Request.QueryString;
            bool configureAwaitTrueInController = !string.IsNullOrEmpty(getParams.Get("controller"));
            bool configureAwaitTrueInHelper = !string.IsNullOrEmpty(getParams.Get("helper"));
            bool configureAwaitTrueInLibrary = !string.IsNullOrEmpty(getParams.Get("library"));

            for (int c = 0; c < 10; c++)
            {
                await HelperFunctionWithClosure(this.Request, configureAwaitTrueInHelper, configureAwaitTrueInLibrary).ConfigureAwait(configureAwaitTrueInController);
            }

            return this.Ok();
        }

        /// <summary>
        /// Helper function that requires HTTP context
        /// </summary>
        /// <param name="configAwaitInHelper"></param>
        /// <param name="configAwaitInLibrary"></param>
        /// <returns></returns>
        private static async Task HelperFunction(bool configAwaitInHelper, bool configAwaitInLibrary)
        {
            for (int c = 0; c < 10; c++)
            {
                HttpContext currentContext = HttpContext.Current;
                string[] allHeaderKeys = currentContext.Request.Headers.AllKeys;
                await LibraryFunction(configAwaitInLibrary).ConfigureAwait(configAwaitInHelper);
            }
        }

        /// <summary>
        /// Helper function which requires HTTP context but accepts it as a closure object in an input parameter
        /// </summary>
        /// <param name="request"></param>
        /// <param name="configAwaitInHelper"></param>
        /// <param name="configAwaitInLibrary"></param>
        /// <returns></returns>
        private static async Task HelperFunctionWithClosure(HttpRequestMessage request, bool configAwaitInHelper, bool configAwaitInLibrary)
        {
            for (int c = 0; c < 10; c++)
            {
                var allHeaderKeys = request.Headers.ToArray();
                await LibraryFunction(configAwaitInLibrary).ConfigureAwait(configAwaitInHelper);
            }
        }

        /// <summary>
        /// Library function that doesn't care about HTTP context
        /// </summary>
        /// <param name="configAwaitInLibrary"></param>
        /// <returns></returns>
        private static async Task LibraryFunction(bool configAwaitInLibrary)
        {
            await Task.Delay(10).ConfigureAwait(configAwaitInLibrary);
        }
    }
}
