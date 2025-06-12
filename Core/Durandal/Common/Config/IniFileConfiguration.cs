using Durandal.Common.Logger;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Config.Annotation;
using Durandal.Common.Parsers;
using Durandal.Common.Collections;

namespace Durandal.Common.Config
{
    /// <summary>
    /// Specifies a configuration that is backed by an .ini config file.
    /// There are two different operating modes of this class depending on whether a file name is specified.
    /// If there is a configFileName specified, this config will match that .ini file on disk, which includes automatically reflecting changes bidirectionally.
    /// If configFileName is null, this config will operate in-memory only, however you will be able to read/write arbitrary .ini configs using ReadStream and WriteStream.
    /// </summary>
    public class IniFileConfiguration : AbstractConfiguration
    {
        private readonly Regex ANNOTATION_PARSE_REGEX = new Regex("\\[([a-zA-Z0-9]+?)(?:\\|([^\\]]+))?\\]");

        private readonly bool _inMemoryOnly;
        private readonly bool _trackChanges;
        private readonly IFileSystem _fileSystem;
        private readonly VirtualPath _iniFilePath;
        private readonly List<string> _originalFileContents;
        private IFileSystemWatcher _fileChangeWatcher;
        private int _disposed = 0;

        private IniFileConfiguration(
            ILogger logger,
            VirtualPath configFileName,
            IFileSystem fileSystem,
            IRealTimeProvider realTime,
            bool trackChanges) : base(logger, realTime)
        {
            _inMemoryOnly = fileSystem == null || configFileName == null;
            _originalFileContents = new List<string>();
            _trackChanges = trackChanges;

            if (_inMemoryOnly)
            {
                // If there is no filename or file system, then this is basically an in-memory config.
                // We assume then the user will use ReadStream() later on to populate this object.
                _iniFilePath = null;
                _fileSystem = NullFileSystem.Singleton;
            }
            else
            {
                _iniFilePath = configFileName;
                _fileSystem = fileSystem;
            }
        }

        /// <summary>
        /// Retrieves (or creates, if one doesn't exist) a configuration resource (file) with the given name.
        /// If the file does not exist, attempt to find the "default" config, and then create a new configuration file.
        /// For example, if you passed "config.ini" and that file does not exist, this constructor will look for "config.default.ini" and then create a new "config.ini" from that.
        /// </summary>
        /// <param name="logger">A logger to write warnings/errors to</param>
        /// <param name="configFileName">The name of the configuration file to map to, e.g. "config.ini". If null, this configuration will operate in-memory only.</param>
        /// <param name="fileSystem">The underlying file system. If null, this configuration will operate in-memory only.</param>
        /// <param name="realTime">A definition of real time, used for background updates to the underlying file</param>
        /// <param name="warnIfNotFound">Emit a warning to the log if the given file is not found</param>
        /// <param name="reloadOnExternalChanges">Whether you want to actively monitor the filesystem and reload the .ini file if it is changed externally</param>
        public static async Task<IniFileConfiguration> Create(
            ILogger logger,
            VirtualPath configFileName,
            IFileSystem fileSystem,
            IRealTimeProvider realTime,
            bool warnIfNotFound = false,
            bool reloadOnExternalChanges = false)
        {
            IniFileConfiguration returnVal = new IniFileConfiguration(logger, configFileName, fileSystem, realTime, reloadOnExternalChanges);
            await returnVal.Initialize(configFileName, realTime, warnIfNotFound).ConfigureAwait(false);
            return returnVal;
        }

