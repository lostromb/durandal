using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.API;
using Durandal.API.Data;
using Durandal.Common.Utils;

namespace Durandal.Answers.ListAnswer
{
    using Durandal.API.Utils;
    using Durandal.Common.Utils.Tasks;
    using System.Threading.Tasks;

    public class ListAnswer : DurandalPlugin
    {
        public ListAnswer()
            : base("list")
        {
        }

        protected override ConversationTree BuildConversationTree(ConversationTree tree)
        {
            ConversationNode promptTitleNode = tree.CreateNode(PromptTitle, "PromptTitle");
            ConversationNode showNewListNode = tree.CreateNode(ShowNewList, "ShowNewList");
            ConversationNode appendingThingsNode = tree.CreateNode(AppendingThings, "AppendingThings");
            ConversationNode reviewListNode = tree.CreateNode(ReviewList, "ReviewList");
            ConversationNode cancelListNode = tree.CreateNode(CancelList, "CancelList");

            tree.AddStartState("create_list", promptTitleNode);

            promptTitleNode.CreateNormalEdge("name_list", showNewListNode);
            promptTitleNode.CreateCommonEdge("deny", cancelListNode);
            promptTitleNode.CreateNormalEdge("button_cancel", cancelListNode);

            showNewListNode.CreateNormalEdge("append", appendingThingsNode);
            showNewListNode.CreateCommonEdge("side_speech", appendingThingsNode);
            showNewListNode.CreateNormalEdge("item_input", appendingThingsNode);
            showNewListNode.CreateCommonEdge("deny", cancelListNode);
            showNewListNode.CreateNormalEdge("button_cancel", cancelListNode);

            appendingThingsNode.CreateNormalEdge("append", appendingThingsNode);
            appendingThingsNode.CreateNormalEdge("item_input", appendingThingsNode);
            appendingThingsNode.CreateCommonEdge("side_speech", appendingThingsNode);
            appendingThingsNode.CreateNormalEdge("finish_appending", reviewListNode);
            appendingThingsNode.CreateCommonEdge("deny", reviewListNode);
            appendingThingsNode.CreateCommonEdge("contempt", reviewListNode);

            return tree;
        }

        private ListEntity TryGetDialogState(IDataStore store)
        {
            ListEntity state;
            if (!store.TryGetObject<ListEntity>("state", out state))
            {
                // Retrieval failed; create a new state
                return new ListEntity();
            }

            return state;
        }

        private void SaveSessionState(IDataStore store, ListEntity state)
        {
            store.Put("state", state);
        }

        private bool HasTitle(QueryWithContext input, ListEntity state)
        {
            return !string.IsNullOrEmpty(GetTitle(input, state));
        }

        private string GetTitle(QueryWithContext input, ListEntity state)
        {
            string returnVal = null;
            if (!string.IsNullOrEmpty(state.Title))
                returnVal = state.Title;
            if (returnVal == null)
                returnVal = DialogHelpers.TryGetSlotValue(input.Understanding, "list_title");
            return returnVal;
        }

        private async Task<DialogResult> ShowNewList(QueryWithContext queryWithContext, PluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            ListEntity state = this.TryGetDialogState(services.SessionStore);
            // Extract the title from the slots
            string title = GetTitle(queryWithContext, state);
            if (string.IsNullOrEmpty(title))
            {
                return new DialogResult(Result.Failure)
                    {
                        ErrorMessage = "Null title"
                    };
            }

            state.Title = title;

            SaveSessionState(services.SessionStore, state);

            ILGPattern responsePattern = services.LanguageGenerator.GetPattern("PromptListContent",
                                                                             queryWithContext.ClientContext,
                                                                             services.Logger);
            DialogResult returnVal = new DialogResult(Result.Success)
            {
                ResponseHtml = GenerateListHtml(responsePattern.RenderText(), state, queryWithContext.ClientContext, services),
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
            return responsePattern.ApplyToDialogResult(returnVal);
        }

        private async Task<DialogResult> AppendingThings(QueryWithContext queryWithContext, PluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            ListEntity state = this.TryGetDialogState(services.SessionStore);
            
            // Extract the item from the slots
            string item = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "item");
            if (!string.IsNullOrEmpty(item))
            {
                state.Entries.Add(item);
            }

            SaveSessionState(services.SessionStore, state);

            ILGPattern responsePattern = services.LanguageGenerator.GetPattern("PromptForMore", queryWithContext.ClientContext, services.Logger);
            DialogResult returnVal = new DialogResult(Result.Success)
            {
                ResponseHtml = GenerateListHtml(responsePattern.RenderText(), state, queryWithContext.ClientContext, services),
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
            return responsePattern.ApplyToDialogResult(returnVal);
        }

        private async Task<DialogResult> ReviewList(QueryWithContext queryWithContext, PluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            ListEntity state = this.TryGetDialogState(services.SessionStore);

            SaveSessionState(services.SessionStore, state);

            ILGPattern responsePattern = services.LanguageGenerator.GetPattern("ShowFinishedList", queryWithContext.ClientContext, services.Logger);
            DialogResult returnVal = new DialogResult(Result.Success)
            {
                ResponseHtml = GenerateListHtml(responsePattern.RenderText(), state, queryWithContext.ClientContext, services),
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
            return responsePattern.ApplyToDialogResult(returnVal);
        }

        private async Task<DialogResult> PromptTitle(QueryWithContext queryWithContext, PluginServices services)
        {
            ListEntity state = this.TryGetDialogState(services.SessionStore);

            if (this.HasTitle(queryWithContext, state))
            {
                // Skip ahead to ShowNewList if we already have a title
                DialogResult result = await this.ShowNewList(queryWithContext, services);
                result.ResultConversationNode = "ShowNewList";
                return result;
            }

            SaveSessionState(services.SessionStore, state);

            DialogResult returnVal = new DialogResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic
                };
            return services.LanguageGenerator.GetPattern("PromptListTitle", queryWithContext.ClientContext, services.Logger)
                .ApplyToDialogResult(returnVal);
        }

        private async Task<DialogResult> CancelList(QueryWithContext queryWithContext, PluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            services.SessionStore.ClearAll();

            DialogResult returnVal = new DialogResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.None
            };
            return services.LanguageGenerator.GetPattern("ListDeleted", queryWithContext.ClientContext, services.Logger)
                .ApplyToDialogResult(returnVal);
        }

        private string GenerateListHtml(string topText, ListEntity note, ClientContext context, PluginServices services)
        {
            ListView view = new ListView()
            {
                pageTitle = "List",
                conversationResponse = topText,
                list = note,
                cancelLink = services.RegisterDialogActionUrl(new DialogAction() { Domain = "note", Intent = "button_cancel" }, context.ClientId),
                doneLink = services.RegisterDialogActionUrl(new DialogAction() { Domain = "note", Intent = "button_ok" }, context.ClientId)
            };

            return view.Render();
        }
    }
}
