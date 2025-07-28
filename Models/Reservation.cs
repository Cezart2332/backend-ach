using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("reservations")]
    public class Reservation
    {
        public int Id { get; set; }

        [Required]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        public string CustomerEmail { get; set; } = string.Empty;

        public string? CustomerPhone { get; set; }

        [Required]
        public DateTime ReservationDate { get; set; }

        [Required]
        public TimeSpan ReservationTime { get; set; }

        [Required]
        [Range(1, 20)]
        public int NumberOfPeople { get; set; }

        public string? SpecialRequests { get; set; }

        [Required]
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? CanceledAt { get; set; }

        public string? CancellationReason { get; set; }

        public string? Notes { get; set; }

        // Foreign key to Location (Required - reservations are for specific locations)
        [Required]
        public int LocationId { get; set; }
        public Location Location { get; set; } = null!;

        // Optional: Reference to user if they have an account
        public int? UserId { get; set; }
        public User? User { get; set; }
    }

    public enum ReservationStatus
    {
        Pending,
        Confirmed,
        Completed,
        Canceled,
        NoShow
    }
}