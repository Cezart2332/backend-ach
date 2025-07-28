using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1
{
    public class AddAddressColumns
    {
        public static async Task ExecuteAsync(AppDbContext context)
        {
            // Check if columns already exist before adding them
            var addressColumnExists = await context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'acumh' 
                AND TABLE_NAME = 'events' 
                AND COLUMN_NAME = 'Address'");

            if (addressColumnExists == 0)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE events 
                    ADD COLUMN Address TEXT NOT NULL DEFAULT ''");
            }

            var cityColumnExists = await context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'acumh' 
                AND TABLE_NAME = 'events' 
                AND COLUMN_NAME = 'City'");

            if (cityColumnExists == 0)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE events 
                    ADD COLUMN City TEXT NOT NULL DEFAULT ''");
            }

            var latitudeColumnExists = await context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'acumh' 
                AND TABLE_NAME = 'events' 
                AND COLUMN_NAME = 'Latitude'");

            if (latitudeColumnExists == 0)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE events 
                    ADD COLUMN Latitude DOUBLE NULL");
            }

            var longitudeColumnExists = await context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'acumh' 
                AND TABLE_NAME = 'events' 
                AND COLUMN_NAME = 'Longitude'");

            if (longitudeColumnExists == 0)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE events 
                    ADD COLUMN Longitude DOUBLE NULL");
            }

            // Also remove State column if it exists
            var stateColumnExists = await context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = 'acumh' 
                AND TABLE_NAME = 'events' 
                AND COLUMN_NAME = 'State'");

            if (stateColumnExists > 0)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE events 
                    DROP COLUMN State");
            }

            Console.WriteLine("Address columns added/updated successfully!");
        }
    }
}
