namespace WebApplication1.Models;

public class EventResponse
{
    public int Id { get; set; }
    public string Photo { get; set; } = string.Empty;
    public bool HasPhoto { get; set; } = false;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public int Likes { get; set; }
    public string Company { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    
    // New fields for enhanced event management
    public DateTime EventDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    
    // Event address (independent of company locations)
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public DateTime CreatedAt { get; set; }
}