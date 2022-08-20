using EnergyTracker.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EnergyTracker.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly Data.DatabaseContext db;

        public WeatherController(Data.DatabaseContext db)
        {
            this.db = db;
        }

        [HttpGet("GetHistoricAll")]
        public List<Weather> GetHistoricAll()
        {
            return db.Weathers.ToList();
        }

        [HttpGet("GetHistoricByDate")]
        public List<Weather> GetHistoricByDate(DateTime startDate, DateTime endDate)
        {
            return db.Weathers.Where(x => x.Date >= startDate && x.Date <= endDate).ToList();
        }

        [HttpGet("GetFuture")]
        public List<Forecast> GetFuture(DateTime startDate, int days)
        {
            var forecasts = db.Forecasts.Where(x => x.Date > startDate && x.Date <= startDate.AddDays(days)).ToList();
            var weathers = db.Weathers.Where(x => x.Date > startDate && x.Date <= startDate.AddDays(days)).ToList();

            var diffDatesWeather = weathers.Select(x => x.Date).Except(forecasts.Select(x => x.Date)).ToList();
            if (diffDatesWeather.Any())
            {
                weathers = weathers.Where(x => diffDatesWeather.Contains(x.Date)).ToList();
                var weatherConverted = JsonSerializer.Deserialize<List<Forecast>>(JsonSerializer.Serialize(weathers));
                forecasts.AddRange(weatherConverted);
                return forecasts;
            }
            else
                return forecasts;
        }
    }
}