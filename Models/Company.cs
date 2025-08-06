using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("companies")]
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Cui { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public string? CertificatePath { get; set; }

        // Navigation properties
        public ICollection<Location> Locations { get; set; } = new List<Location>();
        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}