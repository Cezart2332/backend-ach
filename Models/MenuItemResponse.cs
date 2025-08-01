namespace WebApplication1.Models
{
    public class MenuItemResponse
    {
        public int Id { get; set; }
        public int LocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
