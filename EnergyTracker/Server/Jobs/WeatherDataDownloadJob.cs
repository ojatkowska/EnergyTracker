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

namespace EnergyTracker.Server.Jobs
{

    public class WeatherDataDownloadJob
    {
        private readonly DatabaseContext? db;
        private readonly PolandPublicHoliday publicHoliday;
        private readonly Dictionary<string, int> meteoStations;

        public WeatherDataDownloadJob(DatabaseContext db)
        {
            this.db = db;
            publicHoliday = new PolandPublicHoliday();
            meteoStations = new Dictionary<string, int>()
            {
                { "Łódź", 12465 }
            };
        }

        [AutomaticRetry(Attempts = 10)]
        [DisableConcurrentExecution(3600)]
        public async Task Execute(PerformContext performContext)
        {
            DateTime startDate = new DateTime(2015, 1, 5, 0, 0, 0, DateTimeKind.Local);

            if (db.Weathers.Any())
                startDate = db.Weathers.OrderByDescending(x => x.Date).First().Date.AddDays(-2);

            int iterate = 5; // range is from 1 to 5
            DateTime endDate = DateTime.Now.AddDays(iterate).AddHours(-2);

            for (DateTime time = startDate; time < endDate; time = time.AddDays(iterate))
            {
                WebClient webClient = new WebClient();
                string page = webClient.DownloadString($"https://meteomodel.pl/aktualne-dane-pomiarowe/?data={time.ToString("yyyy-MM-dd")}&rodzaj=st&wmoid={meteoStations["Łódź"]}&dni={iterate}&ord=asc");

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(page);

                List<List<string>> table = doc.DocumentNode.SelectSingleNode("//table[@id='tablepl']")
                            .Descendants("tr")
                            .Skip(2)
                            .Where(tr => tr.Elements("td").Count() > 1)
                            .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                            .ToList();

                var weathers = new List<Weather>();
                foreach (var element in table)
                {
                    var weather = new Weather();
                    try
                    {
                        weather.Date = Convert.ToDateTime(element[0]);
                        weather.Temperature = Convert.ToDouble(element[1], CultureInfo.InvariantCulture);
                        weather.Humidity = Convert.ToInt32(element[7]);
                        weather.WindSpeed = Convert.ToInt32(element[14]);
                        weather.Pressure = Convert.ToDouble(element[19], CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        weather.Date = weathers.Last().Date.AddHours(1);
                        weather.Temperature = weathers.Last().Temperature;
                        weather.Humidity = weathers.Last().Humidity;
                        weather.WindSpeed = weathers.Last().WindSpeed;
                        weather.Pressure = weathers.Last().Pressure;
                    }
                    weather.IsWorkingDay = publicHoliday.IsWorkingDay(weather.Date);
                    weathers.Add(weather);
                }

                await db.BulkInsertOrUpdateAsync(weathers);
                performContext.WriteLine($"Added weather data for period {time.AddDays(-1 * iterate)} - {time}");
            }
        }
    }
}
