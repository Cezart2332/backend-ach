using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public interface IDataMigrationService
    {
        Task MigrateLocationFilesToStorageAsync();
        Task<int> GetPendingMigrationCountAsync();
    }

    public class DataMigrationService : IDataMigrationService
    {
        private readonly AppDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<DataMigrationService> _logger;

        public DataMigrationService(
            AppDbContext context,
            IFileStorageService fileStorageService,
            ILogger<DataMigrationService> logger)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        public async Task<int> GetPendingMigrationCountAsync()
        {
            try
            {
                var count = await _context.Locations
                    .Where(l => (l.Photo.Length > 0 && string.IsNullOrEmpty(l.PhotoPath)) ||
                               (l.MenuData.Length > 0 && string.IsNullOrEmpty(l.MenuPath)))
                    .CountAsync();

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending migration count");
                return 0;
            }
        }

        public async Task MigrateLocationFilesToStorageAsync()
        {
            try
            {
                _logger.LogInformation("Starting location files migration to file storage...");

                // Get locations that need migration (have binary data but no file paths)
                var locationsToMigrate = await _context.Locations
                    .Where(l => (l.Photo.Length > 0 && string.IsNullOrEmpty(l.PhotoPath)) ||
                               (l.MenuData.Length > 0 && string.IsNullOrEmpty(l.MenuPath)))
                    .ToListAsync();

                _logger.LogInformation("Found {Count} locations that need file migration", locationsToMigrate.Count);

                foreach (var location in locationsToMigrate)
                {
                    try
                    {
                        bool updated = false;

                        // Migrate photo if exists
                        if (location.Photo != null && location.Photo.Length > 0 && string.IsNullOrEmpty(location.PhotoPath))
                        {
                            var photoFileName = $"migrated_photo_{location.Id}.jpg";
                            var photoPath = await _fileStorageService.SaveFileAsync(
                                location.Photo, 
                                photoFileName, 
                                location.Id, 
                                "photos"
                            );

                            location.PhotoPath = photoPath;
                            updated = true;
                            _logger.LogInformation("Migrated photo for location {LocationId} to {PhotoPath}", location.Id, photoPath);
                        }

                        // Migrate menu if exists
                        if (location.MenuData != null && location.MenuData.Length > 0 && string.IsNullOrEmpty(location.MenuPath))
                        {
                            var menuFileName = !string.IsNullOrEmpty(location.MenuName) 
                                ? location.MenuName 
                                : $"migrated_menu_{location.Id}.pdf";

                            var menuPath = await _fileStorageService.SaveFileAsync(
                                location.MenuData,
                                menuFileName,
                                location.Id,
                                "menus"
                            );

                            location.MenuPath = menuPath;
                            updated = true;
                            _logger.LogInformation("Migrated menu for location {LocationId} to {MenuPath}", location.Id, menuPath);
                        }

                        if (updated)
                        {
                            location.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to migrate files for location {LocationId}", location.Id);
                        // Continue with other locations even if one fails
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Completed location files migration. Migrated {Count} locations", locationsToMigrate.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate location files");
                throw;
            }
        }
    }
}
