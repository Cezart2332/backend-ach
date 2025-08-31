using System.Security.Cryptography;
using System.Text;

namespace WebApplication1.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, int locationId, string fileType);
        Task<string> SaveFileAsync(byte[] fileData, string fileName, int locationId, string fileType);
        Task<string> SaveEventFileAsync(IFormFile file, int eventId, string fileType);
        Task<string> SaveEventFileAsync(byte[] fileData, string fileName, int eventId, string fileType);
        Task<string> SaveCompanyCertificateAsync(IFormFile file, int companyId);
        Task<string> SaveCompanyPhotoAsync(IFormFile file, int companyId);
        Task<bool> DeleteFileAsync(string filePath);
        Task<FileInfo?> GetFileInfoAsync(string filePath);
        Task<byte[]?> GetFileAsync(string filePath);
        string GetFileUrl(string filePath);
        Task<bool> EnsureLocationDirectoryAsync(int locationId);
        Task<bool> EnsureEventDirectoryAsync(int eventId);
        Task<bool> EnsureCompanyDirectoryAsync(int companyId);
        Task<bool> DeleteLocationDirectoryAsync(int locationId);
        Task<bool> DeleteEventDirectoryAsync(int eventId);
        Task<bool> DeleteCompanyDirectoryAsync(int companyId);
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
            _baseStoragePath = _configuration["FileStorage:BasePath"] ?? "/app/uploads";
            _baseUrl = _configuration["FileStorage:BaseUrl"] ?? "https://api.acoomh.ro/files";
        }

        public Task<bool> EnsureLocationDirectoryAsync(int locationId)
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

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create directory structure for location {LocationId}", locationId);
                return Task.FromResult(false);
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
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    _logger.LogInformation("Creating directory: {Directory}", directory);
                    Directory.CreateDirectory(directory);
                    
                    // Verify directory was created
                    if (!Directory.Exists(directory))
                    {
                        throw new InvalidOperationException($"Failed to create directory: {directory}");
                    }
                }

                _logger.LogInformation("Saving file to: {FullPath}", fullPath);

                // Save file
                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                    await fileStream.FlushAsync(); // Ensure data is written to disk
                }

                // Verify file was actually created
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"File was not created: {fullPath}");
                }

                var fileInfo = new FileInfo(fullPath);
                _logger.LogInformation("File created successfully: {FullPath}, Size: {Size} bytes", fullPath, fileInfo.Length);

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
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
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

        public Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return Task.FromResult(true);

                var fullPath = Path.Combine(_baseStoragePath, filePath.Replace('/', Path.DirectorySeparatorChar));
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("File deleted: {FilePath}", filePath);
                    return Task.FromResult(true);
                }

                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                return Task.FromResult(true); // Consider missing file as successfully "deleted"
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
                return Task.FromResult(false);
            }
        }

        public Task<FileInfo?> GetFileInfoAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return Task.FromResult<FileInfo?>(null);

                var fullPath = Path.Combine(_baseStoragePath, filePath.Replace('/', Path.DirectorySeparatorChar));
                
                if (File.Exists(fullPath))
                {
                    return Task.FromResult<FileInfo?>(new FileInfo(fullPath));
                }

                return Task.FromResult<FileInfo?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file info: {FilePath}", filePath);
                return Task.FromResult<FileInfo?>(null);
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

        public Task<bool> DeleteLocationDirectoryAsync(int locationId)
        {
            try
            {
                var locationPath = Path.Combine(_baseStoragePath, "locations", locationId.ToString());
                
                if (Directory.Exists(locationPath))
                {
                    Directory.Delete(locationPath, true); // Delete recursively
                    _logger.LogInformation("Deleted directory for location {LocationId}: {Path}", locationId, locationPath);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete directory for location {LocationId}", locationId);
                return Task.FromResult(false);
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

        // Event-specific file storage methods
        public Task<bool> EnsureEventDirectoryAsync(int eventId)
        {
            try
            {
                var eventPath = Path.Combine(_baseStoragePath, "events", eventId.ToString());
                var photosPath = Path.Combine(eventPath, "photos");

                if (!Directory.Exists(photosPath))
                {
                    Directory.CreateDirectory(photosPath);
                    _logger.LogInformation("Created photos directory for event {EventId}: {Path}", eventId, photosPath);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure directory for event {EventId}", eventId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> DeleteEventDirectoryAsync(int eventId)
        {
            try
            {
                var eventPath = Path.Combine(_baseStoragePath, "events", eventId.ToString());
                if (Directory.Exists(eventPath))
                {
                    Directory.Delete(eventPath, true);
                    _logger.LogInformation("Deleted directory for event {EventId}: {Path}", eventId, eventPath);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete directory for event {EventId}", eventId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> EnsureCompanyDirectoryAsync(int companyId)
        {
            try
            {
                var companyPath = Path.Combine(_baseStoragePath, "companies", companyId.ToString());
                var certificatesPath = Path.Combine(companyPath, "certificates");
                var photosPath = Path.Combine(companyPath, "photos");

                if (!Directory.Exists(certificatesPath))
                {
                    Directory.CreateDirectory(certificatesPath);
                    _logger.LogInformation("Created certificates directory for company {CompanyId}: {Path}", companyId, certificatesPath);
                }

                if (!Directory.Exists(photosPath))
                {
                    Directory.CreateDirectory(photosPath);
                    _logger.LogInformation("Created photos directory for company {CompanyId}: {Path}", companyId, photosPath);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure directory for company {CompanyId}", companyId);
                return Task.FromResult(false);
            }
        }

        public Task<bool> DeleteCompanyDirectoryAsync(int companyId)
        {
            try
            {
                var companyPath = Path.Combine(_baseStoragePath, "companies", companyId.ToString());
                if (Directory.Exists(companyPath))
                {
                    Directory.Delete(companyPath, true);
                    _logger.LogInformation("Deleted directory for company {CompanyId}: {Path}", companyId, companyPath);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete directory for company {CompanyId}", companyId);
                return Task.FromResult(false);
            }
        }

        public async Task<string> SaveCompanyCertificateAsync(IFormFile file, int companyId)
        {
            try
            {
                // Ensure directory exists
                await EnsureCompanyDirectoryAsync(companyId);

                // Validate file type for certificates (stricter - PDFs preferred, but allow images as backup)
                var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                {
                    throw new ArgumentException("Only PDF and image files are allowed for certificates");
                }

                // Validate file size (max 10MB)
                if (file.Length > 10 * 1024 * 1024)
                {
                    throw new ArgumentException("Certificate file size cannot exceed 10MB");
                }

                // Generate secure filename with proper extension
                var fileExtension = Path.GetExtension(file.FileName)?.ToLower();
                if (string.IsNullOrEmpty(fileExtension))
                {
                    // Determine extension from content type if missing
                    fileExtension = file.ContentType.ToLower() switch
                    {
                        "application/pdf" => ".pdf",
                        "image/jpeg" => ".jpg",
                        "image/jpg" => ".jpg",
                        "image/png" => ".png",
                        _ => ".pdf" // default
                    };
                }

                var fileName = $"certificate_{DateTime.UtcNow:yyyyMMdd_HHmmss}{fileExtension}";
                var relativePath = Path.Combine("companies", companyId.ToString(), "certificates", fileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);

                // Save file
                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);

                _logger.LogInformation("Saved certificate for company {CompanyId}: {FileName} ({Size} bytes, {ContentType})", 
                    companyId, fileName, file.Length, file.ContentType);

                return relativePath.Replace('\\', '/');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save certificate for company {CompanyId}", companyId);
                throw;
            }
        }

        public async Task<string> SaveCompanyPhotoAsync(IFormFile file, int companyId)
        {
            try
            {
                // Ensure directory exists
                await EnsureCompanyDirectoryAsync(companyId);

                // Validate file type for photos (only images)
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                {
                    throw new ArgumentException("Only image files (JPEG, PNG, GIF, WebP) are allowed for photos");
                }

                // Validate file size (max 5MB for photos)
                if (file.Length > 5 * 1024 * 1024)
                {
                    throw new ArgumentException("Photo file size cannot exceed 5MB");
                }

                // Generate secure filename with proper extension
                var fileExtension = Path.GetExtension(file.FileName)?.ToLower();
                if (string.IsNullOrEmpty(fileExtension))
                {
                    // Determine extension from content type if missing
                    fileExtension = file.ContentType.ToLower() switch
                    {
                        "image/jpeg" => ".jpg",
                        "image/jpg" => ".jpg",
                        "image/png" => ".png",
                        "image/gif" => ".gif",
                        "image/webp" => ".webp",
                        _ => ".jpg" // default
                    };
                }

                var fileName = $"photo_{DateTime.UtcNow:yyyyMMdd_HHmmss}{fileExtension}";
                var relativePath = Path.Combine("companies", companyId.ToString(), "photos", fileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);

                // Save file
                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);

                _logger.LogInformation("Saved photo for company {CompanyId}: {FileName} ({Size} bytes, {ContentType})", 
                    companyId, fileName, file.Length, file.ContentType);

                return relativePath.Replace('\\', '/');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save photo for company {CompanyId}", companyId);
                throw;
            }
        }

        public async Task<string> SaveEventFileAsync(IFormFile file, int eventId, string fileType)
        {
            try
            {
                // Ensure directory exists
                await EnsureEventDirectoryAsync(eventId);

                if (!IsValidFileType(file, fileType))
                {
                    throw new ArgumentException($"Invalid file type for {fileType}");
                }

                // Generate secure filename
                var fileName = GenerateSecureFileName(file.FileName, fileType);
                var relativePath = Path.Combine("events", eventId.ToString(), fileType, fileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);

                // Ensure directory exists for the specific file
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    _logger.LogInformation("Creating directory: {Directory}", directory);
                    Directory.CreateDirectory(directory);
                    
                    // Verify directory was created
                    if (!Directory.Exists(directory))
                    {
                        throw new InvalidOperationException($"Failed to create directory: {directory}");
                    }
                }

                _logger.LogInformation("Saving file to: {FullPath}", fullPath);

                // Save file
                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                    await fileStream.FlushAsync(); // Ensure data is written to disk
                }

                // Verify file was actually created
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"File was not created: {fullPath}");
                }

                var fileInfo = new FileInfo(fullPath);
                _logger.LogInformation("File created successfully: {FullPath}, Size: {Size} bytes", fullPath, fileInfo.Length);

                // Return relative path for database storage
                var dbPath = relativePath.Replace('\\', '/'); // Normalize path separators
                _logger.LogInformation("File saved successfully: {FileName} -> {Path}", file.FileName, dbPath);
                
                return dbPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file {FileName} for event {EventId}", file.FileName, eventId);
                throw;
            }
        }

        public async Task<string> SaveEventFileAsync(byte[] fileData, string fileName, int eventId, string fileType)
        {
            try
            {
                // Ensure directory exists
                await EnsureEventDirectoryAsync(eventId);

                // Generate secure filename
                var secureFileName = GenerateSecureFileName(fileName, fileType);
                var relativePath = Path.Combine("events", eventId.ToString(), fileType, secureFileName);
                var fullPath = Path.Combine(_baseStoragePath, relativePath);

                // Ensure directory exists for the specific file
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
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
                _logger.LogError(ex, "Failed to save file {FileName} for event {EventId}", fileName, eventId);
                throw;
            }
        }
    }
}
