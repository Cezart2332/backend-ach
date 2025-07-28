using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("location_hours")]
    public class LocationHour
    {
        public int Id { get; set; }
        public int LocationId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan? OpenTime { get; set; }
        public TimeSpan? CloseTime { get; set; }
        public bool IsClosed { get; set; } = false;

        // Navigation properties
        public Location Location { get; set; } = null!;
    }
}
