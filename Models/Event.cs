using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models;

public class Event
{
    public int Id { get; set; }
    public byte[] Photo { get; set; } = Array.Empty<byte>(); // Legacy field - will be removed later
    public string? PhotoPath { get; set; } // New file path field
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    
    // New fields for enhanced event management
    public DateTime EventDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
        public bool IsActive { get; set; } = true;
    
    // Address and location data
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey("Company")]
    public int CompanyId { get; set; }
    
    public Company Company { get; set; } = null!;
}