using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class BugReport
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(255)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string DeviceType { get; set; } = string.Empty; // ios or android
        
        [StringLength(500)]
        public string? DeviceInfo { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsResolved { get; set; } = false;
        
        [StringLength(1000)]
        public string? AdminNotes { get; set; }
    }
}
