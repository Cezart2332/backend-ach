using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class BugReportDto
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255, MinimumLength = 5)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(2000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string DeviceType { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? DeviceInfo { get; set; }
    }
    
    public class BugReportResponse
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string? DeviceInfo { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsResolved { get; set; }
        public string? AdminNotes { get; set; }
    }
}
