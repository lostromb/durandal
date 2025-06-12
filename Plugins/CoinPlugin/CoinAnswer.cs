
namespace Durandal.Plugins
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class CoinAnswer : DurandalPlugin
    {
        private Random _rand = new Random();

        public CoinAnswer() : base("coin") { }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode flipNode = returnVal.CreateNode(Flip);
            IConversationNode rollNode = returnVal.CreateNode(Roll);

            flipNode.CreateCommonEdge("repeat", flipNode);
            rollNode.CreateCommonEdge("repeat", rollNode);

            returnVal.AddStartState("flip", flipNode);
            returnVal.AddStartState("roll_dice", rollNode);
            returnVal.AddStartState("pick_number", PickNumber);

            return returnVal;
        }

        public async Task<PluginResult> Flip(QueryWithContext queryWithContext, IPluginServices services)
        {
            bool isHeads = _rand.NextDouble() < 0.5;
            ILGPattern pattern = null;
            if (isHeads)
            {
                pattern = services.LanguageGenerator.GetPattern("Heads", queryWithContext.ClientContext, services.Logger);
            }
            else
            {
                pattern = services.LanguageGenerator.GetPattern("Tails", queryWithContext.ClientContext, services.Logger);
            }

            RenderedLG lg = await pattern.Render().ConfigureAwait(false);

            MessageView view = new MessageView()
            {
                Content = lg.Text,
                Image = lg.ExtraFields["Image"],
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            PluginResult returnVal = new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResponseHtml = view.Render()
                };

            returnVal = await pattern.ApplyToDialogResult(returnVal).ConfigureAwait(false);
            return returnVal;
        }

        private static Regex dndParser = new Regex("(([0-9]+) ?)?d ?([0-9]+)");

        public async Task<PluginResult> Roll(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            int numDice = 2;
            int numOfSides = 6;
            string dndNotationSlot = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "dnd_dice");
            if (!string.IsNullOrEmpty(dndNotationSlot) && dndParser.IsMatch(dndNotationSlot))
            {
                // Parse D&D notation dice roll
                Match m = dndParser.Match(dndNotationSlot);
                if (m.Groups[1].Success)
                {
                    // It's a full "2d20"
                    numDice = int.Parse(m.Groups[2].Value);
                    numOfSides = int.Parse(m.Groups[3].Value);
                }
                else
                {
                    // It's just "d6"
                    numDice = 1;
                    numOfSides = int.Parse(m.Groups[3].Value);
                }
            }
            else
            {
                // Parse it from the numerical value of the word, and assume the dice are 6-sided
                SlotValue numberSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "number");
                if (numberSlot != null)
                {
                    decimal? ordinal = numberSlot.GetNumber();
                    if (ordinal != null)
                    {
                        numDice = (int)Math.Floor(ordinal.Value);
                    }
                }
            }

            services.Logger.Log("Dice arguments parsed as numDice=" + numDice + ", numOfSides=" + numOfSides);

            int[] dice = new int[numDice];
            int sum = 0;
            for (int d = 0; d < numDice; d++)
            {
                dice[d] = _rand.Next(1, numOfSides + 1);
                sum += dice[d];
            }

            MessageView html = new MessageView()
            {
                Content = sum.ToString(),
                Subscript = "Roll of " + numDice + " " + numOfSides + "-sided dice (" + string.Join(" + ", dice) + ")",
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return new PluginResult(Result.Success)
                {
                    ResponseText = sum.ToString(),
                    ResponseSsml = sum.ToString(),
                    ResponseHtml = html.Render(),
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively
                };
        }

        public async Task<PluginResult> PickNumber(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            SlotValue minSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "min");
            SlotValue maxSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "max");

            if (minSlot == null ||
                maxSlot == null ||
                !minSlot.GetNumber().HasValue ||
                !maxSlot.GetNumber().HasValue)
            {
                return new PluginResult(Result.Skip);
            }

            int min = (int)Math.Floor(minSlot.GetNumber().Value);
            int max = (int)Math.Floor(maxSlot.GetNumber().Value);
            
            if (max < min)
            {
                int swap = min;
                min = max;
                max = swap;
            }

            int picked = _rand.Next(min, max + 1);
            
            return new PluginResult(Result.Success)
                {
                    ResponseText = picked.ToString(),
                    ResponseSsml = picked.ToString(),
                    ResponseHtml = new MessageView()
                    {
                        Content = picked.ToString(),
                        Subscript = "Random number between " + min + " and " + max,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
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
                    InternalName = "coin",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Coin Toss",
                    ShortDescription = "Heads or tails?",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Flip a coin");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Heads or tails?");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Roll some dice");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Roll six dice");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Pick a number between 1 and 10");

                return returnVal;
            }
        }
    }
}
