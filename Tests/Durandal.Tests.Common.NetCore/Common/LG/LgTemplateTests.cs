namespace Durandal.Tests.Common.LG
{
    using Durandal.API;
        using Durandal.Common.LG;
    using Durandal.Common.LG.Template;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Durandal.Common.NLP.Language;

    [TestClass]
    public class LgTemplateTests
    {
        private static InMemoryFileSystem _templateProvider;
        private static ILogger _logger;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _templateProvider = new InMemoryFileSystem();
            _templateProvider.AddFile(new VirtualPath("basic"), Encoding.UTF8.GetBytes(
                        "[Simple:en-US]\r\n" +
                        "Text=TextOut\r\n" +
                        "ShortText=ShortTextOut\r\n" +
                        "Spoken=SpokenOut\r\n" +
                        "[Sub:en-US]\r\n" +
                        "Text=Text{field1}\r\n" +
                        "ShortText=Short{field1}\r\n" +
                        "Spoken=Spoken{field1}\r\n" +
                        "[Sub]\r\n" +
                        "Text=Generic{field1}\r\n" +
                        "ShortText=Generic{field1}\r\n" +
                        "Spoken=Generic{field1}\r\n" +
                        "[TextOnly]\r\n" +
                        "Text=TextOnly{field1}\r\n" +
                        "[Extra:en-US]\r\n" +
                        "SuggestionText=Suggestion{field1}\r\n"));
            _templateProvider.AddFile(new VirtualPath("error"), Encoding.UTF8.GetBytes(
                        "[Sub:en-US]\r\n" +
                        "null\r\n" +
                        "7*SDfnj30==sdSD=\r\n" +
                        "garbage#48\r\n" +
                        "[Simple:en-US]\r\n" +
                        "Text=TextOut\r\n" +
                        "ShortText=ShortTextOut\r\n" +
                        "Spoken=SpokenOut\r\n"));
            
            _logger = new ConsoleLogger();
        }

        private static ClientContext GetEnglishClientContext()
        {
            return new ClientContext()
            {
                Locale = LanguageCode.EN_US,
                Capabilities = (ClientCapabilities.CanSynthesizeSpeech | ClientCapabilities.DisplayUnlimitedText | ClientCapabilities.DoNotRenderTextAsHtml)
            };
        }

        private static ClientContext GetUnknownClientContext()
        {
            return new ClientContext()
            {
                Locale = LanguageCode.RUSSIAN.InCountry(RegionCode.RUSSIA)
            };
        }

        private static List<VirtualPath> GetFakeFileList(string fileName)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();
            returnVal.Add(new VirtualPath(fileName));
            return returnVal;
        }
        
        [TestMethod]
        public async Task TestLgTemplatesParsingSuccess()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Simple", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            Assert.AreEqual("TextOut", (await pattern.Render()).Text);
            Assert.AreEqual("ShortTextOut", (await pattern.Render()).ShortText);
            Assert.AreEqual("SpokenOut", (await pattern.Render()).Spoken);
        }

        [TestMethod]
        public async Task TestLgTemplatesParsingErrors()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("error"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Simple", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            Assert.AreEqual("TextOut", (await pattern.Render()).Text);
            Assert.AreEqual("ShortTextOut", (await pattern.Render()).ShortText);
            Assert.AreEqual("SpokenOut", (await pattern.Render()).Spoken);
            ILGPattern nonexistent = template.GetPattern("Sub", GetEnglishClientContext());
            Assert.IsTrue(string.IsNullOrEmpty((await nonexistent.Render()).Text));
        }

        [TestMethod]
        public async Task TestLgTemplatesNoSubstitution()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Simple", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            Assert.AreEqual("TextOut", (await pattern.Render()).Text);
            Assert.AreEqual("ShortTextOut", (await pattern.Render()).ShortText);
            Assert.AreEqual("SpokenOut", (await pattern.Render()).Spoken);
        }

        [TestMethod]
        public async Task TestLgTemplatesBasicSubstitution()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Sub", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", "Test");
            Assert.AreEqual("TextTest", (await pattern.Render()).Text);
            Assert.AreEqual("ShortTest", (await pattern.Render()).ShortText);
            Assert.AreEqual("SpokenTest", (await pattern.Render()).Spoken);
        }

        [TestMethod]
        public async Task TestLgTemplatesApplyToDialogResult()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Sub", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", "Test");
            PluginResult result = new PluginResult(Result.Success);
            await pattern.ApplyToDialogResult(result);
            Assert.AreEqual("TextTest", result.ResponseText);
            Assert.AreEqual("SpokenTest", result.ResponseSsml);
        }

        [TestMethod]
        public async Task TestLgTemplatesBasicSubstitutionWithFallback()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("TextOnly", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", "Test");
            Assert.AreEqual("TextOnlyTest", (await pattern.Render()).Text);
            Assert.AreEqual("TextOnlyTest", (await pattern.Render()).ShortText);
            Assert.AreEqual("TextOnlyTest", (await pattern.Render()).Spoken);
            PluginResult result = new PluginResult(Result.Success);
            await pattern.ApplyToDialogResult(result);
            Assert.AreEqual("TextOnlyTest", result.ResponseText);
            Assert.AreEqual("TextOnlyTest", result.ResponseSsml);
        }

        [TestMethod]
        public async Task TestLgTemplatesExtraFields()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Extra", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", "Test");
            RenderedLG rendered = await pattern.Render();
            Assert.AreEqual("SuggestionTest", rendered.ExtraFields["SuggestionText"]);
        }

        [TestMethod]
        public async Task TestLgTemplatesLocaleFallback()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Sub", GetUnknownClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", "Test");
            Assert.AreEqual("GenericTest", (await pattern.Render()).Text);
            Assert.AreEqual("GenericTest", (await pattern.Render()).ShortText);
            Assert.AreEqual("GenericTest", (await pattern.Render()).Spoken);
        }

        [TestMethod]
        public async Task TestLgTemplatesObjectSubstitution()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            ILGPattern pattern = template.GetPattern("Sub", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", 15);
            Assert.AreEqual("Text15", (await pattern.Render()).Text);
            Assert.AreEqual("Short15", (await pattern.Render()).ShortText);
            Assert.AreEqual("Spoken15", (await pattern.Render()).Spoken);
        }

        [TestMethod]
        public async Task TestLgTemplatesCustomLogic()
        {
            TemplateBasedLGEngine template = await TemplateBasedLGEngine.Create(GetFakeFileList("basic"), _templateProvider, _logger);
            Assert.IsNotNull(template);
            template.RegisterCustomCode("Custom", CustomLogic, LanguageCode.EN_US);
            ILGPattern pattern = template.GetPattern("Custom", GetEnglishClientContext());
            Assert.IsNotNull(pattern);
            pattern = pattern.Sub("field1", "Hello");
            PluginResult output = await pattern.ApplyToDialogResult(new PluginResult(Result.Success));
            Assert.IsNotNull(output);
            Assert.AreEqual("Hello", output.ResponseText);
        }

        private RenderedLG CustomLogic(
            IDictionary<string, object> substitutions,
            ILogger logger,
            ClientContext clientContext)
        {
            RenderedLG returnVal = new RenderedLG();
            Assert.IsTrue(clientContext.Locale.ToBcp47Alpha2String().Equals("en-US"));
            Assert.IsTrue(substitutions.ContainsKey("field1"));
            returnVal.Text = substitutions["field1"].ToString();
            
            return returnVal;
        }
    }
}
