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

    public class KseDataDownloadJob
    {
        private readonly DatabaseContext? db;

        public KseDataDownloadJob(DatabaseContext db)
        {
            this.db = db;
        }

        [AutomaticRetry(Attempts = 10)]
        [DisableConcurrentExecution(3600)]
        public async Task Execute(PerformContext performContext)
        {
            DateTime startDate = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Local);

            if (db.Kses.Any())
                startDate = db.Kses.OrderByDescending(x => x.Date).First().Date.AddDays(-2);

            int iterate = 30;
            DateTime endDate = DateTime.Now.AddHours(-2);

            for (DateTime time = startDate; time < endDate; time = time.AddDays(iterate) )
            {
                long timestamp1 = ConvertToTimestamp(time);

                long timestamp2 = time.AddDays(iterate) > endDate ? ConvertToTimestamp(endDate) : ConvertToTimestamp(time.AddDays(iterate));

                List<KseCSV> csv = GetCSV($"https://www.pse.pl/dane-systemowe/funkcjonowanie-kse/raporty-dobowe-z-pracy-kse/zapotrzebowanie-mocy-kse?p_p_id=danekse_WAR_danekserbportlet&p_p_lifecycle=2&p_p_state=normal&p_p_mode=view&p_p_cacheability=cacheLevelPage&p_p_col_id=column-2&p_p_col_count=1&_danekse_WAR_danekserbportlet_type=kse&_danekse_WAR_danekserbportlet_target=csv&_danekse_WAR_danekserbportlet_from={timestamp1}&_danekse_WAR_danekserbportlet_to={timestamp2}");

                List<Kse> kses = new List<Kse>();
                foreach (var item in csv)
                {
                    Kse kse = new Kse();

                    // date handling
                    DateTime date = DateTime.ParseExact(item.Date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);

                    int hour = 0;
                    if (int.TryParse(item.Hour, out hour))
                    {
                        if (hour > 24)
                            continue;
                        else
                            kse.Date = date.AddHours(hour);
                    }
                    else
                        continue;

                    // power handling
                    if (item.RealPowerDemand == "-" && kse.Date > endDate)
                        break;

                    try 
                    {
                        kse.Power = (int)(Convert.ToDouble(item.RealPowerDemand, CultureInfo.CreateSpecificCulture("pl-PL")) * 1000);
                    }
                    catch
                    {
                        kse.Power = (int)(Convert.ToDouble(item.ForecastPowerDemand, CultureInfo.CreateSpecificCulture("pl-PL")) * 1000);
                    }

                    kses.Add(kse);
                }

                await db.BulkInsertOrUpdateAsync(kses);
                performContext.WriteLine($"Added KSE data for period {time} - {time.AddDays(iterate)}");
            }
        }

        public List<KseCSV> GetCSV(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("user-agent", "API");
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            StreamReader streamReader = new StreamReader(response.GetResponseStream());

            CsvConfiguration configuration = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";", Encoding = Encoding.UTF8 };
            using (var csv = new CsvReader(streamReader, configuration))
            {
                return csv.GetRecords<KseCSV>().ToList();
            }
        }

        private long ConvertToTimestamp(DateTime value)
        {
            long epoch = (value.Ticks - 621355968000000000) / 10000;
            return epoch;
        }
    }

    public class KseCSV
    {
        [Index(0)]
        public string Date { get; set; }

        [Index(1)]
        public string Hour { get; set; }

        [Index(2)]
        public string? ForecastPowerDemand { get; set; }

        [Index(3)]
        public string? RealPowerDemand{ get; set; }
    }
}
