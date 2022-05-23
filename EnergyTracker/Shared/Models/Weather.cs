using System.ComponentModel.DataAnnotations;

namespace EnergyTracker.Shared.Models
{
    public class Weather
    {
        [Key]
        public DateTime Date { get; set; }

        public double Temperature { get; set; }

        public int Humidity { get; set; }

        public int WindSpeed { get; set; }

        public double Pressure { get; set; }

        public bool IsWorkingDay { get; set; }
    }
}