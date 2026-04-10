using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Local disk-based file storage service.
    /// Stores files securely outside web root with GUID-based naming for security.
    /// Supports future cloud migration via interface abstraction.
    /// </summary>
    public class FileStorageService : IFileStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileStorageService> _logger;
        private readonly string _storageDirectory;

        public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // Read storage directory from configuration
            _storageDirectory = _configuration["DocumentStorage:Directory"] ?? "AppData/uploads";
            
            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirectory);
        }

        /// <summary>
        /// Generate unique storage path with GUID-based filename.
        /// Pattern: {storageDirectory}/{userId}/{projectId or "personal"}/{guid}.{extension}
        /// </summary>
        public string GenerateStoragePath(int userId, int? projectId, string originalFileName)
        {
            // Extract file extension from original filename
            var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLower();
            
            // Validate extension is allowed
            var allowedExtensions = GetAllowedExtensions();
            if (!allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"File extension '{extension}' is not allowed");
            }

            // Build path components
            var userFolder = Path.Combine(_storageDirectory, userId.ToString());
            var projectFolder = projectId.HasValue ? projectId.ToString() : "personal";
            var folder = Path.Combine(userFolder, projectFolder);

            // Generate unique filename with GUID
            var uniqueFileName = $"{Guid.NewGuid()}.{extension}";
            var fullPath = Path.Combine(folder, uniqueFileName);

            _logger.LogDebug("Generated storage path for {OriginalFileName}: {StoragePath}", 
                originalFileName, fullPath);

            return fullPath;
        }

        /// <summary>
        /// Save file to secure storage.
        /// Creates parent directories if needed, validates path is within storage directory.
        /// </summary>
        public async Task<bool> SaveFileAsync(string filePath, Stream fileStream)
        {
            try
            {
                // Validate path is within storage directory (prevent directory traversal)
                if (!IsPathValid(filePath))
                {
                    _logger.LogWarning("Attempted to save file outside storage directory: {FilePath}", filePath);
                    throw new UnauthorizedAccessException("File path is outside allowed storage directory");
                }

                // Ensure parent directory exists
                var directory = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directory);

                // Save file
                using (var fileStreamDest = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await fileStream.CopyToAsync(fileStreamDest);
                }

                _logger.LogInformation("File saved successfully: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Retrieve file stream for download.
        /// Opens file in read-only mode.
        /// </summary>
        public async Task<Stream> GetFileAsync(string filePath)
        {
            try
            {
                // Validate path
                if (!IsPathValid(filePath))
                {
                    throw new UnauthorizedAccessException("File path is outside allowed storage directory");
                }

                // Check file exists
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                    return null;
                }

                // Return file stream (caller is responsible for disposing)
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _logger.LogDebug("File stream opened: {FilePath}", filePath);
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Delete file from storage (hard delete, no recovery).
        /// </summary>
        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                // Validate path
                if (!IsPathValid(filePath))
                {
                    throw new UnauthorizedAccessException("File path is outside allowed storage directory");
                }

                // Delete file if exists
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("File deleted: {FilePath}", filePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Move file to quarantine storage when virus is detected.
        /// </summary>
        public async Task<string> QuarantineFileAsync(string filePath, int documentId)
        {
            try
            {
                // Validate original path
                if (!IsPathValid(filePath))
                {
                    throw new UnauthorizedAccessException("File path is invalid");
                }

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for quarantine: {FilePath}", filePath);
                    return null;
                }

                // Determine user ID from path
                var pathParts = filePath.Split(Path.DirectorySeparatorChar);
                var userFolder = pathParts.Length > 0 ? pathParts[pathParts.Length - 3] : "unknown";

                // Create quarantine path
                var quarantineFolder = Path.Combine(_storageDirectory, userFolder, "quarantine");
                Directory.CreateDirectory(quarantineFolder);

                var quarantineFileName = $"{documentId}_{Guid.NewGuid()}.quarantine";
                var quarantinePath = Path.Combine(quarantineFolder, quarantineFileName);

                // Move file to quarantine
                File.Move(filePath, quarantinePath, overwrite: true);
                _logger.LogInformation("File quarantined: {OriginalPath} -> {QuarantinePath}", 
                    filePath, quarantinePath);

                return quarantinePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quarantining file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Check available free space in storage directory.
        /// </summary>
        public async Task<bool> HasSufficientSpaceAsync(long requiredBytes)
        {
            try
            {
                // On non-Windows/Unix platforms, DriveInfo needs an absolute path or mount point.
                // Resolve the storage directory to a full path first.
                var absolutePath = Path.GetFullPath(_storageDirectory);
                
                // On Unix-like systems (macOS/Linux), DriveInfo can take any path and 
                // it will find the mount point for it, but it must be absolute.
                var drive = new DriveInfo(absolutePath);
                var available = drive.AvailableFreeSpace;
                var hasSufficientSpace = available >= requiredBytes;

                if (!hasSufficientSpace)
                {
                    _logger.LogWarning("Insufficient storage space. Required: {Required} bytes, Available: {Available} bytes",
                        requiredBytes, available);
                }

                return hasSufficientSpace;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking storage space for directory: {Directory}", _storageDirectory);
                // If we can't check space, we'll assume there is space but log the error
                // This prevents blocking uploads when DriveInfo fails on certain environments
                return true;
            }
        }

        /// <summary>
        /// Validate path is within allowed storage directory (prevent directory traversal).
        /// </summary>
        public bool IsPathValid(string filePath)
        {
            try
            {
                // Get full paths to prevent .. traversal
                var fullPath = Path.GetFullPath(filePath);
                var storageDirFull = Path.GetFullPath(_storageDirectory);

                // Ensure path is within storage directory
                return fullPath.StartsWith(storageDirFull + Path.DirectorySeparatorChar) || fullPath.StartsWith(storageDirFull);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get list of allowed file extensions.
        /// </summary>
        private HashSet<string> GetAllowedExtensions()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Documents
                "pdf", "docx", "doc", "xlsx", "xls", "pptx", "ppt", "txt", "csv",
                // Images
                "jpg", "jpeg", "png",
                // Additional common formats
                "zip", "rar", "7z"
            };
        }
    }
}