        private async Task Initialize(VirtualPath configFileName, IRealTimeProvider realTime, bool warnIfNotFound)
        {
            if (!_inMemoryOnly)
            {
                _logger.Log("Creating an ini configuration mapped to " + configFileName.FullName);
                VirtualPath defaultFileName = configFileName.Container.Combine(configFileName.NameWithoutExtension + ".default.ini");

                if (_fileSystem == null)
                {
                    // shouldn't happen
                    _logger.Log("Configuration cannot load resources, filesystem is null! " + configFileName, LogLevel.Err);
                    return;
                }

                if (!(await _fileSystem.ExistsAsync(_iniFilePath).ConfigureAwait(false)))
                {
                    if (await _fileSystem.ExistsAsync(defaultFileName).ConfigureAwait(false))
                    {
                        if (warnIfNotFound)
                        {
                            _logger.Log("Config file " + _iniFilePath + " not found! Using default file...", LogLevel.Wrn);
                        }

                        await LoadFile(defaultFileName, realTime).ConfigureAwait(false);
                        _logger.Log("Loaded " + _configValues.Count + " configuration keys from " + defaultFileName.FullName, LogLevel.Vrb);
                        await WriteFile(_fileSystem, _iniFilePath).ConfigureAwait(false);
                        _logger.Log("Copied default configuration " + defaultFileName.FullName + " to " + _iniFilePath.FullName, LogLevel.Vrb);
                    }
                    else
                    {
                        if (warnIfNotFound)
                        {
                            _logger.Log("Config file " + _iniFilePath.FullName + " not found! Creating new one...", LogLevel.Wrn);
                        }

                        await WriteFile(_fileSystem, _iniFilePath).ConfigureAwait(false);
                    }
                }
                else
                {
                    _logger.Log("Loading configuration from " + configFileName.FullName, LogLevel.Std);
                    await LoadFile(_iniFilePath, realTime).ConfigureAwait(false);
                    _logger.Log("Loaded " + _configValues.Count + " configuration keys from " + configFileName.FullName, LogLevel.Vrb);
                }

                if (_trackChanges && _fileChangeWatcher == null)
                {
                    _fileChangeWatcher = await _fileSystem.CreateDirectoryWatcher(configFileName.Container, configFileName.Name, false).ConfigureAwait(false);
                    _fileChangeWatcher.ChangedEvent.Subscribe(OnIniFileChangedExternally);
                }
            }
            else
            {
                _logger.Log("Creating an in-memory ini configuration only", LogLevel.Vrb);
                _fileChangeWatcher = null;
            }
        }

        /// <summary>
        /// Populates this configuration with data from an .ini file, which is read in the form of a raw data stream.
        /// This method is only legal if no file name was passed to the constructor of this class.
        /// </summary>
        /// <param name="inputStream">The stream to load the config file from</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public Task<bool> LoadStream(Stream inputStream, IRealTimeProvider realTime)
        {
            if (!_inMemoryOnly)
            {
                throw new InvalidOperationException("Cannot manually load an .ini file from a stream when the configuration is already bound to an existing file");
            }

            return LoadStreamInternal(inputStream, realTime);
        }

