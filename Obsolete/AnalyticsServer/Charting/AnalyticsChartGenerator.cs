using Durandal.Common.Instrumentation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DurandalServices.Instrumentation.Analytics.Charting;
using System.Text;
using System.Threading.Tasks;
using DurandalServices.Instrumentation.Analytics.Html;
using Durandal.Common.Database;
using Durandal.Common.Database.MySql;

namespace DurandalServices.Instrumentation.Analytics.Charting
{
    public class AnalyticsChartGenerator
    {
        private MySqlInstrumentation _instrumentationAdapter;
        private Dictionary<string, Func<Task<string>>> _routes;

        public AnalyticsChartGenerator(MySqlInstrumentation instrumentationAdapter)
        {
            _instrumentationAdapter = instrumentationAdapter;
            _routes = new Dictionary<string, Func<Task<string>>>();
            _routes.Add("/latency/daily", () => MakeLatencySummaryPage(TimeSpan.FromDays(14), TimeSpan.FromHours(24)));
            _routes.Add("/latency/hourly", () => MakeLatencySummaryPage(TimeSpan.FromHours(24), TimeSpan.FromHours(1)));
            _routes.Add("/activeusers/daily", () => MakeDAUPage(TimeSpan.FromDays(28)));
            _routes.Add("/rps", () => MakeRPSPage());
        }

        public Task<string> RenderRoute(string url)
        {
            Func<Task<string>> renderer;
            if (_routes.TryGetValue(url, out renderer))
            {
                return renderer();
            }

            return Task.FromResult<string>(null);
        }

        public string DefaultUrl
        {
            get
            {
                return "/latency/daily";
            }
        }

        private List<NavbarLink> CreateNavbar()
        {
            List<NavbarLink> returnVal = new List<NavbarLink>();
            returnVal.Add(new NavbarLink("/latency/daily", "Daily latency"));
            returnVal.Add(new NavbarLink("/latency/hourly", "Hourly latency"));
            returnVal.Add(new NavbarLink("/activeusers/daily", "DAU"));
            returnVal.Add(new NavbarLink("/rps", "RPS"));
            return returnVal;
        }

        private async Task<string> MakeDAUPage(TimeSpan history)
        {
            DateTimeOffset startTime = DateTimeOffset.UtcNow - history;
            DateTimeOffset endTime = DateTimeOffset.UtcNow;
            List<Tuple<DateTimeOffset, int>> dau_data = await _instrumentationAdapter.GetDailyActiveUsers(startTime, endTime);

            List<DateTimeChartPoint> chartPoints = new List<DateTimeChartPoint>();
            foreach (var point in dau_data)
            {
                chartPoints.Add(new DateTimeChartPoint(point.Item1, point.Item2));
            }

            IndexPage indexPage = new IndexPage()
            {
                Content = new SingleChartContainer()
                {
                    ChartElement1 = new DateTimeSingleLineChart()
                    {
                        ChartId = "dau-chart",
                        Label = "Daily active users",
                        Data = chartPoints,
                        LineColor = Color.FromArgb(255, 108, 0),
                        Tension = 0.2f,
                        MinTime = startTime,
                        MaxTime = endTime
                    }.Render(),
                }.Render(),
                NavLinks = CreateNavbar()
            };

            return indexPage.Render();
        }

        private async Task<string> MakeRPSPage()
        {
            DateTimeOffset dailyStartTime = DateTimeOffset.UtcNow - TimeSpan.FromDays(14);
            DateTimeOffset hourlyStartTime = DateTimeOffset.UtcNow - TimeSpan.FromHours(12);
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            List<Tuple<DateTimeOffset, int>> daily_normal_rps = await _instrumentationAdapter.GetDailyRequestRate(dailyStartTime, endTime, false);
            List<Tuple<DateTimeOffset, int>> daily_error_rps = await _instrumentationAdapter.GetDailyRequestRate(dailyStartTime, endTime, true);
            List<Tuple<DateTimeOffset, int>> hourly_normal_rps = await _instrumentationAdapter.GetHourlyRequestRate(hourlyStartTime, endTime, false);
            List<Tuple<DateTimeOffset, int>> hourly_error_rps = await _instrumentationAdapter.GetHourlyRequestRate(hourlyStartTime, endTime, true);

            List<DateTimeChartPoint> daily_normal_points = new List<DateTimeChartPoint>();
            foreach (var point in daily_normal_rps)
            {
                daily_normal_points.Add(new DateTimeChartPoint(point.Item1, point.Item2));
            }

            List<DateTimeChartPoint> daily_error_points = new List<DateTimeChartPoint>();
            foreach (var point in daily_error_rps)
            {
                daily_error_points.Add(new DateTimeChartPoint(point.Item1, point.Item2));
            }

            List<DateTimeChartPoint> hourly_normal_points = new List<DateTimeChartPoint>();
            foreach (var point in hourly_normal_rps)
            {
                hourly_normal_points.Add(new DateTimeChartPoint(point.Item1, point.Item2));
            }

            List<DateTimeChartPoint> hourly_error_points = new List<DateTimeChartPoint>();
            foreach (var point in hourly_error_rps)
            {
                hourly_error_points.Add(new DateTimeChartPoint(point.Item1, point.Item2));
            }

            IndexPage indexPage = new IndexPage()
            {
                Content = new DoubleChartContainerRows()
                {
                    ChartElement1 = new DateTimeDoubleLineChart()
                    {
                        ChartId = "daily-chart",
                        ChartTitle = "Requests per day",
                        Label1 = "Total",
                        Data1 = daily_normal_points,
                        LineColor1 = Color.FromArgb(93, 210, 119),
                        Label2 = "Errors",
                        Data2 = daily_error_points,
                        LineColor2 = Color.FromArgb(255, 49, 49),
                        Tension = 0.2f,
                        MinTime = dailyStartTime,
                        MaxTime = endTime
                    }.Render(),
                    ChartElement2 = new DateTimeDoubleLineChart()
                    {
                        ChartId = "hourly-chart",
                        ChartTitle = "Requests per hour",
                        Label1 = "Total",
                        Data1 = hourly_normal_points,
                        LineColor1 = Color.FromArgb(93, 210, 119),
                        Label2 = "Errors",
                        Data2 = hourly_error_points,
                        LineColor2 = Color.FromArgb(255, 49, 49),
                        Tension = 0.2f,
                        MinTime = dailyStartTime,
                        MaxTime = endTime
                    }.Render()
                }.Render(),
                NavLinks = CreateNavbar()
            };

            return indexPage.Render();
        }

