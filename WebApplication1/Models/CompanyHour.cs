namespace WebApplication1.Models;
public enum DayOfWeekEnum
{
    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public class CompanyHour
{
    public int Id { get; set; }

    public DayOfWeekEnum DayOfWeek { get; set; }
    
    public bool Is24Hours { get; set; }

    public TimeSpan? OpenTime { get; set; }

    public TimeSpan? CloseTime { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; }
}

