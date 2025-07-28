using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models;

public class Like
{
    public int Id { get; set; }
    
    [ForeignKey("Event")]
    public int EventId { get; set; }
    
    [ForeignKey("User")]
    public int UserId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Event Event { get; set; } = null!;
    public User User { get; set; } = null!;
}
