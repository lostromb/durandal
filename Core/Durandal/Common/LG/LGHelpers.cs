using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.LG.Statistical;
using Durandal.Common.LG.Template;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.LG
{
    public static class LGHelpers
    {
        private static readonly Regex LG_FILENAME_MATCHER = new Regex("(.+?) (\\d)\\.(\\d)(?:\\.[a-z]{2}-[a-z]{2}\\.|\\.)ini");

        /// <summary>
        /// Finds all LG files that could be specified for a single domain.
        /// Usually files are in the form of "DOMAIN.en-US.ini"
        /// </summary>
        /// <returns></returns>
        private static async Task<IList<VirtualPath>> FindLgFilesForPlugin(IFileSystem fileSystem, VirtualPath lgDirectory, PluginStrongName pluginName)
        {
            IList<VirtualPath> lgFiles = new List<VirtualPath>();
            if (await fileSystem.ExistsAsync(lgDirectory).ConfigureAwait(false))
            {
                foreach (VirtualPath lgFile in await fileSystem.ListFilesAsync(lgDirectory).ConfigureAwait(false))
                {
                    Match m = LG_FILENAME_MATCHER.Match(lgFile.Name);
                    if (m.Success &&
                        m.Groups[1].Value.Equals(pluginName.PluginId) &&
                        m.Groups[2].Value.Equals(pluginName.MajorVersion.ToString()) &&
                        m.Groups[3].Value.Equals(pluginName.MinorVersion.ToString()))
                    {
                        lgFiles.Add(lgFile);
                    }
                }
            }

            return lgFiles;
        }

        /// <summary>
        /// Reads the first line of an LG template file and looks for the "[engine:x]" tag at the top.
        /// Then returns the value of that engine. If not tag is found, this returns null
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        private static async Task<string> DetectLgEngine(IFileSystem fileSystem, VirtualPath file)
        {
            using (StreamReader reader = new StreamReader(await fileSystem.OpenStreamAsync(file, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false)))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine().Trim().ToLower();
                    if (line.StartsWith("[engine:"))
                    {
                        return line.Substring(8, line.Length - 9);
                    }
                }

                reader.Dispose();
            }

            return null;
        }

        /// <summary>
        /// Parses lg configuration files to build the implementation LG engine for a single plugin.
        /// </summary>
        /// <param name="lgFileSystem">The filesystem containing LG files</param>
        /// <param name="lgDataDirectory">The directory of where to find LG files (usually just /lg)</param>
        /// <param name="pluginStrongName">The strong name of the plugin that we are building this engine for</param>
        /// <param name="logger">A logger to use as the default for the new engine</param>
        /// <param name="scriptCompiler">The script compiler to use for the new engine</param>
        /// <param name="nlTools">A dictionary of locale -> NLP tools</param>
        /// <returns>A newly created LG engine for a single plugin</returns>
        public static async Task<ILGEngine> BuildLGEngineForPlugin(
            IFileSystem lgFileSystem,
            VirtualPath lgDataDirectory,
            PluginStrongName pluginStrongName,
            ILogger logger,
            ILGScriptCompiler scriptCompiler,
            INLPToolsCollection nlTools)
        {
            // Load LG files that match this domain
            ILGEngine thisPluginLg = null;
            IList<VirtualPath> lgFiles = await FindLgFilesForPlugin(lgFileSystem, lgDataDirectory, pluginStrongName).ConfigureAwait(false);

            // Now try and use those files to make an LG engine specific to this plugin
            if (lgFiles.Count > 0)
            {
                string engine = null;
                VirtualPath firstFile = lgFiles[0];

                // Make sure all files use the same engine
                for (int fileIdx = 0; fileIdx < lgFiles.Count; fileIdx++)
                {
                    VirtualPath otherFile = lgFiles[fileIdx];
                    string otherEng = await DetectLgEngine(lgFileSystem, otherFile).ConfigureAwait(false);
                    if (otherEng == null)
                    {
                        logger.Log("The LG template " + otherFile.FullName + " does not specify an [engine:x] field as its first line and thus cannot be loaded", LogLevel.Err);
                    }
                    else if (engine == null)
                    {
                        engine = otherEng;
                    }
                    else if (!string.Equals(otherEng, engine))
                    {
                        logger.Log(string.Format("All LG templates for the domain {0} must use the same engine. {1} specifies {2} when it other templates use {3}", pluginStrongName.ToString(), otherFile.FullName, otherEng, engine), LogLevel.Err);
                        engine = null;
                        break;
                    }
                }

                logger.Log("Loading LG templates for " + pluginStrongName.ToString() + " using engine \"" + engine + "\"", LogLevel.Std);
                if (engine == null)
                {
                    thisPluginLg = new NullLGEngine();
                }
                else if (engine.Equals("template"))
                {
                    thisPluginLg = await TemplateBasedLGEngine.Create(
                        lgFiles,
                        lgFileSystem,
                        logger.Clone("LanguageGeneration-" + pluginStrongName.PluginId)).ConfigureAwait(false);
                }
                else if (engine.Equals("statistical"))
                {
                    thisPluginLg = await StatisticalLGEngine.Create(
                        lgFileSystem,
                        logger.Clone("LanguageGeneration-" + pluginStrongName.PluginId),
                        string.Format("{0} {1}.{2}", pluginStrongName.PluginId, pluginStrongName.MajorVersion, pluginStrongName.MinorVersion),
                        scriptCompiler,
                        lgFiles,
                        nlTools).ConfigureAwait(false);
                }
                else
                {
                    logger.Log("No LG engine exists of type \"" + engine + "\" found in " + firstFile.FullName, LogLevel.Err);
                    thisPluginLg = new NullLGEngine();
                }
            }
            else
            {
                logger.Log("No LG templates exists for the plugin \"" + pluginStrongName.ToString() + "\"", LogLevel.Wrn);
                thisPluginLg = new NullLGEngine();
            }

            return thisPluginLg;
        }
    }
}
