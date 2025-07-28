namespace WebApplication1.Models
{
    public class CreateReservationRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public DateTime ReservationDate { get; set; }
        public TimeSpan ReservationTime { get; set; }
        public int NumberOfPeople { get; set; }
        public string? SpecialRequests { get; set; }
        public int LocationId { get; set; }
    }

    public class UpdateReservationRequest
    {
        public int Id { get; set; }
        public ReservationStatus Status { get; set; }
        public string? Notes { get; set; }
        public string? CancellationReason { get; set; }
    }

    public class ReservationResponse
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public DateTime ReservationDate { get; set; }
        public TimeSpan ReservationTime { get; set; }
        public int NumberOfPeople { get; set; }
        public string? SpecialRequests { get; set; }
        public ReservationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CanceledAt { get; set; }
        public string? CancellationReason { get; set; }
        public string? Notes { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int? UserId { get; set; }
    }

    public class ReservationSummaryResponse
    {
        public int TotalReservations { get; set; }
        public int PendingReservations { get; set; }
        public int ConfirmedReservations { get; set; }
        public int CompletedReservations { get; set; }
        public int CanceledReservations { get; set; }
        public int NoShowReservations { get; set; }
    }
}