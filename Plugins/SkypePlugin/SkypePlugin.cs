
namespace Durandal.Plugins.Skype
{
    using Durandal.API;
        using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    public class SkypePlugin : DurandalPlugin
    {
        private IDictionary<string, string> _knownContacts;

        private string _activeCallType;
        private string _activeContactName;

        // TODO: Use the normalized slot rewriter like Winamp does it
        
        public SkypePlugin()
            : base("skype")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            // TODO: Need a way to extract Skype contacts from profile
            // and so I can get URIs for them
            _knownContacts = new Dictionary<string, string>();

            // Load the available programs from the external config file
            VirtualPath skypeContactsFile = services.PluginDataDirectory + "\\skype_contacts.txt";
            if (!(await services.FileSystem.ExistsAsync(skypeContactsFile).ConfigureAwait(false)))
            {
                return;
            }

            using (StreamReader fileIn = new StreamReader(await services.FileSystem.OpenStreamAsync(skypeContactsFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false)))
            {
                while (!fileIn.EndOfStream)
                {
                    string[] parts = fileIn.ReadLine().Split('\t');
                    if (parts.Length == 2)
                    {
                        _knownContacts.Add(parts[0], parts[1]);
                    }
                }

                fileIn.Dispose();
            }
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode makeCallNode = returnVal.CreateNode(this.MakeCall);
            IConversationNode confirmCallNode = returnVal.CreateNode(this.ConfirmMakeCall);
            IConversationNode denyCallNode = returnVal.CreateNode(this.ConfirmMakeCall);

            makeCallNode.CreateCommonEdge("confirm", confirmCallNode);
            makeCallNode.CreateCommonEdge("deny", denyCallNode);

            returnVal.AddStartState("make_call", makeCallNode);

            return returnVal;
        }

        private void MakeCall(string userName, bool video, ILogger logger)
        {
            string skypeURI = string.Format("skype:{0}?call{1}", userName, "&video=false");
            //ShellExecute(skypeURI, "", logger);
        }

        //public static void ShellExecute(string programName, string args, ILogger logger)
        //{
        //    try
        //    {
        //        Process.Start(programName, args);
        //    }
        //    catch (Exception exp)
        //    {
        //        logger.Log("Exception in ShellExecute: " + exp.Message, LogLevel.Err);
        //    }
        //}

        private async Task<string> TryFindContact(LexicalString contactName, LanguageCode locale, IPluginServices services)
        {
            IList<NamedEntity<string>> contactsList = new List<NamedEntity<string>>();
            foreach (string contact in _knownContacts.Keys)
            {
                contactsList.Add(new NamedEntity<string>(contact, new List<LexicalString>() { new LexicalString(contact) }));
            }

            IList<Hypothesis<string>> resolvedContacts = await services.EntityResolver.ResolveEntity(contactName, contactsList, locale, services.Logger).ConfigureAwait(false);
            if (resolvedContacts.Count == 0 || resolvedContacts[0].Conf < 0.75)
            {
                return null;
            }

            return resolvedContacts[0].Value;
        }

        public async Task<PluginResult> MakeCall(QueryWithContext queryWithContext, IPluginServices services)
        {
            LexicalString queryContactName = DialogHelpers.TryGetLexicalSlotValue(queryWithContext.Understanding, "contactname");
            _activeCallType = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "calltype");

            if (queryContactName == null ||
                string.IsNullOrWhiteSpace(queryContactName.WrittenForm))
            {
                services.Logger.Log("no contact name extracted", LogLevel.Wrn);
                return new PluginResult(Result.Skip);
            }

            _activeContactName = await TryFindContact(queryContactName, queryWithContext.ClientContext.Locale, services).ConfigureAwait(false);

            if (this._activeContactName == null || !this._knownContacts.ContainsKey(this._activeContactName))
            {
                services.Logger.Log("no contact found similar to " + this._activeContactName, LogLevel.Err);

                ILGPattern lg = services.LanguageGenerator.GetPattern("CannotFindContact", queryWithContext.ClientContext, services.Logger);
                return await lg.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = (await lg.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
            }

            ILGPattern pattern = services.LanguageGenerator.GetPattern("ConfirmCall", queryWithContext.ClientContext, services.Logger)
                .Sub("name", this._activeContactName);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueQuickly,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> ConfirmMakeCall(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern;

            if (queryWithContext.Understanding.Intent == "confirm")
            {
                string activeContactId = this._knownContacts[this._activeContactName];
                if (this._activeCallType.Equals("video"))
                {
                    this.MakeCall(activeContactId, true, services.Logger);
                    pattern = services.LanguageGenerator.GetPattern("StartVideoCall", queryWithContext.ClientContext, services.Logger)
                        .Sub("name", this._activeContactName);

                    return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render().ConfigureAwait(false)).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    }).ConfigureAwait(false);
                }
                else
                {
                    this.MakeCall(activeContactId, false, services.Logger);
                    pattern = services.LanguageGenerator.GetPattern("StartAudioCall", queryWithContext.ClientContext, services.Logger)
                        .Sub("name", this._activeContactName);

                    return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                    {
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render().ConfigureAwait(false)).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    }).ConfigureAwait(false);
                }
            }

            pattern = services.LanguageGenerator.GetPattern("CallCanceled", queryWithContext.ClientContext);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
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
                    InternalName = "Skype",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Skype",
                    ShortDescription = "Skype pun goes here",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Call David on Skype");
                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Start a video call with Ashley");

                return returnVal;
            }
        }
    }
}
