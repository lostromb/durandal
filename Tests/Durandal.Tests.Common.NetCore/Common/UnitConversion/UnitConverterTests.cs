using Durandal.Common.Logger;
using Durandal.Common.UnitConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.UnitConversion
{
    [TestClass]
    public class UnitConverterTests
    {
        private static ILogger _logger = new ConsoleLogger();

        [TestMethod]
        public void TestUnitConversionFootToMeter()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.FOOT, UnitName.METER, 12, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual("3.658", result.TargetAmountString);
            Assert.AreEqual(3.658, (double)result.TargetUnitAmount, 0.001);

            // do some extra assertions for this test only
            Assert.AreEqual(UnitName.FOOT, result.SourceUnitName);
            Assert.AreEqual(UnitName.METER, result.TargetUnitName);
            Assert.AreEqual(12M, result.SourceUnitAmount);
            Assert.IsTrue(result.IsApproximate);
            Assert.IsFalse(result.HasTimeVariance);
        }
        
        [TestMethod]
        public void TestUnitConversionInchToMile()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.INCH, UnitName.MILE, 1, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            // Is this desirable?
            Assert.AreEqual("0", result.TargetAmountString);
            Assert.AreEqual(0.00001578, (double)result.TargetUnitAmount, 0.001);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionInchToMile2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.INCH, UnitName.MILE, 95040, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            // Is this desirable?
            Assert.AreEqual("1.5", result.TargetAmountString);
            Assert.AreEqual(1.5M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionInchToMile3()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.INCH, UnitName.MILE, 100, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual("0.001578", result.TargetAmountString);
            Assert.AreEqual(0.001578, (double)result.TargetUnitAmount, 0.001);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionCentimeterToMeter()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.CENTIMETER, UnitName.METER, 100, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual("1", result.TargetAmountString);
            Assert.AreEqual(1M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionCentimeterToMeter2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.CENTIMETER, UnitName.METER, 1576, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual("15.76", result.TargetAmountString);
            Assert.AreEqual(15.76M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionOunceToKilogram()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.MASS_OUNCE, UnitName.KILOGRAM, 200, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Mass, result.ConversionType);
            Assert.AreEqual("5.67", result.TargetAmountString);
            Assert.AreEqual(5.67, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionSecondsInADay()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.DAY, UnitName.SECOND, 1, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Time, result.ConversionType);
            Assert.AreEqual("86400", result.TargetAmountString);
            Assert.AreEqual(86400M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
            Assert.IsFalse(result.HasTimeVariance);
        }

        [TestMethod]
        public void TestUnitConversionTimeDaysInAMonth()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.MONTH, UnitName.DAY, 2, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Time, result.ConversionType);
            Assert.AreEqual("60", result.TargetAmountString);
            Assert.AreEqual(60, result.TargetUnitAmount);
            Assert.IsTrue(result.IsApproximate);
            Assert.IsTrue(result.HasTimeVariance);
        }

        [TestMethod]
        public void TestUnitConversionTimeMonthsInAYear()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.YEAR, UnitName.MONTH, 1, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Time, result.ConversionType);
            Assert.AreEqual("12", result.TargetAmountString);
            Assert.AreEqual(12M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
            Assert.IsFalse(result.HasTimeVariance);
        }

        [TestMethod]
        public void TestUnitConversionTimeDaysInAWeek()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.WEEK, UnitName.DAY, 2, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Time, result.ConversionType);
            Assert.AreEqual("14", result.TargetAmountString);
            Assert.AreEqual(14M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
            Assert.IsFalse(result.HasTimeVariance);
        }

        [TestMethod]
        public void TestUnitConversionTimeHoursToMonths()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.HOUR, UnitName.MONTH, 30000, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Time, result.ConversionType);
            Assert.AreEqual("41.1", result.TargetAmountString);
            Assert.AreEqual(41.1, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
            Assert.IsTrue(result.HasTimeVariance);
        }

        [TestMethod]
        public void TestUnitConversionDaysInAYear()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.YEAR, UnitName.DAY, 1, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Time, result.ConversionType);
            Assert.AreEqual("365", result.TargetAmountString);
            Assert.AreEqual(365M, result.TargetUnitAmount);
            Assert.IsFalse(result.IsApproximate);
            Assert.IsTrue(result.HasTimeVariance);
        }

        [TestMethod]
        public void TestUnitConversionCelsiusToFahrenheit()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.CELSIUS, UnitName.FAHRENHEIT, 30, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Temperature, result.ConversionType);
            Assert.AreEqual("86", result.TargetAmountString);
            Assert.AreEqual(86, (double)result.TargetUnitAmount, 0.01);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionFahrenheitToCelsius()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.FAHRENHEIT, UnitName.CELSIUS, -30, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Temperature, result.ConversionType);
            Assert.AreEqual("-34.44", result.TargetAmountString);
            Assert.AreEqual(-34.44, (double)result.TargetUnitAmount, 0.01);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionKelvinToFahrenheit()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.KELVIN, UnitName.FAHRENHEIT, 198, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Temperature, result.ConversionType);
            Assert.AreEqual("-103.3", result.TargetAmountString);
            Assert.AreEqual(-103.27, (double)result.TargetUnitAmount, 0.01);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionCelsiusToFahrenheit2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.CELSIUS, UnitName.FAHRENHEIT, 0, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Temperature, result.ConversionType);
            Assert.AreEqual("32", result.TargetAmountString);
            Assert.AreEqual(32, (double)result.TargetUnitAmount, 0.01);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionIncompatible1()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.FOOT, UnitName.POUND, 12, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestUnitConversionIncompatible2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.CELSIUS, UnitName.US_FLUID_OUNCE, 12, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestUnitConversionIncompatible3()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.SECOND, UnitName.YARD, 12, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestUnitConversionNoOp()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.METER, UnitName.METER, 12, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual(UnitName.METER, result.SourceUnitName);
            Assert.AreEqual(UnitName.METER, result.TargetUnitName);
            Assert.AreEqual("12", result.TargetAmountString);
            Assert.AreEqual(12M, result.SourceUnitAmount);
            Assert.AreEqual(12M, result.TargetUnitAmount);
            Assert.IsFalse(result.ConversionWasRequired);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionNoOp2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.METER, UnitSystemName.METRIC, 12, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual(UnitName.METER, result.SourceUnitName);
            Assert.AreEqual(UnitName.METER, result.TargetUnitName);
            Assert.AreEqual("12", result.TargetAmountString);
            Assert.AreEqual(12M, result.SourceUnitAmount);
            Assert.AreEqual(12M, result.TargetUnitAmount);
            Assert.IsFalse(result.ConversionWasRequired);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionAmbigOunceMass()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.POUND, UnitName.AMBIG_ENG_OUNCE, 2, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Mass, result.ConversionType);
            Assert.AreEqual(UnitName.MASS_OUNCE, result.TargetUnitName);
            Assert.AreEqual("32", result.TargetAmountString);
            Assert.AreEqual(32M, result.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAmbigOunceMass2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_OUNCE, UnitName.POUND, 20, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Mass, result.ConversionType);
            Assert.AreEqual(UnitName.MASS_OUNCE, result.SourceUnitName);
            Assert.AreEqual("1.25", result.TargetAmountString);
            Assert.AreEqual(1.25M, result.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAmbigOunceVolume()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.LITER, UnitName.AMBIG_ENG_OUNCE, 1, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Volume, result.ConversionType);
            Assert.AreEqual(UnitName.US_FLUID_OUNCE, result.TargetUnitName);
            Assert.AreEqual("33.81", result.TargetAmountString);
            Assert.AreEqual(33.814, (double)result.TargetUnitAmount, 0.001);
        }

        [TestMethod]
        public void TestUnitConversionAmbigOunceVolume2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.LITER, UnitName.AMBIG_ENG_OUNCE, 1, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Volume, result.ConversionType);
            Assert.AreEqual(UnitName.US_FLUID_OUNCE, result.TargetUnitName);
            Assert.AreEqual("33.81", result.TargetAmountString);
            Assert.AreEqual(33.814, (double)result.TargetUnitAmount, 0.001);
        }

        [TestMethod]
        public void TestUnitConversionAmbigOunceVolume3()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.LITER, UnitName.AMBIG_ENG_OUNCE, 1, _logger, UnitSystem.BritishImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Volume, result.ConversionType);
            Assert.AreEqual(UnitName.IMP_FLUID_OUNCE, result.TargetUnitName);
            Assert.AreEqual("35.2", result.TargetAmountString);
            Assert.AreEqual(35.195, (double)result.TargetUnitAmount, 0.001);
        }

        /// <summary>
        /// This one is important because it tests that we can output several hypotheses in the presence of ambiguity
        /// </summary>
        [TestMethod]
        public void TestUnitConversionAmbigOunceToMetric()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_OUNCE, UnitSystemName.METRIC, 1, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
            UnitConversionResult volumeResult = results.Where((r) => r.ConversionType == UnitType.Volume).FirstOrDefault();
            Assert.IsNotNull(volumeResult);
            UnitConversionResult massResult = results.Where((r) => r.ConversionType == UnitType.Mass).FirstOrDefault();
            Assert.IsNotNull(massResult);

            Assert.AreEqual(UnitName.US_FLUID_OUNCE, volumeResult.SourceUnitName);
            Assert.AreEqual(UnitName.MILLILITER, volumeResult.TargetUnitName);
            Assert.AreEqual("29.57", volumeResult.TargetAmountString);
            Assert.AreEqual(29.573, (double)volumeResult.TargetUnitAmount, 0.001);

            Assert.AreEqual(UnitName.MASS_OUNCE, massResult.SourceUnitName);
            Assert.AreEqual(UnitName.GRAM, massResult.TargetUnitName);
            Assert.AreEqual("28.35", massResult.TargetAmountString);
            Assert.AreEqual(28.35, (double)massResult.TargetUnitAmount, 0.001);
        }

        [TestMethod]
        public void TestUnitConversionUnambiguousQuart()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_QUART, UnitName.AMBIG_ENG_GALLON, 1, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult usGallonResult = results[0];
            Assert.AreEqual(UnitName.US_QUART, usGallonResult.SourceUnitName);
            Assert.AreEqual(UnitName.US_GALLON, usGallonResult.TargetUnitName);
            Assert.AreEqual("0.25", usGallonResult.TargetAmountString);
            Assert.AreEqual(0.25M, usGallonResult.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAmbigPint()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_PINT, UnitSystemName.METRIC, 60, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);

            UnitConversionResult usPintResult = results.Where((r) => r.SourceUnitName == UnitName.US_PINT).FirstOrDefault();
            Assert.IsNotNull(usPintResult);
            UnitConversionResult britishPintResult = results.Where((r) => r.SourceUnitName == UnitName.IMP_PINT).FirstOrDefault();
            Assert.IsNotNull(britishPintResult);

            Assert.AreEqual(UnitName.US_PINT, usPintResult.SourceUnitName);
            Assert.AreEqual(UnitName.LITER, usPintResult.TargetUnitName);
            Assert.AreEqual("28.39", usPintResult.TargetAmountString);
            Assert.AreEqual(28.39, (double)usPintResult.TargetUnitAmount, 0.01);

            Assert.AreEqual(UnitName.IMP_PINT, britishPintResult.SourceUnitName);
            Assert.AreEqual(UnitName.LITER, britishPintResult.TargetUnitName);
            Assert.AreEqual("34.1", britishPintResult.TargetAmountString);
            Assert.AreEqual(34.09, (double)britishPintResult.TargetUnitAmount, 0.01);
        }

        [TestMethod]
        public void TestUnitConversionFeetToMetric()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.FOOT, UnitSystemName.METRIC, 300, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Length, result.ConversionType);
            Assert.AreEqual(UnitName.METER, result.TargetUnitName);
            Assert.AreEqual("91.44", result.TargetAmountString);
            Assert.AreEqual(91.44, (double)result.TargetUnitAmount, 0.001);
        }

        [TestMethod]
        public void TestUnitConversionSqYardToMetric()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.SQUARE_YARD, UnitSystemName.METRIC, 300, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Area, result.ConversionType);
            Assert.AreEqual(UnitName.SQUARE_METER, result.TargetUnitName);
            Assert.AreEqual("250.8", result.TargetAmountString);
            Assert.AreEqual(250.838, (double)result.TargetUnitAmount, 0.01);
        }

        [TestMethod]
        public void TestUnitConversionToImperialAmbiguous()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.LITER, UnitSystemName.IMPERIAL, 8, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);

            UnitConversionResult usGallonResult = results.Where((r) => r.TargetUnitName == UnitName.US_GALLON).FirstOrDefault();
            Assert.IsNotNull(usGallonResult);
            UnitConversionResult britishGallonResult = results.Where((r) => r.TargetUnitName == UnitName.IMP_GALLON).FirstOrDefault();
            Assert.IsNotNull(britishGallonResult);

            Assert.AreEqual(UnitName.LITER, usGallonResult.SourceUnitName);
            Assert.AreEqual(UnitName.US_GALLON, usGallonResult.TargetUnitName);
            Assert.AreEqual("2.113", usGallonResult.TargetAmountString);
            Assert.AreEqual(2.113, (double)usGallonResult.TargetUnitAmount, 0.001);

            Assert.AreEqual(UnitName.LITER, britishGallonResult.SourceUnitName);
            Assert.AreEqual(UnitName.IMP_GALLON, britishGallonResult.TargetUnitName);
            Assert.AreEqual("1.76", britishGallonResult.TargetAmountString);
            Assert.AreEqual(1.76, (double)britishGallonResult.TargetUnitAmount, 0.001);
        }

        [TestMethod]
        public void TestUnitConversionToImperialAmbiguous2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_GALLON, UnitSystemName.IMPERIAL, 8, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);

            UnitConversionResult usGallonResult = results.Where((r) => r.TargetUnitName == UnitName.US_GALLON).FirstOrDefault();
            Assert.IsNotNull(usGallonResult);
            UnitConversionResult britishGallonResult = results.Where((r) => r.TargetUnitName == UnitName.IMP_GALLON).FirstOrDefault();
            Assert.IsNotNull(britishGallonResult);

            Assert.AreEqual(UnitName.US_GALLON, usGallonResult.SourceUnitName);
            Assert.AreEqual(UnitName.US_GALLON, usGallonResult.TargetUnitName);
            Assert.AreEqual("8", usGallonResult.TargetAmountString);
            Assert.AreEqual(8M, usGallonResult.TargetUnitAmount);

            Assert.AreEqual(UnitName.IMP_GALLON, britishGallonResult.SourceUnitName);
            Assert.AreEqual(UnitName.IMP_GALLON, britishGallonResult.TargetUnitName);
            Assert.AreEqual("8", britishGallonResult.TargetAmountString);
            Assert.AreEqual(8M, britishGallonResult.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionToImperialAmbiguous3()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_GALLON, UnitSystemName.IMPERIAL, 8, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult usGallonResult = results[0];
            Assert.AreEqual(UnitName.US_GALLON, usGallonResult.SourceUnitName);
            Assert.AreEqual(UnitName.US_GALLON, usGallonResult.TargetUnitName);
            Assert.AreEqual("8", usGallonResult.TargetAmountString);
            Assert.AreEqual(8M, usGallonResult.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionSuperAmbiguous()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_OUNCE, UnitSystemName.IMPERIAL, 100, _logger, UnitSystem.Unspecified);
            Assert.IsNotNull(results);
            Assert.AreEqual(3, results.Count);

            UnitConversionResult usQuartResult = results.Where((r) => r.TargetUnitName == UnitName.US_QUART).FirstOrDefault();
            Assert.IsNotNull(usQuartResult);
            UnitConversionResult britishQuartResult = results.Where((r) => r.TargetUnitName == UnitName.IMP_QUART).FirstOrDefault();
            Assert.IsNotNull(britishQuartResult);
            UnitConversionResult poundResult = results.Where((r) => r.TargetUnitName == UnitName.POUND).FirstOrDefault();
            Assert.IsNotNull(poundResult);

            Assert.AreEqual(UnitName.US_FLUID_OUNCE, usQuartResult.SourceUnitName);
            Assert.AreEqual(UnitName.US_QUART, usQuartResult.TargetUnitName);
            Assert.AreEqual("3.125", usQuartResult.TargetAmountString);
            Assert.AreEqual(3.125M, usQuartResult.TargetUnitAmount);

            Assert.AreEqual(UnitName.IMP_FLUID_OUNCE, britishQuartResult.SourceUnitName);
            Assert.AreEqual(UnitName.IMP_QUART, britishQuartResult.TargetUnitName);
            Assert.AreEqual("2.5", britishQuartResult.TargetAmountString);
            Assert.AreEqual(2.5M, britishQuartResult.TargetUnitAmount);

            Assert.AreEqual(UnitName.MASS_OUNCE, poundResult.SourceUnitName);
            Assert.AreEqual(UnitName.POUND, poundResult.TargetUnitName);
            Assert.AreEqual("6.25", poundResult.TargetAmountString);
            Assert.AreEqual(6.25M, poundResult.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionSuperAmbiguous2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_OUNCE, UnitSystemName.IMPERIAL, 100, _logger, UnitSystem.BritishImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
            
            UnitConversionResult britishQuartResult = results.Where((r) => r.TargetUnitName == UnitName.IMP_QUART).FirstOrDefault();
            Assert.IsNotNull(britishQuartResult);
            UnitConversionResult poundResult = results.Where((r) => r.TargetUnitName == UnitName.POUND).FirstOrDefault();
            Assert.IsNotNull(poundResult);
            
            Assert.AreEqual(UnitName.IMP_FLUID_OUNCE, britishQuartResult.SourceUnitName);
            Assert.AreEqual(UnitName.IMP_QUART, britishQuartResult.TargetUnitName);
            Assert.AreEqual("2.5", britishQuartResult.TargetAmountString);
            Assert.AreEqual(2.5M, britishQuartResult.TargetUnitAmount);

            Assert.AreEqual(UnitName.MASS_OUNCE, poundResult.SourceUnitName);
            Assert.AreEqual(UnitName.POUND, poundResult.TargetUnitName);
            Assert.AreEqual("6.25", poundResult.TargetAmountString);
            Assert.AreEqual(6.25M, poundResult.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionMoreAmbiguity()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_OUNCE, UnitSystemName.IMPERIAL, 1, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);

            UnitConversionResult volumeResult = results.Where((r) => r.TargetUnitName == UnitName.MASS_OUNCE).FirstOrDefault();
            Assert.IsNotNull(volumeResult);
            UnitConversionResult massResult = results.Where((r) => r.TargetUnitName == UnitName.US_FLUID_OUNCE).FirstOrDefault();
            Assert.IsNotNull(massResult);

            Assert.AreEqual(UnitName.MASS_OUNCE, volumeResult.SourceUnitName);
            Assert.AreEqual(UnitName.MASS_OUNCE, volumeResult.TargetUnitName);
            Assert.AreEqual("1", volumeResult.TargetAmountString);
            Assert.AreEqual(1M, volumeResult.TargetUnitAmount);

            Assert.AreEqual(UnitName.US_FLUID_OUNCE, massResult.SourceUnitName);
            Assert.AreEqual(UnitName.US_FLUID_OUNCE, massResult.TargetUnitName);
            Assert.AreEqual("1", massResult.TargetAmountString);
            Assert.AreEqual(1M, massResult.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAlreadyInTargetSystem()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.US_FLUID_OUNCE, UnitSystemName.IMPERIAL, 5, _logger, UnitSystem.USImperial);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Volume, result.ConversionType);
            Assert.AreEqual(UnitName.US_FLUID_OUNCE, result.SourceUnitName);
            Assert.AreEqual(UnitName.US_FLUID_OUNCE, result.TargetUnitName);
            Assert.AreEqual("5", result.TargetAmountString);
            Assert.AreEqual(5M, result.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAmbiguousCVolume()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_CELSIUS, UnitSystemName.METRIC, 10, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Volume, result.ConversionType);
            Assert.AreEqual(UnitName.LITER, result.TargetUnitName);
            Assert.AreEqual("2.366", result.TargetAmountString);
            Assert.AreEqual(2.366, (double)result.TargetUnitAmount, 0.001);
        }

        [TestMethod]
        public void TestUnitConversionAmbiguousCVolume2()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_CELSIUS, UnitName.US_QUART, 10, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Volume, result.ConversionType);
            Assert.AreEqual(UnitName.US_QUART, result.TargetUnitName);
            Assert.AreEqual("2.5", result.TargetAmountString);
            Assert.AreEqual(2.5M, result.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAmbiguousCTemperature()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.AMBIG_ENG_CELSIUS, UnitName.FAHRENHEIT, 0, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Temperature, result.ConversionType);
            Assert.AreEqual(UnitName.FAHRENHEIT, result.TargetUnitName);
            Assert.AreEqual("32", result.TargetAmountString);
            Assert.AreEqual(32M, result.TargetUnitAmount);
        }

        [TestMethod]
        public void TestUnitConversionAmbiguousPoundUsage()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.POUND, UnitSystemName.METRIC, 8, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);

            UnitConversionResult massResult = results.Where((r) => r.TargetUnitName == UnitName.KILOGRAM).FirstOrDefault();
            Assert.IsNotNull(massResult);
            UnitConversionResult forceResult = results.Where((r) => r.TargetUnitName == UnitName.NEWTON).FirstOrDefault();
            Assert.IsNotNull(forceResult);

            Assert.AreEqual(UnitName.POUND, massResult.SourceUnitName);
            Assert.AreEqual(UnitName.KILOGRAM, massResult.TargetUnitName);
            Assert.AreEqual("3.629", massResult.TargetAmountString);
            Assert.AreEqual(3.629, (double)massResult.TargetUnitAmount, 0.001);

            Assert.AreEqual(UnitName.POUND, forceResult.SourceUnitName);
            Assert.AreEqual(UnitName.NEWTON, forceResult.TargetUnitName);
            Assert.AreEqual("35.59", forceResult.TargetAmountString);
            Assert.AreEqual(35.59, (double)forceResult.TargetUnitAmount, 0.01);
        }

        [TestMethod]
        public void TestUnitConversionMbarToInHg()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.MILLIBAR, UnitName.INCHES_MERCURY, 1000, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Pressure, result.ConversionType);
            Assert.AreEqual("29.53", result.TargetAmountString);
            Assert.AreEqual(29.529, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionPascalToTorr()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.PASCAL, UnitName.TORR, 1000, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Pressure, result.ConversionType);
            Assert.AreEqual("7.501", result.TargetAmountString);
            Assert.AreEqual(7.501, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionMPHtoKPH()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.MILE_PER_HOUR, UnitName.KILOMETER_PER_HOUR, 60, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Speed, result.ConversionType);
            Assert.AreEqual("96.56", result.TargetAmountString);
            Assert.AreEqual(96.56, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionMPHtoMPS()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.MILE_PER_HOUR, UnitName.METER_PER_SECOND, 60, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Speed, result.ConversionType);
            Assert.AreEqual("26.82", result.TargetAmountString);
            Assert.AreEqual(26.82, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionDegreesToRadians()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.DEGREE, UnitName.RADIAN, 60, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Angle, result.ConversionType);
            Assert.AreEqual("1.047", result.TargetAmountString);
            Assert.AreEqual(1.047, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionPoundsToNewtons()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.POUND, UnitName.NEWTON, 100, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Force, result.ConversionType);
            Assert.AreEqual("444.8", result.TargetAmountString);
            Assert.AreEqual(444.8, (double)result.TargetUnitAmount, 0.1);
            Assert.IsTrue(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionJouleToCalorie()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.JOULE, UnitName.CALORIE, 100, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Energy, result.ConversionType);
            Assert.AreEqual("23.9", result.TargetAmountString);
            Assert.AreEqual(23.9, (double)result.TargetUnitAmount, 0.01);
            Assert.IsFalse(result.IsApproximate);
        }

        [TestMethod]
        public void TestUnitConversionKilowattToHorsepower()
        {
            List<UnitConversionResult> results = UnitConverter.Convert(UnitName.KILOWATT, UnitName.HORSEPOWER, 5, _logger);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            UnitConversionResult result = results[0];
            Assert.AreEqual(UnitType.Power, result.ConversionType);
            Assert.AreEqual("6.705", result.TargetAmountString);
            Assert.AreEqual(6.705, (double)result.TargetUnitAmount, 0.01);
            Assert.IsTrue(result.IsApproximate);
        }
    }
}
