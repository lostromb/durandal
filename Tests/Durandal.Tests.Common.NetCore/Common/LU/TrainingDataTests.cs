using Durandal.Common.Logger;
using Durandal.Common.NLP.Train;
using Durandal.Common.File;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace Durandal.Tests.Common.LU
{
    [TestClass]
    public class TrainingDataTests
    {
        private static readonly Regex UnitConvertValidator = new Regex("how many \\[target_unit\\](.+?)\\[/target_unit\\] (are )?in (a |\\[amount\\][0-9]+\\[/amount\\] )?\\[source_unit\\](.+?)\\[/source_unit\\]\\??");

        private static readonly string UnitConvertTemplate =
            "#PATTERNS#\r\n" +
            "; Unit conversion\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] are in a [source_unit]{unit_si}[/source_unit]\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] in a [source_unit]{unit_si}[/source_unit]\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] are in a [source_unit]{unit_si}[/source_unit]?\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] in a [source_unit]{unit_si}[/source_unit]?\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] are in [amount]{amounts_pl}[/amount] [source_unit]{unit_pl}[/source_unit]\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] in [amount]{amounts_pl}[/amount] [source_unit]{unit_pl}[/source_unit]\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] are in [amount]{amounts_pl}[/amount] [source_unit]{unit_pl}[/source_unit]?\r\n" +
            "bing/unit_convert\thow many [target_unit]{unit_pl}[/target_unit] in [amount]{amounts_pl}[/amount] [source_unit]{unit_pl}[/source_unit]?\r\n" +
            "\r\n" +
            "#amounts_pl#\r\n" +
            "4\r\n" +
            "10\r\n" +
            "23\r\n" +
            "5\r\n" +
            "9\r\n" +
            "100\r\n" +
            "1000\r\n" +
            "\r\n" +
            "#unit_si#\r\n" +
            "millimeter\r\n" +
            "centimeter\r\n" +
            "meter\r\n" +
            "kilometer\r\n" +
            "mile\r\n" +
            "foot\r\n" +
            "inch\r\n" +
            "\r\n" +
            "#unit_pl#\r\n" +
            "millimeters\r\n" +
            "centimeters\r\n" +
            "meters\r\n" +
            "kilometers\r\n" +
            "miles\r\n" +
            "inches\r\n" +
            "feet\r\n";

        [TestMethod]
        public void TestBalancedTemplateExpander()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            VirtualPath templateFile = new VirtualPath("test.template");
            fileSystem.AddFile(templateFile, Encoding.UTF8.GetBytes(UnitConvertTemplate));

            TrainingDataTemplate template = new TrainingDataTemplate(templateFile, fileSystem, LanguageCode.EN_US, logger, false, false);
            ITrainingDataStream expansionStream = new TemplateFileExpanderBalanced(template, logger, 1.0f, 100);
            int count = 0;
            while (expansionStream.MoveNext() && count++ < expansionStream.RecommendedOutputCount)
            {
                Console.WriteLine(expansionStream.Current.Utterance);
                Assert.IsTrue(UnitConvertValidator.Match(expansionStream.Current.Utterance).Success);
            }

            Assert.IsTrue(count >= 100);
        }

        [TestMethod]
        public void TestBalancedTemplateExpanderGoesInfinitely()
        {
            ILogger logger = new ConsoleLogger();
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            VirtualPath templateFile = new VirtualPath("test.template");
            fileSystem.AddFile(templateFile, Encoding.UTF8.GetBytes(UnitConvertTemplate));

            TrainingDataTemplate template = new TrainingDataTemplate(templateFile, fileSystem, LanguageCode.EN_US, logger, false, false);
            ITrainingDataStream expansionStream = new TemplateFileExpanderBalanced(template, logger, 1.0f, 100);
            for (int c = 0; c < 10000; c++)
            {
                Assert.IsTrue(expansionStream.MoveNext());
            }
        }

        [TestMethod]
        public void TestBalancedTemplateExpanderEmptyTemplate()
        {
            ILogger logger = new ConsoleLogger();
            TrainingDataTemplate template = new TrainingDataTemplate(logger, LanguageCode.EN_US, false);
            ITrainingDataStream expansionStream = new TemplateFileExpanderBalanced(template, logger, 1.0f, 100);
            Assert.IsFalse(expansionStream.MoveNext());
        }
    }
}
