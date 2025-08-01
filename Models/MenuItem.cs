using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("menu_items")]
    public class MenuItem
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("Location")]
        public int LocationId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(255)]
        public string? Description { get; set; }
        
        [Column(TypeName = "decimal(6,2)")]
        public decimal? Price { get; set; }
        
        [MaxLength(50)]
        public string? Category { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Location Location { get; set; } = null!;
    }
}
