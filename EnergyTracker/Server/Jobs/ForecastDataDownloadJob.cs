using EnergyTracker.Server.Data;
using Hangfire;
using System.Net;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using System.Text;
using EnergyTracker.Shared.Models;
using Hangfire.Server;
using Hangfire.Console;
using PublicHoliday;
using EFCore.BulkExtensions;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace EnergyTracker.Server.Jobs
{

    public class ForecastDataDownloadJob
    {
        private readonly DatabaseContext? db;
        private readonly PolandPublicHoliday publicHoliday;


        public ForecastDataDownloadJob(DatabaseContext db)
        {
            this.db = db;
            publicHoliday = new PolandPublicHoliday();
        }

        [AutomaticRetry(Attempts = 10)]
        [DisableConcurrentExecution(3600)]
        public async Task Execute(PerformContext performContext, string apiKey)
        {
            //HttpClient client = new HttpClient();
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Authorization", apiKey);
            //var json = await client.GetStringAsync("https://api.stormglass.io/v2/weather/point?lat=51.7592&lng=19.4560&params=airTemperature,windSpeed,pressure,humidity");

            var processInfo = new ProcessStartInfo("curl", $"\"https://api.stormglass.io/v2/weather/point?lat=51.7592&lng=19.4560&params=airTemperature,windSpeed,pressure,humidity\" -H \"Authorization: {apiKey}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Process p = Process.Start(processInfo);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var jsonArray = JsonDocument.Parse(output).RootElement.GetProperty("hours");

            var forecasts = new List<Forecast>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                var forecast = new Forecast();
                forecast.Temperature = item.GetProperty("airTemperature").GetProperty("noaa").GetDouble();
                forecast.Humidity = (int)item.GetProperty("humidity").GetProperty("noaa").GetDouble();
                forecast.Pressure = item.GetProperty("pressure").GetProperty("noaa").GetDouble();
                forecast.Date = item.GetProperty("time").GetDateTime();
                forecast.WindSpeed = (int)item.GetProperty("windSpeed").GetProperty("noaa").GetDouble();
                forecast.IsWorkingDay = publicHoliday.IsWorkingDay(forecast.Date);
                forecasts.Add(forecast);
            }

            await db.BulkInsertOrUpdateAsync(forecasts);
            performContext.WriteLine($"Added forecast data for period {forecasts.First().Date} - {forecasts.Last().Date}");
        }
    }
}
