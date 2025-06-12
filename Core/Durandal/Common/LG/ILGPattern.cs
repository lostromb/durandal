using Durandal.Common.Logger;
using Durandal.Common.NLP.Language;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Durandal.API
{
    public interface ILGPattern
    {
        /// <summary>
        /// Renders all outputs of this pattern given the current set of substitutions
        /// </summary>
        /// <returns></returns>
        Task<RenderedLG> Render();

        /// <summary>
        /// Applies the result of this rendered pattern to a specific dialog result.
        /// If there is custom code attached to this template, that code will be invoked
        /// as part of this method.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        Task<PluginResult> ApplyToDialogResult(PluginResult input);

        /// <summary>
        /// Returns a clone of this pattern with all substitutions reset
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="newClientContext"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        ILGPattern Clone(ILogger logger, ClientContext newClientContext, bool debug = false);

        /// <summary>
        /// Applies a substitution by inserting a field name and value into the template.
        /// This value will then be used to help render the final result
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        ILGPattern Sub(string key, object value);

        /// <summary>
        /// Gets the name of this pattern
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the associated language code of this pattern
        /// </summary>
        LanguageCode Locale { get; }
    }
}