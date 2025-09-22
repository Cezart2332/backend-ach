using System;

namespace WebApplication1.Models
{
    public class Friendship
    {
        public int Id { get; set; }

        // Normalize the pair so UserAId < UserBId to enforce uniqueness
        public int UserAId { get; set; }
        public int UserBId { get; set; }

        // Who initiated the request (must be either UserAId or UserBId)
        public int RequestedByUserId { get; set; }

        public bool IsAccepted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }

        // Navigation properties
        public User UserA { get; set; } = null!;
        public User UserB { get; set; } = null!;
    }
}
