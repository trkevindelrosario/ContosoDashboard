using System;
using System.IO;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Service interface for file storage operations.
    /// Handles secure storage, quarantine, and retrieval of uploaded files.
    /// Can be implemented locally (disk-based) or cloud-based (Azure Blob).
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Generate unique storage path for uploaded file.
        /// Pattern: AppData/uploads/{userId}/{projectId or "personal"}/{guid}.{extension}
        /// GUID-based naming prevents: path traversal attacks, filename collisions, enumeration.
        /// </summary>
        /// <param name="userId">Uploader user ID</param>
        /// <param name="projectId">Associated project (null for personal)</param>
        /// <param name="originalFileName">Original filename (extension is extracted)</param>
        /// <returns>Full storage path with GUID filename</returns>
        string GenerateStoragePath(int userId, int? projectId, string originalFileName);

        /// <summary>
        /// Save uploaded file to secure storage at specified path.
        /// File MUST be saved outside web root (not in wwwroot/).
        /// Validates path is within storage directory (prevents directory traversal).
        /// </summary>
        /// <param name="filePath">Full storage path (typically from GenerateStoragePath)</param>
        /// <param name="fileStream">File content stream</param>
        /// <returns>True if saved successfully</returns>
        Task<bool> SaveFileAsync(string filePath, Stream fileStream);

        /// <summary>
        /// Retrieve file stream for download.
        /// Opens file in read-only mode from secure storage.
        /// </summary>
        /// <param name="filePath">Full storage path of the file</param>
        /// <returns>FileStream to read file content</returns>
        Task<Stream> GetFileAsync(string filePath);

        /// <summary>
        /// Delete file from storage (permanent removal).
        /// Used when document is deleted or quarantined.
        /// </summary>
        /// <param name="filePath">Full storage path of file to delete</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteFileAsync(string filePath);

        /// <summary>
        /// Move file to quarantine storage when virus is detected.
        /// Separates infected files from normal storage for safety.
        /// Pattern: AppData/uploads/{userId}/quarantine/{documentId}_{guid}
        /// </summary>
        /// <param name="filePath">Current storage path</param>
        /// <param name="documentId">Document ID for quarantine naming</param>
        /// <returns>Quarantine storage path where file moved</returns>
        Task<string> QuarantineFileAsync(string filePath, int documentId);

        /// <summary>
        /// Check if storage directory has sufficient free space.
        /// Prevents "storage full" errors during upload.
        /// </summary>
        /// <param name="requiredBytes">Bytes needed</param>
        /// <returns>True if free space available</returns>
        Task<bool> HasSufficientSpaceAsync(long requiredBytes);

        /// <summary>
        /// Validate that file path is within allowed storage directory.
        /// Prevents directory traversal attacks.
        /// </summary>
        bool IsPathValid(string filePath);
    }
}
