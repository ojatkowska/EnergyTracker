using EnergyTracker.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using EFCore.BulkExtensions;
using System.Text.Json;
using System.Diagnostics;

namespace EnergyTracker.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KseController : ControllerBase
    {
        private readonly PublicHoliday.PolandPublicHoliday publicHoliday;
        private readonly Data.DatabaseContext db;

        public KseController(Data.DatabaseContext db)
        {
            this.db = db;
            publicHoliday = new PublicHoliday.PolandPublicHoliday();
        }

        [HttpGet("GetWithWeather")]
        public IActionResult GetWithWeather()
        {
            var kses = db.Kses.ToList();
            var weathers = db.Weathers.ToList();

            var diffDatesWeather = kses.Select(x => x.Date).Except(weathers.Select(x => x.Date)).ToList();
            var forecasts = db.Forecasts.Where(x => diffDatesWeather.Contains(x.Date)).ToList();
            var forecastsConverted = JsonSerializer.Deserialize<List<Weather>>(JsonSerializer.Serialize(forecasts));
            weathers.AddRange(forecastsConverted);
            foreach (var item in diffDatesWeather.Except(forecasts.Select(x => x.Date)))
            {
                weathers.Add(new Weather
                {
                    Date = item.Date,
                    IsWorkingDay = publicHoliday.IsWorkingDay(item.Date),
                    Humidity = weathers.Single(x => x.Date == item.Date.AddDays(-1)).Humidity,
                    Pressure = weathers.Single(x => x.Date == item.Date.AddDays(-1)).Pressure,
                    Temperature = weathers.Single(x => x.Date == item.Date.AddDays(-1)).Temperature,
                    WindSpeed = weathers.Single(x => x.Date == item.Date.AddDays(-1)).WindSpeed,
                });
            }

            var diffDatesKse = weathers.Select(x => x.Date).Except(kses.Select(x => x.Date)).ToList();
            foreach (var item in diffDatesKse.Where(x => x.Date >= kses.First().Date))
            {
                kses.Add(new Kse
                {
                    Date = item.Date,
                    Power = kses.Single(x => x.Date == item.Date.AddHours(1)).Power
                });
            }

            var data = kses.Join(weathers, kse => kse.Date, weather => weather.Date, (kse, weather) => new
            {
                kse.Date,
                kse.Power,
                weather.Temperature,
                weather.Humidity,
                weather.WindSpeed,
                weather.Pressure,
                weather.IsWorkingDay,
            }).OrderBy(x => x.Date).ToList();
            return Ok(data);
        }

        [HttpGet("GetAll")]
        public List<Kse> GetAll()
        {
            return db.Kses.ToList();
        }

        [HttpGet("GetByDate")]
        public List<Kse> GetByDate(DateTime startDate, DateTime endDate)
        {
            return db.Kses.Where(x => x.Date >= startDate && x.Date <= endDate).ToList();
        }

        [HttpGet("GetPredictions")]
        public List<Prediction> GetPredictions()
        {
            return db.Predictions.ToList();
        }

        [HttpGet("MakePredictions")]
        public IActionResult MakePredictions(int windowLength = 168, int horizon = 24, int batchSize = 32, bool shuffle = true, int epochs = 5)
        {
            var processInfo = new ProcessStartInfo("python3", $"ml-script.py --window_length {windowLength} --horizon {horizon} --batch_size {batchSize} --shuffle {shuffle} --epochs {epochs}")
            {
                WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory + "Scripts",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Process p = Process.Start(processInfo);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return Ok();
        }

        [HttpPost("PostPredictions")]
        public IActionResult PostPredictions([FromBody] string json)
        {
            var predictions = JsonSerializer.Deserialize<List<Prediction>>(json);

            if (predictions.Count > 0)
                db.BulkDelete(db.Predictions.ToList());

            db.BulkInsert(predictions);
            return Ok();
        }
    }
}