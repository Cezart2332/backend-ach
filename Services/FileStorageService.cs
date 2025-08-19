using System.Security.Cryptography;
using System.Text;

namespace WebApplication1.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, int locationId, string fileType);
        Task<string> SaveFileAsync(byte[] fileData, string fileName, int locationId, string fileType);
        Task<bool> DeleteFileAsync(string filePath);
        Task<FileInfo?> GetFileInfoAsync(string filePath);
        Task<byte[]?> GetFileAsync(string filePath);
        string GetFileUrl(string filePath);
        Task<bool> EnsureLocationDirectoryAsync(int locationId);
        Task<bool> DeleteLocationDirectoryAsync(int locationId);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileStorageService> _logger;
        private readonly string _baseStoragePath;
        private readonly string _baseUrl;

        public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _baseStoragePath = _configuration["FileStorage:BasePath"] ?? "/var/www/uploads";
            _baseUrl = _configuration["FileStorage:BaseUrl"] ?? "https://api.acoomh.ro/files";
        }

        public async Task<bool> EnsureLocationDirectoryAsync(int locationId)
        {
            try
            {
                var locationPath = Path.Combine(_baseStoragePath, "locations", locationId.ToString());
                var photosPath = Path.Combine(locationPath, "photos");
                var menusPath = Path.Combine(locationPath, "menus");

                if (!Directory.Exists(photosPath))
                {
                    Directory.CreateDirectory(photosPath);
                    _logger.LogInformation("Created photos directory for location {LocationId}: {Path}", locationId, photosPath);
                }

                if (!Directory.Exists(menusPath))
                {
                    Directory.CreateDirectory(menusPath);
                    _logger.LogInformation("Created menus directory for location {LocationId}: {Path}", locationId, menusPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create directory structure for location {LocationId}", locationId);
                return false;
            }
        }

        public async Task<string> SaveFileAsync(IFormFile file, int locationId, string fileType)
        {
            try
            {
                // Ensure directory exists
                await EnsureLocationDirectoryAsync(locationId);

                // Validate file type
                if (!IsValidFileType(file, fileType))
                {
                    throw new ArgumentException($"Invalid file type for {fileType}");
                }

                // Generate secure filename
                var fileName = GenerateSecureFileName(file.FileName, fileType);
                var relativePath = Path.Combine("locations", locationId.ToString(), fileType, fileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);

                // Ensure directory exists for the specific file
                var directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save file
                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // Return relative path for database storage
                var dbPath = relativePath.Replace('\\', '/'); // Normalize path separators
                _logger.LogInformation("File saved successfully: {FileName} -> {Path}", file.FileName, dbPath);
                
                return dbPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file {FileName} for location {LocationId}", file.FileName, locationId);
                throw;
            }
        }

        public async Task<string> SaveFileAsync(byte[] fileData, string fileName, int locationId, string fileType)
        {
            try
            {
                // Ensure directory exists
                await EnsureLocationDirectoryAsync(locationId);

                // Generate secure filename
                var secureFileName = GenerateSecureFileName(fileName, fileType);
                var relativePath = Path.Combine("locations", locationId.ToString(), fileType, secureFileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);

                // Ensure directory exists for the specific file
                var directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save file
                await File.WriteAllBytesAsync(fullPath, fileData);

                // Return relative path for database storage
                var dbPath = relativePath.Replace('\\', '/'); // Normalize path separators
                _logger.LogInformation("File saved successfully: {FileName} -> {Path}", fileName, dbPath);
                
                return dbPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file {FileName} for location {LocationId}", fileName, locationId);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return true;

                var fullPath = Path.Combine(_baseStoragePath, filePath.Replace('/', Path.DirectorySeparatorChar));
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("File deleted: {FilePath}", filePath);
                    return true;
                }

                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                return true; // Consider missing file as successfully "deleted"
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<FileInfo?> GetFileInfoAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return null;

                var fullPath = Path.Combine(_baseStoragePath, filePath.Replace('/', Path.DirectorySeparatorChar));
                
                if (File.Exists(fullPath))
                {
                    return new FileInfo(fullPath);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file info: {FilePath}", filePath);
                return null;
            }
        }

        public async Task<byte[]?> GetFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return null;

                var fullPath = Path.Combine(_baseStoragePath, filePath.Replace('/', Path.DirectorySeparatorChar));
                
                if (File.Exists(fullPath))
                {
                    return await File.ReadAllBytesAsync(fullPath);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read file: {FilePath}", filePath);
                return null;
            }
        }

        public string GetFileUrl(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            return $"{_baseUrl}/{filePath}";
        }

        public async Task<bool> DeleteLocationDirectoryAsync(int locationId)
        {
            try
            {
                var locationPath = Path.Combine(_baseStoragePath, "locations", locationId.ToString());
                
                if (Directory.Exists(locationPath))
                {
                    Directory.Delete(locationPath, true); // Delete recursively
                    _logger.LogInformation("Deleted directory for location {LocationId}: {Path}", locationId, locationPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete directory for location {LocationId}", locationId);
                return false;
            }
        }

        private bool IsValidFileType(IFormFile file, string fileType)
        {
            var allowedPhotoTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            var allowedMenuTypes = new[] { "application/pdf", "image/jpeg", "image/jpg", "image/png" };

            return fileType.ToLower() switch
            {
                "photos" => allowedPhotoTypes.Contains(file.ContentType?.ToLower()),
                "menus" => allowedMenuTypes.Contains(file.ContentType?.ToLower()),
                _ => false
            };
        }

        private string GenerateSecureFileName(string originalFileName, string fileType)
        {
            // Extract extension
            var extension = Path.GetExtension(originalFileName)?.ToLower() ?? "";
            
            // Generate unique identifier
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var randomPart = GenerateRandomString(8);
            
            // Create secure filename
            var secureFileName = $"{fileType}_{timestamp}_{randomPart}{extension}";
            
            return secureFileName;
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            using var rng = RandomNumberGenerator.Create();
            var result = new StringBuilder(length);
            var buffer = new byte[1];

            for (int i = 0; i < length; i++)
            {
                rng.GetBytes(buffer);
                result.Append(chars[buffer[0] % chars.Length]);
            }

            return result.ToString();
        }
    }
}
