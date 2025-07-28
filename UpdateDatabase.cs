using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1
{
    class UpdateDatabase
    {
        static async Task Main(string[] args)
        {
            var connectionString = "Server=127.0.0.1;Database=acumh;Uid=root;Pwd=root;";
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                .Options;

            using var context = new AppDbContext(options);
            
            try
            {
                Console.WriteLine("Starting database update...");
                
                // First, let's check what columns exist
                var columns = await context.Database.ExecuteSqlRawAsync(@"
                    SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = 'acumh' 
                    AND TABLE_NAME = 'events'");
                
                Console.WriteLine("Current events table columns checked.");
                
                // Add Address column if it doesn't exist
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE events 
                        ADD COLUMN Address TEXT NOT NULL DEFAULT ''");
                    Console.WriteLine("Added Address column");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Duplicate column name"))
                        Console.WriteLine("Address column already exists");
                    else
                        Console.WriteLine($"Error adding Address column: {ex.Message}");
                }

                // Add City column if it doesn't exist
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE events 
                        ADD COLUMN City TEXT NOT NULL DEFAULT ''");
                    Console.WriteLine("Added City column");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Duplicate column name"))
                        Console.WriteLine("City column already exists");
                    else
                        Console.WriteLine($"Error adding City column: {ex.Message}");
                }

                // Add Latitude column if it doesn't exist
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE events 
                        ADD COLUMN Latitude DOUBLE NULL");
                    Console.WriteLine("Added Latitude column");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Duplicate column name"))
                        Console.WriteLine("Latitude column already exists");
                    else
                        Console.WriteLine($"Error adding Latitude column: {ex.Message}");
                }

                // Add Longitude column if it doesn't exist
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE events 
                        ADD COLUMN Longitude DOUBLE NULL");
                    Console.WriteLine("Added Longitude column");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Duplicate column name"))
                        Console.WriteLine("Longitude column already exists");
                    else
                        Console.WriteLine($"Error adding Longitude column: {ex.Message}");
                }

                // Try to remove State column if it exists
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE events 
                        DROP COLUMN State");
                    Console.WriteLine("Removed State column");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Can't DROP"))
                        Console.WriteLine("State column doesn't exist (already removed)");
                    else
                        Console.WriteLine($"Error removing State column: {ex.Message}");
                }

                Console.WriteLine("Database update completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
