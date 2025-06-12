using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.CommonViews;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.NLP.Search;
using Durandal.Common.NLP.Language.English;
using Newtonsoft.Json;
using Durandal.Common.File;
using Durandal.Common.Statistics;
using Durandal.Common.NLP.Language;

namespace Durandal.Plugins.Color
{
    public class ColorPlugin : DurandalPlugin
    {
        private static IList<ColorComparison> _comparisonColors;
        private IList<ColorInformation> _loadedColors;
        private IDictionary<LanguageCode, StringFeatureSearchIndex<ColorInformation>> _colorNameIndex;
        private IRandom _random;

        public ColorPlugin() : this(new FastRandom()) { }
        
        public ColorPlugin(IRandom random) : base("color")
        {
            _random = random;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            returnVal.AddStartState("get_color_suggestion", GetColorSuggestion);
            returnVal.AddStartState("get_color_info", GetColorInformation);
            returnVal.AddStartState("favorite_color", GetFavoriteColor);
            return returnVal;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _colorNameIndex = new Dictionary<LanguageCode, StringFeatureSearchIndex<ColorInformation>>();

            List<SerializedColorInformation> deserializedColors;
            VirtualPath dataFile = services.PluginDataDirectory + "\\colors.json";
            using (Stream readStream = await services.FileSystem.OpenStreamAsync(dataFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
            {
                JsonSerializer serializer = new JsonSerializer();
                using (JsonReader reader = new JsonTextReader(new StreamReader(readStream, Encoding.UTF8)))
                {
                    deserializedColors = serializer.Deserialize<List<SerializedColorInformation>>(reader);
                }
            }

            _loadedColors = new List<ColorInformation>();
            foreach (var deserializedColor in deserializedColors)
            {
                _loadedColors.Add(new ColorInformation(deserializedColor));
            }

            foreach (ColorInformation rawColor in _loadedColors)
            {
                // Parse the data a bit better and precalculate vectors
                try
                {
                    if (string.IsNullOrEmpty(rawColor.RGB))
                    {
                        throw new InvalidDataException("Color does not contain RGB data: " + JsonConvert.SerializeObject(rawColor));
                    }

                    string[] rgbParts = rawColor.RGB.Split(',');
                    if (rgbParts.Length != 3)
                    {
                        throw new InvalidDataException("Color has invalid RGB data (expected 255,255,255): " + JsonConvert.SerializeObject(rawColor));
                    }

                    rawColor.R = int.Parse(rgbParts[0]);
                    rawColor.G = int.Parse(rgbParts[1]);
                    rawColor.B = int.Parse(rgbParts[2]);
                    rawColor.RGBVector = new Vector3f(rawColor.R, rawColor.G, rawColor.B);

                    if (string.IsNullOrEmpty(rawColor.HSL))
                    {
                        throw new InvalidDataException("Color does not contain HL data: " + JsonConvert.SerializeObject(rawColor));
                    }

                    string[] hslParts = rawColor.HSL.Split(',');
                    if (hslParts.Length != 3)
                    {
                        throw new InvalidDataException("Color has invalid HSL data (expected 360,100,100): " + JsonConvert.SerializeObject(rawColor));
                    }

                    rawColor.H = int.Parse(hslParts[0]);
                    rawColor.S = int.Parse(hslParts[1]);
                    rawColor.L = int.Parse(hslParts[2]);
                }
                catch (FormatException)
                {
                    throw new InvalidDataException("Color has invalid data: " + JsonConvert.SerializeObject(rawColor));
                }

                // Also index each color name
                foreach (LanguageCode locale in rawColor.Name.Keys)
                {
                    if (!_colorNameIndex.ContainsKey(locale))
                    {
                        _colorNameIndex[locale] = new StringFeatureSearchIndex<ColorInformation>(new EnglishNgramApproxStringFeatureExtractor(), services.Logger);
                    }

                    foreach (string name in rawColor.Name[locale])
                    {
                        _colorNameIndex[locale].Index(name, rawColor);
                    }
                }
            }

            _comparisonColors = GetComparisonColors(services.Logger);

            services.Logger.Log("Loaded " + _loadedColors.Count + " colors from database");
        }

        public override Task OnUnload(IPluginServices services)
        {
            _loadedColors = null;
            _comparisonColors = null;
            _colorNameIndex = null;
            return DurandalTaskExtensions.NoOpTask;
        }

        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            switch (queryWithContext.Understanding.Intent)
            {
                case "get_color_info":
                    ColorInformation color = TryGetColorInformation(queryWithContext, services);
                    if (color == null)
                    {
                        return new TriggerResult()
                            {
                                BoostResult = BoostingOption.Suppress
                            };
                    }

                    return new TriggerResult()
                        {
                            BoostResult = BoostingOption.NoChange
                        };

                default:
                    return await base.Trigger(queryWithContext, services).ConfigureAwait(false);
            }
        }

        public async Task<PluginResult> GetFavoriteColor(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern;

            if (_loadedColors == null || _loadedColors.Count == 0)
            {
                pattern = services.LanguageGenerator.GetPattern("NoFavoriteColor", queryWithContext.ClientContext, services.Logger, false, _random.NextInt());
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            ColorInformation favoriteColor = _loadedColors[_random.NextInt(_loadedColors.Count)];

            pattern = services.LanguageGenerator.GetPattern("MyFavoriteColor", queryWithContext.ClientContext, services.Logger, false, _random.NextInt())
                .Sub("color", favoriteColor.Name[queryWithContext.ClientContext.Locale][0]);

            BrightnessLevel brightness = favoriteColor.BrightnessLevelEnum;
            SaturationLevel saturation = favoriteColor.SaturationLevelEnum;

            ColorForDisplay primaryDisplayedColor = new ColorForDisplay()
            {
                Hex = favoriteColor.Hex,
                IsBright = favoriteColor.BrightnessLevelRaw > 0.65f,
                LocalizedName = favoriteColor.Name[queryWithContext.ClientContext.Locale][0]
            };

            ColorDisplay html = new ColorDisplay()
                {
                    MainColor = primaryDisplayedColor,
                    NearbyColors = null,
                    Text = (await pattern.Render().ConfigureAwait(false)).Text,
                };

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = html.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> GetColorSuggestion(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern;

            if (_loadedColors == null || _loadedColors.Count == 0)
            {
                pattern = services.LanguageGenerator.GetPattern("NoColorSuggestions", queryWithContext.ClientContext, services.Logger, false, _random.NextInt());
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            ColorInformation colorSuggestion = _loadedColors[_random.NextInt(_loadedColors.Count)];

            pattern = services.LanguageGenerator.GetPattern("SuggestColor", queryWithContext.ClientContext, services.Logger, false, _random.NextInt())
                .Sub("color", colorSuggestion.Name[queryWithContext.ClientContext.Locale][0]);

            BrightnessLevel brightness = colorSuggestion.BrightnessLevelEnum;
            SaturationLevel saturation = colorSuggestion.SaturationLevelEnum;

            ColorForDisplay primaryDisplayedColor = new ColorForDisplay()
            {
                Hex = colorSuggestion.Hex,
                IsBright = colorSuggestion.BrightnessLevelRaw > 0.65f,
                LocalizedName = colorSuggestion.Name[queryWithContext.ClientContext.Locale][0]
            };

            ColorDisplay html = new ColorDisplay()
            {
                MainColor = primaryDisplayedColor,
                NearbyColors = null,
                Text = (await pattern.Render().ConfigureAwait(false)).Text,
            };

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = html.Render()
            }).ConfigureAwait(false);
        }

        public async Task<PluginResult> GetColorInformation(QueryWithContext queryWithContext, IPluginServices services)
        {
            ColorInformation color = TryGetColorInformation(queryWithContext, services);
            if (color == null)
            {
                return new PluginResult(Result.Skip);
            }

            BrightnessLevel brightness = color.BrightnessLevelEnum;
            SaturationLevel saturation = color.SaturationLevelEnum;
            bool colorIsDark = color.BrightnessLevelRaw < 0.65f;
            ColorForDisplay primaryDisplayedColor = new ColorForDisplay()
            {
                Hex = color.Hex,
                IsBright = color.BrightnessLevelRaw > 0.65f,
                LocalizedName = color.Name[queryWithContext.ClientContext.Locale][0]
            };

            ILGPattern pattern;
            
            if (color.ColorOf != null &&
                color.ColorOf.ContainsKey(queryWithContext.ClientContext.Locale))
            {
                // If the matched color has ColorOf annotations, we can describe it using objects rather than relative to other colors
                IList<string> possibleObjects = color.ColorOf[queryWithContext.ClientContext.Locale];
                pattern = services.LanguageGenerator.GetPattern("DescribeColorRelative", queryWithContext.ClientContext, services.Logger, false, _random.NextInt())
                    .Sub("color", primaryDisplayedColor.LocalizedName)
                    .Sub("thing", possibleObjects[_random.NextInt(0, possibleObjects.Count)]);
            }
            else
            {
                // Find similar colors to describe this one
                IList<ColorComparison> comparisonColors = _comparisonColors;

                float closestDistance = float.MaxValue;
                ColorComparison closestColor = null;
                foreach (var value in comparisonColors)
                {
                    float dist = Math.Abs(color.RGBVector.Distance(value.SimilarityVector));
                    if (dist < closestDistance &&
                        !string.Equals(color.Hex, value.FirstComparison.Hex)) // don't allow comparison of colors with themselves
                    {
                        closestColor = value;
                        closestDistance = dist;
                    }
                }

                if (closestColor.SecondComparison == null)
                {
                    pattern = services.LanguageGenerator.GetPattern("DescribeColorPrimary", queryWithContext.ClientContext, services.Logger, false, _random.NextInt())
                    .Sub("color", primaryDisplayedColor.LocalizedName)
                    .Sub("lightness", brightness.ToString().ToUpperInvariant())
                    .Sub("saturation", saturation.ToString().ToUpperInvariant())
                    .Sub("similar_color", closestColor.FirstComparison.Name[queryWithContext.ClientContext.Locale][0]);
                }
                else
                {
                    pattern = services.LanguageGenerator.GetPattern("DescribeColorMidpoint", queryWithContext.ClientContext, services.Logger, false, _random.NextInt())
                    .Sub("color", primaryDisplayedColor.LocalizedName)
                    .Sub("lightness", brightness.ToString().ToUpperInvariant())
                    .Sub("saturation", saturation.ToString().ToUpperInvariant())
                    .Sub("similar_color", closestColor.FirstComparison.Name[queryWithContext.ClientContext.Locale][0])
                    .Sub("similar_color_2", closestColor.SecondComparison.Name[queryWithContext.ClientContext.Locale][0]);
                }
            }

            // Also find nearby colors as suggestions
            IList<ColorForDisplay> nearbyColors = GetSimilarColors(color, queryWithContext.ClientContext.Locale, services, queryWithContext.ClientContext.ClientId);

            ColorDisplay html = new ColorDisplay()
            {
                MainColor = primaryDisplayedColor,
                NearbyColors = nearbyColors,
                Text = (await pattern.Render().ConfigureAwait(false)).Text,
            };

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = html.Render()
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns up to 6 colors which are similar to the given base color
        /// </summary>
        /// <param name="baseColor"></param>
        /// <param name="locale"></param>
        /// <returns></returns>
        private IList<ColorForDisplay> GetSimilarColors(ColorInformation baseColor, LanguageCode locale, IPluginServices services, string clientId)
        {
            // FIXME This uses loads of CPU; if I were smart I would use an octree
            List<ColorInformation> nearbyColors = new List<ColorInformation>();
            foreach (ColorInformation c in _loadedColors)
            {
                if (baseColor.RGBVector.Distance(c.RGBVector) < 50 &&
                    !string.Equals(baseColor.Hex, c.Hex))
                {
                    nearbyColors.Add(c);
                }
            }

            HashSet<int> indicesUsed = new HashSet<int>();
            List<ColorForDisplay> nearbyColorsToShow = new List<ColorForDisplay>();
            while (nearbyColorsToShow.Count < Math.Min(6, nearbyColors.Count))
            {
                int index = _random.NextInt(0, nearbyColors.Count);
                if (!indicesUsed.Contains(index))
                {
                    ColorInformation z = nearbyColors[index];
                    DialogAction clickAction = new DialogAction()
                    {
                        Domain = "color",
                        Intent = "get_color_info",
                        InteractionMethod = InputMethod.Tactile,
                        Slots = new List<SlotValue>()
                        {
                            new SlotValue("color", z.Name[locale][0], SlotValueFormat.DialogActionParameter)
                        }
                    };
                    
                    nearbyColorsToShow.Add(new ColorForDisplay()
                    {
                        Hex = z.Hex,
                        LocalizedName = z.Name[locale][0],
                        IsBright = z.BrightnessLevelRaw > 0.65f,
                        ActionUrl = services.RegisterDialogActionUrl(clickAction, clientId)
                    });
                    indicesUsed.Add(index);
                }
            }

            return nearbyColorsToShow;
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            using (MemoryStream pngStream = new MemoryStream())
            {
                if (pluginDataDirectory != null && pluginDataManager != null)
                {
                    VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                    if (pluginDataManager.Exists(iconFile))
                    {
                        using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            iconStream.CopyTo(pngStream);
                        }
                    }
                }

                PluginInformation returnVal = new PluginInformation()
                {
                    InternalName = "Colors",
                    Creator = "Logan Stromberg",
                    MajorVersion = 2,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Colors",
                    ShortDescription = "What's your color?",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What color is chartreuse?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What is your favorite color?");

                return returnVal;
            }
        }

        /// <summary>
        /// Returns a hardcoded list of common colors that can be uased as a basis to describe other colors
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        private IList<ColorComparison> GetComparisonColors(ILogger logger)
        {
            IList<ColorInformation> primaryColors = new List<ColorInformation>();
            AddComparisonColor(primaryColors, "black", logger);
            AddComparisonColor(primaryColors, "white", logger);

            AddComparisonColor(primaryColors, "blue", logger);
            AddComparisonColor(primaryColors, "brown", logger);
            AddComparisonColor(primaryColors, "gray", logger);
            AddComparisonColor(primaryColors, "green", logger);
            AddComparisonColor(primaryColors, "orange", logger);
            AddComparisonColor(primaryColors, "pink", logger);
            AddComparisonColor(primaryColors, "purple", logger);
            AddComparisonColor(primaryColors, "red", logger);
            AddComparisonColor(primaryColors, "yellow", logger);

            AddComparisonColor(primaryColors, "dark blue", logger);
            AddComparisonColor(primaryColors, "dark brown", logger);
            AddComparisonColor(primaryColors, "dark gray", logger);
            AddComparisonColor(primaryColors, "dark green", logger);
            AddComparisonColor(primaryColors, "dark orange", logger);
            AddComparisonColor(primaryColors, "dark pink", logger);
            AddComparisonColor(primaryColors, "dark purple", logger);
            AddComparisonColor(primaryColors, "dark red", logger);
            AddComparisonColor(primaryColors, "dark yellow", logger);

            AddComparisonColor(primaryColors, "light blue", logger);
            AddComparisonColor(primaryColors, "light brown", logger);
            AddComparisonColor(primaryColors, "light gray", logger);
            AddComparisonColor(primaryColors, "light green", logger);
            AddComparisonColor(primaryColors, "light orange", logger);
            AddComparisonColor(primaryColors, "light pink", logger);
            AddComparisonColor(primaryColors, "light purple", logger);
            AddComparisonColor(primaryColors, "light red", logger);
            AddComparisonColor(primaryColors, "light yellow", logger);

            // Build a large comparison table containing all the primary colors as well as all midpoints between those primary colors
            IList<ColorComparison> comparisons = new List<ColorComparison>();
            for (int primaryColorIdx = 0; primaryColorIdx < primaryColors.Count; primaryColorIdx++)
            {
                ColorInformation primaryColor = primaryColors[primaryColorIdx];
                comparisons.Add(new ColorComparison(primaryColor));

                for (int secondaryColorIdx = primaryColorIdx + 1; secondaryColorIdx < primaryColors.Count; secondaryColorIdx++)
                {
                    ColorInformation secondaryColor = primaryColors[secondaryColorIdx];
                    if (primaryColor.RGBVector.Distance(secondaryColor.RGBVector) < 110) // Only compare colors that are fairly similar to each other, so we don't create a midpoint between "light pink" and "black", for example
                    {
                        // logger.Log(primaryColor.Name["en-US"][0] + " -> " + secondaryColor.Name["en-US"][0] + " = " + primaryColor.RGBVector.Distance(secondaryColor.RGBVector));
                        comparisons.Add(new ColorComparison(primaryColor, secondaryColor));
                    }
                }
            }

            return comparisons;
        }

        private void AddComparisonColor(IList<ColorInformation> comparisonColors, string colorNameEnglish, ILogger logger)
        {
            ColorInformation resolvedColor = ResolveColorName(colorNameEnglish, LanguageCode.Parse("en-US"), logger);
            if (resolvedColor == null)
            {
                logger.Log("No reference color found named \"" + colorNameEnglish + "\"", LogLevel.Err);
            }
            else
            {
                comparisonColors.Add(resolvedColor);
            }
        }

        private ColorInformation ResolveColorName(string name, LanguageCode locale, ILogger logger)
        {
            if (_colorNameIndex == null)
            {
                throw new InvalidOperationException("Color name index is null!");
            }

            if (!_colorNameIndex.ContainsKey(locale))
            {
                logger.Log("Color index does not exist for locale " + locale, LogLevel.Err);
                return null;
            }

            IList<Hypothesis<ColorInformation>> hyps = _colorNameIndex[locale].Search(name, 5);
            if (hyps == null || hyps.Count == 0)
            {
                logger.Log("No color information found for \"" + name + "\" (" + locale + ")");
                return null;
            }

            if (hyps[0].Conf < 0.85f)
            {
                logger.Log("Color hypothesis found for \"" + name + "\" (" + locale + ") had too low of confidence (" + hyps[0].Conf + ")");
                return null;
            }

            return hyps[0].Value;
        }

        private ColorInformation TryGetColorInformation(QueryWithContext query, IPluginServices services)
        {
            SlotValue colorSlot = DialogHelpers.TryGetSlot(query.Understanding, "color");
            if (colorSlot == null)
            {
                return null;
            }

            List<string> colorsToTry = new List<string>();
            colorsToTry.Add(colorSlot.Value);
            IList<string> spellSuggestions = colorSlot.GetSpellSuggestions();
            if (spellSuggestions != null)
            {
                colorsToTry.AddRange(spellSuggestions);
            }

            foreach (string colorName in colorsToTry)
            {
                services.Logger.Log("Trying color query " + colorName);
                ColorInformation hyp = ResolveColorName(colorName, query.ClientContext.Locale, services.Logger);
                
                if (hyp != null)
                {
                    services.Logger.Log("Matched color \"" + hyp.Name[query.ClientContext.Locale][0] + "\" #" + hyp.Hex);
                    return hyp;
                }
            }

            return null;
        }
    }
}
