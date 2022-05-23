using System.ComponentModel.DataAnnotations;

namespace EnergyTracker.Shared.Models
{
    public class Kse
    {
        [Key]
        public DateTime Date { get; set; }

        public int Power { get; set; }
    }
}