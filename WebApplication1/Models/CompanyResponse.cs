namespace WebApplication1.Models;

public class CompanyResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    
    public required string Description { get; set; }
    
    public int Cui {get; set;}
    public required string Category { get; set; }
}