        public async Task<bool> WriteToStream(Stream outputStream)
        {
            if (!_inMemoryOnly)
            {
                throw new InvalidOperationException("Cannot manually write an .ini file to a stream when the configuration is already bound to an existing file");
            }

            int hlock = await _lock.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                return WriteToStreamNonLocking(outputStream);
            }
            finally
            {
                _lock.ExitReadLock(hlock);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    _fileChangeWatcher?.Dispose();
                    _fileChangeWatcher = null;
                }
            }
            finally
            {
                // The abstract configuration will handle the work of flushing any pending changes to the underlying file
                base.Dispose(disposing);
            }
        }

        private async Task OnIniFileChangedExternally(object source, FileSystemChangedEventArgs args, IRealTimeProvider realTime)
        {
            if (args.ChangeType == FileSystemChangeType.FileChanged)
            {
                // Someone else updated the configuration file. Reload from disk
                _logger.Log("Detected external changes to file " + _iniFilePath.FullName + "; reloading...");
                try
                {
                    await LoadFile(_iniFilePath, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // have to suppress all exceptions because this is an async void method
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }

        private async Task<bool> LoadStreamInternal(Stream inputStream, IRealTimeProvider realTime)
        {
            int hlock = await _lock.EnterWriteLockAsync().ConfigureAwait(false);
            try
            {
                // Clear all existing values first
                _configValues.Clear();
                _originalFileContents.Clear();

                ISet<ConfigAnnotation> currentAnnotations = new HashSet<ConfigAnnotation>();
                TypeAnnotation currentTypeAnnotation = null;
                bool readingAnnotations = false;

                using (StreamReader fileReader = new StreamReader(inputStream))
                {
                    while (!fileReader.EndOfStream)
                    {
                        string nextLine = fileReader.ReadLine().Trim();
                        _originalFileContents.Add(nextLine);

                        if (nextLine == null || string.IsNullOrWhiteSpace(nextLine) || nextLine.StartsWith("#") || nextLine.StartsWith(";") || nextLine.StartsWith("//"))
                        {
                            // Ignore comments
                            continue;
                        }

                        if (nextLine.StartsWith("["))
                        {
                            Match parseMatch = ANNOTATION_PARSE_REGEX.Match(nextLine);
                            if (parseMatch.Success && parseMatch.Groups[1].Success)
                            {
                                // It's an annotation line like [TagName|Value]
                                
                                if (!readingAnnotations)
                                {
                                    // Clear annotations from the previous config key if this is a new block
                                    currentAnnotations.Clear();
                                    readingAnnotations = true;
                                }

                                string typeName = parseMatch.Groups[1].Value;
                                ConfigAnnotation newAnnotation = null;
                                bool isTypeAnnotation = false;

                                // Parse annotations and keep a running tally
                                switch (typeName)
                                {
                                    // Treat datatype as a first-class value with special handling
                                    case "Type":
                                        if (parseMatch.Groups[2].Success)
                                        {
                                            isTypeAnnotation = true;
                                            currentTypeAnnotation = new TypeAnnotation();
                                            if (!currentTypeAnnotation.ParseValue(parseMatch.Groups[2].Value))
                                            {
                                                currentTypeAnnotation = null;
                                            }
                                        }
                                        break;
                                    case "Description":
                                        newAnnotation = new DescriptionAnnotation();
                                        break;
                                    case "Category":
                                        newAnnotation = new CategoryAnnotation();
                                        break;
                                    case "GUI":
                                        newAnnotation = new GUIAnnotation();
                                        break;
                                    case "Default":
                                        newAnnotation = new DefaultValueAnnotation();
                                        break;
                                }

                                if (!isTypeAnnotation)
                                {
                                    // Parse its value if present
                                    if (parseMatch.Groups[2].Success)
                                    {
                                        string rawValue = parseMatch.Groups[2].Value;
                                        if (newAnnotation != null && newAnnotation.ParseValue(rawValue))
                                        {
                                            currentAnnotations.Add(newAnnotation);
                                        }
                                    }
                                    else if (newAnnotation != null)
                                    {
                                        currentAnnotations.Add(newAnnotation);
                                    }
                                }
                            }
                            else
                            {
                                // It looks like an annotation line, but it is malformed
                                if (_iniFilePath == null)
                                {
                                    _logger.Log("Badly formatted line in .ini configuration found (line " + _originalFileContents.Count + "): " + nextLine, LogLevel.Wrn);
                                }
                                else
                                {
                                    _logger.Log("Badly formatted line in .ini configuration found (" + _iniFilePath.Name + " line " + _originalFileContents.Count + "): " + nextLine, LogLevel.Wrn);
                                }
                            }
                        }
                        else if (nextLine.Contains("="))
                        {
                            // It's a key/value line in the format key=value or key&variantkey:variant=value ////r
                            readingAnnotations = false;

                            // Parse name, variants, and value
                            string configKeyWithoutVariants;
                            IDictionary<string, string> variants;
                            string configValue;
                            if (!TryParseConfigLine(nextLine, out configKeyWithoutVariants, out configValue, out variants))
                            {
                                throw new FormatException("Failed to parse the configuration line \"" + nextLine + "\"");
                            }

                            // If this is a variant, make sure that a no-variant value has not already been specified
                            if (variants != null && 
                                variants.Count > 0 &&
                                _configValues.ContainsKey(configKeyWithoutVariants) &&
                                _configValues[configKeyWithoutVariants].DefaultValue != null)
                            {
                                throw new FormatException("The non-variant configuration value \"" + configKeyWithoutVariants + "\" must come after all of its variants");
                            }

                            // Now, see if there's already a key for this value.
                            // If so, we can update its existing value
                            RawConfigValue configVariantCollection;
                            if (_configValues.TryGetValue(configKeyWithoutVariants, out configVariantCollection))
                            {
                                configVariantCollection.SetValue(configValue, variants);
                            }
                            else
                            {
                                if (currentTypeAnnotation == null)
                                {
                                    configVariantCollection = new RawConfigValue(configKeyWithoutVariants, null, InferTypeFromConfigValue(configValue));
                                }
                                else
                                {
                                    configVariantCollection = new RawConfigValue(configKeyWithoutVariants, null, currentTypeAnnotation.ValueType);
                                }

                                foreach (ConfigAnnotation annotation in currentAnnotations)
                                {
                                    configVariantCollection.Annotations.Add(annotation);
                                }

                                configVariantCollection.SetValue(configValue, variants);
                                _configValues.Add(configKeyWithoutVariants, configVariantCollection);
                            }

                            // Emit events for config values changing, in case we are reloading a config file that has changed externally
                            ConfigValueChangedEvent.FireInBackground(this, new ConfigValueChangedEventArgs<string>(configKeyWithoutVariants, null), _logger, realTime);
                        }
                        else
                        {
                            // It looks like a key=value line, but it is malformed
                            if (_iniFilePath == null)
                            {
                                _logger.Log("Badly formatted line in .ini configuration found (line " + _originalFileContents.Count + "): " + nextLine, LogLevel.Wrn);
                            }
                            else
                            {
                                _logger.Log("Badly formatted line in .ini configuration found (" + _iniFilePath.Name + " line " + _originalFileContents.Count + "): " + nextLine, LogLevel.Wrn);
                            }
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock(hlock);
            }

            return true;
        }

        private async Task<bool> LoadFile(VirtualPath inputFileName, IRealTimeProvider realTime)
        {
            if (_inMemoryOnly || inputFileName == null || _fileSystem == null || !(await _fileSystem.ExistsAsync(inputFileName).ConfigureAwait(false)))
            {
                return false;
            }

            using (Stream readStream = await _fileSystem.OpenStreamAsync(inputFileName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
            {
                return await LoadStreamInternal(readStream, realTime).ConfigureAwait(false);
            }
        }

        private static bool TryParseConfigLine(string rawLine, out string key, out string value, out IDictionary<string, string> variants)
        {
            key = null;
            value = null;
            variants = null;

            int equals = rawLine.IndexOf('=');
            if (equals <= 0)
            {
                return false;
            }

            value = rawLine.Substring(equals + 1);
            int ampersand = rawLine.IndexOf('&', 0, equals);

            if (ampersand > 0)
            {
                // Variants exist. Parse them
                key = rawLine.Substring(0, ampersand);
                string variantsString = rawLine.Substring(ampersand + 1, equals - ampersand - 1);
                variants = SmallDictionaryGrammar.Dictionary.Parse(variantsString);
            }
            else
            {
                key = rawLine.Substring(0, equals);
                variants = null;
            }

            return true;
        }

        private static ConfigValueType InferTypeFromConfigValue(string value)
        {
            int i;
            float f;
            bool b;
            TimeSpan t;
            if (int.TryParse(value, out i))
            {
                return ConfigValueType.Int;
            }
            else if (float.TryParse(value, out f))
            {
                return ConfigValueType.Float;
            }
            else if (bool.TryParse(value.ToLowerInvariant(), out b))
            {
                return ConfigValueType.Bool;
            }
            else if (TimeSpanExtensions.TryParseTimeSpan(value, out t))
            {
                return ConfigValueType.TimeSpan;
            }
            else if (value.Contains(","))
            {
                return ConfigValueType.StringList;
            }
            // TODO: Add other parsers here (binary, etc.)
            else
            {
                return ConfigValueType.String;
            }
        }

        private static void WriteSingleValueToFile(StreamWriter fileWriter, string key, Dictionary<string, RawConfigValue> allValues)
        {
            RawConfigValue valueCollection;
            if (!allValues.TryGetValue(key, out valueCollection))
            {
                throw new KeyNotFoundException("Config key \"" + key + "\" missing from value set");
            }

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                // Look for all variants of this value first
                // This is needed so that variants which are written will be done in the right order,
                // and with annotations preserved
                if (valueCollection.VariantValues != null && valueCollection.VariantValues.Count > 0)
                {
                    // Write all annotations
                    foreach (ConfigAnnotation annotation in valueCollection.Annotations)
                    {
                        fileWriter.WriteLine(annotation.ToString());
                    }

                    TypeAnnotation typeAnnotation = new TypeAnnotation(valueCollection.ValueType);
                    fileWriter.WriteLine(typeAnnotation.ToString());

                    // Write all variant values of the key
                    foreach (KeyValuePair<VariantConfig, string> variantConfigValue in valueCollection.VariantValues)
                    {
                        pooledSb.Builder.Clear();
                        pooledSb.Builder.Append(key);
                        foreach (var variantKvp in variantConfigValue.Key.Variants)
                        {
                            pooledSb.Builder.Append("&");
                            pooledSb.Builder.Append(variantKvp.Key);
                            pooledSb.Builder.Append(":");
                            pooledSb.Builder.Append(variantKvp.Value);
                        }

                        pooledSb.Builder.Append("=");
                        pooledSb.Builder.Append(variantConfigValue.Value);
                        fileWriter.WriteLine(pooledSb.Builder.ToString());
                    }

                    // And then write the base value
                    pooledSb.Builder.Clear();
                    pooledSb.Builder.Append(key);
                    pooledSb.Builder.Append("=");
                    pooledSb.Builder.Append(valueCollection.DefaultValue ?? string.Empty);
                    fileWriter.WriteLine(pooledSb.Builder.ToString());
                }
                else
                {
                    RawConfigValue value = allValues[key];
                    // Write all annotations for the value
                    foreach (ConfigAnnotation annotation in value.Annotations)
                    {
                        fileWriter.WriteLine(annotation.ToString());
                    }

                    // And now its type
                    TypeAnnotation typeAnnotation = new TypeAnnotation(value.ValueType);
                    fileWriter.WriteLine(typeAnnotation.ToString());

                    // Now write the value itself
                    pooledSb.Builder.Clear();
                    pooledSb.Builder.Append(key);
                    pooledSb.Builder.Append("=");
                    pooledSb.Builder.Append(value.DefaultValue ?? string.Empty);
                    fileWriter.WriteLine(pooledSb.Builder.ToString());
                }
            }
        }

        private bool WriteToStreamNonLocking(Stream outputStream)
        {
            // Is there actually anything to write?
            if (_configValues.Count == 0)
            {
                return false;
            }

            HashSet<string> keysNotWritten = new HashSet<string>(_configValues.Keys);

            using (StreamWriter fileWriter = new StreamWriter(outputStream))
            {
                foreach (string originalLine in _originalFileContents)
                {
                    // Copy comments and whitespace straight across
                    if (string.IsNullOrWhiteSpace(originalLine) ||
                        originalLine.StartsWith("#"))
                    {
                        fileWriter.WriteLine(originalLine);
                    }
                    else if (originalLine.StartsWith("["))
                    {
                        // Don't preserve the annotations directly; we'll recreate them using WriteSingleValue
                    }
                    else
                    {
                        string configKeyWithoutVariants;
                        string configValue;
                        IDictionary<string, string> variants;
                        if (TryParseConfigLine(originalLine, out configKeyWithoutVariants, out configValue, out variants))
                        {
                            if (_configValues.ContainsKey(configKeyWithoutVariants) && keysNotWritten.Contains(configKeyWithoutVariants))
                            {
                                WriteSingleValueToFile(fileWriter, configKeyWithoutVariants, _configValues);
                                keysNotWritten.Remove(configKeyWithoutVariants);
                            }
                        }
                    }
                }

                // If we added new keys that didn't exist in the original file, append those to the end
                foreach (string notWrittenKey in keysNotWritten)
                {
                    WriteSingleValueToFile(fileWriter, notWrittenKey, _configValues);
                }
            }

            return true;
        }

        protected override Task CommitChanges(IRealTimeProvider realTime)
        {
            return WriteFile(_fileSystem, _iniFilePath);
        }

        private async Task<bool> WriteFile(IFileSystem fileSystem, VirtualPath outputFileName)
        {
            if (_inMemoryOnly || outputFileName == null || fileSystem == null)
            {
                return false;
            }

            // Is there actually anything to write?
            if (_configValues.Count == 0)
            {
                return false;
            }

            int hlock = await _lock.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                // Reset the file system watcher so we don't create a feedback loop of write -> change detected -> refresh file
                if (_trackChanges && _fileChangeWatcher != null)
                {
                    _fileChangeWatcher.ChangedEvent.Unsubscribe(OnIniFileChangedExternally);
                    _fileChangeWatcher.Dispose();
                    _fileChangeWatcher = null;
                }
                
                using (Stream writeStream = await fileSystem.OpenStreamAsync(outputFileName, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
                {
                    WriteToStreamNonLocking(writeStream);
                }

                if (_trackChanges)
                {
                    _fileChangeWatcher = await _fileSystem.CreateDirectoryWatcher(_iniFilePath.Container, _iniFilePath.Name, false).ConfigureAwait(false);
                    _fileChangeWatcher.ChangedEvent.Subscribe(OnIniFileChangedExternally);
                }
            }
            finally
            {
                _lock.ExitReadLock(hlock);
            }

            return true;
        }

        /// <summary>
        /// A small parsing grammar for parsing inline dictionaries
        /// </summary>
        private class SmallDictionaryGrammar
        {
            private static SmallDictionary<K, V> BuildDictionary<K, V>(IEnumerable<KeyValuePair<K, V>> values)
            {
                SmallDictionary<K, V> returnVal = new SmallDictionary<K, V>();
                foreach (var kvp in values)
                {
                    returnVal.Add(kvp.Key, kvp.Value);
                }

                return returnVal;
            }

            public static readonly Parser<SmallDictionary<string, string>> Dictionary =
                (
                    from key in Parse.Regex("[\\w\\-/\\\\]+")
                    from separator in Parse.Char(':')
                    from value in Parse.Regex("[\\w\\-/\\\\]+")
                    select new KeyValuePair<string, string>(key, value))
                .DelimitedBy(Parse.Char('&'))
                .Many()
                .Select(entries => BuildDictionary(entries.First())).End();
        }
    }
}
