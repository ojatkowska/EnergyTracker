using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EnergyTracker.Shared.Models
{
    public class Prediction
    {
        [Key]
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("power")]
        public int Power { get; set; }
    }
}