        private async Task<string> MakeLatencySummaryPage(TimeSpan history, TimeSpan binSize)
        {
            DateTime startTime = DateTime.UtcNow - history;
            DateTime endTime = DateTime.UtcNow;
            List<DateTimePercentileChartPoint> luData = await GatherPercentileData(history, binSize, "LatencyLU");
            List<DateTimePercentileChartPoint> deData = await GatherPercentileData(history, binSize, "LatencyDialog");
            List<DateTimePercentileChartPoint> clData = await GatherPercentileData(history, binSize, "LatencyClientPerceived");

            IndexPage indexPage = new IndexPage()
            {
                Content = new TripleChartContainerRows()
                {
                    ChartElement1 = new DateTimePercentileLineChart()
                    {
                        ChartId = "lu-chart",
                        Label = "LU Latency",
                        Data = luData,
                        LineColor = Color.MediumSeaGreen,
                        Tension = 0.2f,
                        MinTime = startTime,
                        MaxTime = endTime
                    }.Render(),
                    ChartElement2 = new DateTimePercentileLineChart()
                    {
                        ChartId = "de-chart",
                        Label = "Dialog Latency",
                        Data = deData,
                        LineColor = Color.SkyBlue,
                        Tension = 0.2f,
                        MinTime = startTime,
                        MaxTime = endTime
                    }.Render(),
                    ChartElement3 = new DateTimePercentileLineChart()
                    {
                        ChartId = "cl-chart",
                        Label = "Client Latency",
                        Data = clData,
                        LineColor = Color.FromArgb(255, 193, 59),
                        Tension = 0.2f,
                        MinTime = startTime,
                        MaxTime = endTime
                    }.Render()
                }.Render(),
                NavLinks = CreateNavbar()
            };

            return indexPage.Render();
        }

        private async Task<List<DateTimePercentileChartPoint>> GatherPercentileData(TimeSpan historyLength, TimeSpan binSize, string latencyKey)
        {
            int numBins = (int)Math.Ceiling(historyLength.TotalSeconds / binSize.TotalSeconds);
            DateTimeOffset traceStart = DateTimeOffset.UtcNow - historyLength;

            // Start putting data into bins
            List<DateTimePercentileChartPoint> points = new List<DateTimePercentileChartPoint>();

            List<float>[] bins = new List<float>[numBins];
            for (int bin = 0; bin < numBins; bin++)
            {
                bins[bin] = new List<float>();
            }

            List<Tuple<DateTimeOffset, float>> latencies = await _instrumentationAdapter.GetLatencyTimeSeries(latencyKey, traceStart, DateTimeOffset.UtcNow);

            foreach (Tuple<DateTimeOffset, float> trace in latencies)
            {
                if (trace.Item1 != default(DateTimeOffset))
                {
                    // Find what bin it goes into
                    int bin = Math.Max(0, Math.Min(numBins - 1, (int)((trace.Item1.ToUniversalTime() - traceStart).TotalSeconds / binSize.TotalSeconds)));
                    bins[bin].Add(trace.Item2);
                }
            }

            DateTimeOffset currentBinTime = traceStart;
            for (int bin = 0; bin < numBins; bin++)
            {
                int currentBinCount = bins[bin].Count;
                // Calculate percentiles
                if (currentBinCount > 0)
                {
                    bins[bin].Sort();
                    points.Add(new DateTimePercentileChartPoint()
                    {
                        PercentileP5 = bins[bin][Math.Max(0, Math.Min(currentBinCount - 1, (int)Math.Round(((float)currentBinCount * 0.05f))))],
                        PercentileP25 = bins[bin][Math.Max(0, Math.Min(currentBinCount - 1, (int)Math.Round(((float)currentBinCount * 0.25f))))],
                        PercentileP50 = bins[bin][Math.Max(0, Math.Min(currentBinCount - 1, (int)Math.Round(((float)currentBinCount * 0.50f))))],
                        PercentileP75 = bins[bin][Math.Max(0, Math.Min(currentBinCount - 1, (int)Math.Round(((float)currentBinCount * 0.75f))))],
                        PercentileP95 = bins[bin][Math.Max(0, Math.Min(currentBinCount - 1, (int)Math.Round(((float)currentBinCount * 0.95f))))],
                        Time = currentBinTime
                    });
                }

                currentBinTime = currentBinTime.Add(binSize);
            }

            return points;
        }
    }
}
