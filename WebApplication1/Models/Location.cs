using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("locations")]
    public class Location
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Tags { get; set; } = string.Empty;
        public byte[] Photo { get; set; } = Array.Empty<byte>();
        public string MenuName { get; set; } = string.Empty;
        public byte[] MenuData { get; set; } = Array.Empty<byte>();
        public bool HasMenu { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Company Company { get; set; } = null!;
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<LocationHour> LocationHours { get; set; } = new List<LocationHour>();
    }
}
