using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Logger;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG
{
    public abstract class BaseLGPattern : ILGPattern
    {
        public abstract LanguageCode Locale { get; internal set; }
        public abstract string Name { get; internal set; }
        public abstract ILGPattern Clone(ILogger logger, ClientContext newClientContext, bool debug);
        public abstract Task<RenderedLG> Render();
        public abstract ILGPattern Sub(string key, object value);
        protected abstract ClientContext CurrentClientContext { get; }

        /// <summary>
        /// Applies all of the substitutions in this pattern to a PluginResult,
        /// setting the response text, SSML, and so forth.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<PluginResult> ApplyToDialogResult(PluginResult input)
        {
            RenderedLG rendered = await Render().ConfigureAwait(false);
            if (!CurrentClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayUnlimitedText))
            {
                input.ResponseText = rendered.ShortText;
            }
            else
            {
                // Always try and respond with text, even if DisplayUnlimitedText is not specified.
                // This can help a little bit with clients that specify, for example, DisplayHtml but not DisplayText.
                // In those cases, the text will be rendered as HTML in the end.
                input.ResponseText = rendered.Text;
            }

            // TODO: Detect if the template does not use SSML, and try and create SSML in that case
            input.ResponseSsml = rendered.Spoken;

            return input;
        }
    }
}
