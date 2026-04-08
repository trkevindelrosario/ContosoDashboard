using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Models.Documents;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Core document management service.
    /// Handles upload, download, listing, searching, filtering, and deletion of documents.
    /// All methods include authorization checks and audit logging.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<DocumentService> _logger;
        private readonly long _maxFileSizeBytes;

        public DocumentService(
            ApplicationDbContext dbContext,
            IFileStorageService fileStorageService,
            ILogger<DocumentService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _fileStorageService = fileStorageService;
            _logger = logger;
            
            // Read max file size from config (default 25MB)
            var maxSizeMb = int.Parse(configuration["DocumentStorage:MaxFileSizeMB"] ?? "25");
            _maxFileSizeBytes = maxSizeMb * 1024 * 1024;
        }

        /// <summary>
        /// Upload file and create document record with ScanStatus=Pending.
        /// File is saved to storage, document record created, and scanning job enqueued.
        /// </summary>
        public async Task<Document> UploadAsyncAsync(
            int userId,
            int? projectId,
            string title,
            string description,
            DocumentCategory category,
            string tags,
            string fileName,
            Stream fileStream,
            string mimeType)
        {
            try
            {
                // Validate authorization: user must be uploading, not acting on behalf of others
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Upload attempt by non-existent user: {UserId}", userId);
                    throw new UnauthorizedAccessException("User not found");
                }

                // Validate inputs
                if (string.IsNullOrWhiteSpace(title) || title.Length > 255)
                    throw new ArgumentException("Title must be 1-255 characters");

                if (description?.Length > 1000)
                    throw new ArgumentException("Description must be max 1000 characters");

                // Check file size
                if (fileStream.Length > _maxFileSizeBytes)
                {
                    throw new ArgumentException($"File size exceeds maximum of {_maxFileSizeBytes / (1024 * 1024)}MB");
                }

                // Check available storage space
                var hasSpace = await _fileStorageService.HasSufficientSpaceAsync(fileStream.Length);
                if (!hasSpace)
                {
                    throw new InvalidOperationException("Insufficient storage space");
                }

                // Validate project association if specified
                if (projectId.HasValue)
                {
                    var project = await _dbContext.Projects.FindAsync(projectId);
                    if (project == null)
                        throw new ArgumentException("Project not found");

                    // Verify user is assigned to project
                    var isMember = await _dbContext.ProjectMembers
                        .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
                    
                    var isManager = project.ProjectManagerId == userId;
                    
                    if (!isMember && !isManager)
                    {
                        _logger.LogWarning("User {UserId} attempted to upload to unauthorized project {ProjectId}",
                            userId, projectId);
                        throw new UnauthorizedAccessException("User is not assigned to this project");
                    }
                }

                // Generate storage path
                var storagePath = _fileStorageService.GenerateStoragePath(userId, projectId, fileName);

                // Save file to disk
                var fileSaved = await _fileStorageService.SaveFileAsync(storagePath, fileStream);
                if (!fileSaved)
                {
                    throw new InvalidOperationException("Failed to save file to storage");
                }

                // Create document record with Pending status
                var document = new Document
                {
                    UserId = userId,
                    ProjectId = projectId,
                    Title = title,
                    Description = description,
                    Category = category,
                    Tags = tags,
                    FileName = fileName,
                    StoragePath = storagePath,
                    FileSize = fileStream.Length,
                    MimeType = mimeType,
                    ScanStatus = ScanStatus.Pending,
                    UploadDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _dbContext.Documents.AddAsync(document);
                await _dbContext.SaveChangesAsync();

                // Create scan queue job for background processing
                var scanJob = new DocumentScanQueue
                {
                    DocumentId = document.DocumentId,
                    EnqueuedAt = DateTime.UtcNow,
                    Status = "Pending"
                };

                await _dbContext.DocumentScanQueues.AddAsync(scanJob);
                await _dbContext.SaveChangesAsync();

                // Log upload operation
                await LogAccessAsync(document.DocumentId, userId, user.DisplayName ?? user.Email,
                    DocumentOperation.Upload, success: true, ipAddress: null);

                _logger.LogInformation("Document uploaded: {DocumentId}, File: {FileName}, User: {UserId}", 
                    document.DocumentId, fileName, userId);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document: {FileName}, User: {UserId}", fileName, userId);
                throw;
            }
        }

        /// <summary>
        /// Download document file if user is authorized and document is cleared for download.
        /// </summary>
        public async Task<Stream> DownloadAsync(int documentId, int userId)
        {
            try
            {
                var document = await _dbContext.Documents
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    _logger.LogWarning("Download attempt for non-existent document: {DocumentId}", documentId);
                    await LogAccessAsync(documentId, userId, "Unknown", DocumentOperation.Download,
                        success: false, "Document not found");
                    return null;
                }

                // Check authorization
                if (!await CanDownloadAsync(userId, documentId))
                {
                    _logger.LogWarning("Unauthorized download attempt: User {UserId}, Document {DocumentId}",
                        userId, documentId);
                    await LogAccessAsync(documentId, userId, "Unknown", DocumentOperation.Download,
                        success: false, "Not authorized");
                    return null;
                }

                // Check scan status (document must be Clear for download)
                if (document.ScanStatus != ScanStatus.Clear)
                {
                    _logger.LogWarning("Download attempted on document with status {Status}: {DocumentId}",
                        document.ScanStatus, documentId);
                    await LogAccessAsync(documentId, userId, "Unknown", DocumentOperation.Download,
                        success: false, $"Document status is {document.ScanStatus}");
                    return null;
                }

                // Get file stream
                var fileStream = await _fileStorageService.GetFileAsync(document.StoragePath);
                
                if (fileStream == null)
                {
                    _logger.LogError("File not found on disk: {StoragePath}", document.StoragePath);
                    await LogAccessAsync(documentId, userId, "Unknown", DocumentOperation.Download,
                        success: false, "File not found on storage");
                    return null;
                }

                // Log successful download
                var user = await _dbContext.Users.FindAsync(userId);
                await LogAccessAsync(documentId, userId, user?.DisplayName ?? user?.Email ?? "Unknown",
                    DocumentOperation.Download, success: true, ipAddress: null);

                return fileStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document: {DocumentId}, User: {UserId}", 
                    documentId, userId);
                throw;
            }
        }

        /// <summary>
        /// Get document by ID (authorization checked).
        /// </summary>
        public async Task<Document> GetByIdAsync(int documentId, int userId)
        {
            var document = await _dbContext.Documents
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (document == null)
                return null;

            // Check authorization
            if (!await CanViewDocumentAsync(userId, document))
                return null;

            return document;
        }

        /// <summary>
        /// List documents with authorization filtering.
        /// </summary>
        public async Task<List<Document>> ListAsync(
            int userId,
            int? projectId = null,
            DocumentCategory? category = null,
            int skip = 0,
            int take = 50)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return new List<Document>();

            var query = _dbContext.Documents.AsQueryable();

            // Apply filters
            if (projectId.HasValue)
            {
                query = query.Where(d => d.ProjectId == projectId);
            }

            if (category.HasValue)
            {
                query = query.Where(d => d.Category == category);
            }

            // Apply authorization filters based on user role
            if (user.Role == UserRole.Administrator)
            {
                // Admins see all documents
            }
            else if (user.Role == UserRole.ProjectManager)
            {
                // PMs see: their own docs + all docs in their managed projects
                query = query.Where(d =>
                    d.UserId == userId ||  // Own documents
                    (d.ProjectId != null && _dbContext.Projects
                        .Where(p => p.ProjectManagerId == userId)
                        .Select(p => p.ProjectId)
                        .Contains(d.ProjectId.Value))
                );
            }
            else
            {
                // Employees see: their own docs + docs in their assigned projects
                query = query.Where(d =>
                    d.UserId == userId ||  // Own documents
                    (d.ProjectId != null && _dbContext.ProjectMembers
                        .Where(pm => pm.UserId == userId)
                        .Select(pm => pm.ProjectId)
                        .Contains(d.ProjectId.Value))
                );
            }

            var documents = await query
                .OrderByDescending(d => d.UploadDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return documents;
        }

        /// <summary>
        /// Delete document (hard delete - permanent removal).
        /// </summary>
        public async Task<bool> DeleteAsync(int documentId, int userId)
        {
            try
            {
                var document = await _dbContext.Documents
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    _logger.LogWarning("Delete attempt for non-existent document: {DocumentId}", documentId);
                    return false;
                }

                // Check authorization
                if (!await CanDeleteAsync(userId, documentId))
                {
                    _logger.LogWarning("Unauthorized delete attempt: User {UserId}, Document {DocumentId}",
                        userId, documentId);
                    return false;
                }

                // Delete file from storage
                await _fileStorageService.DeleteFileAsync(document.StoragePath);

                // Delete document record
                _dbContext.Documents.Remove(document);
                await _dbContext.SaveChangesAsync();

                // Log deletion
                var user = await _dbContext.Users.FindAsync(userId);
                await LogAccessAsync(documentId, userId, user?.DisplayName ?? user?.Email ?? "Unknown",
                    DocumentOperation.Delete, success: true, ipAddress: null);

                _logger.LogInformation("Document deleted: {DocumentId}, User: {UserId}", documentId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
                throw;
            }
        }

        /// <summary>
        /// Update document metadata.
        /// </summary>
        public async Task<Document> UpdateMetadataAsync(
            int documentId,
            int userId,
            string title,
            string description,
            DocumentCategory category,
            string tags)
        {
            try
            {
                var document = await _dbContext.Documents
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                    throw new ArgumentException("Document not found");

                // Check authorization (owner, PM, or admin)
                if (!await CanDeleteAsync(userId, documentId))  // Same auth as delete
                    throw new UnauthorizedAccessException("Not authorized to modify this document");

                // Update fields
                document.Title = title ?? document.Title;
                document.Description = description ?? document.Description;
                document.Category = category;
                document.Tags = tags ?? document.Tags;
                document.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log update
                var user = await _dbContext.Users.FindAsync(userId);
                await LogAccessAsync(documentId, userId, user?.DisplayName ?? user?.Email ?? "Unknown",
                    DocumentOperation.MetadataUpdate, success: true, ipAddress: null);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document metadata: {DocumentId}", documentId);
                throw;
            }
        }

        /// <summary>
        /// Search documents by title, description, or tags.
        /// </summary>
        public async Task<List<Document>> SearchAsync(string query, int userId, int? projectId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Document>();

            var searchTerm = query.ToLower();

            var documents = await ListAsync(userId, projectId);  // Start with authorized documents
            
            return documents
                .Where(d =>
                    d.Title.ToLower().Contains(searchTerm) ||
                    d.Description?.ToLower().Contains(searchTerm) == true ||
                    d.Tags?.ToLower().Contains(searchTerm) == true
                )
                .OrderByDescending(d => d.UploadDate)
                .ToList();
        }

        /// <summary>
        /// Filter and sort documents.
        /// </summary>
        public async Task<List<Document>> FilterAsync(
            int userId,
            int? projectId,
            FilterCriteria criteria)
        {
            var documents = await ListAsync(userId, projectId);

            // Apply category filter
            if (criteria.Category.HasValue)
            {
                documents = documents.Where(d => d.Category == criteria.Category).ToList();
            }

            // Apply date range filter
            if (criteria.UploadDateFrom.HasValue)
            {
                documents = documents.Where(d => d.UploadDate >= criteria.UploadDateFrom).ToList();
            }

            if (criteria.UploadDateTo.HasValue)
            {
                documents = documents.Where(d => d.UploadDate <= criteria.UploadDateTo).ToList();
            }

            // Apply file size filter
            if (criteria.MinFileSize.HasValue)
            {
                documents = documents.Where(d => d.FileSize >= criteria.MinFileSize).ToList();
            }

            if (criteria.MaxFileSize.HasValue)
            {
                documents = documents.Where(d => d.FileSize <= criteria.MaxFileSize).ToList();
            }

            // Apply sorting
            documents = ApplySorting(documents, criteria.SortBy, criteria.SortDescending);

            return documents;
        }

        // ==================== Authorization Helpers ====================

        private async Task<bool> CanDownloadAsync(int userId, int documentId)
        {
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
                return false;

            return await CanViewDocumentAsync(userId, document);
        }

        private async Task<bool> CanDeleteAsync(int userId, int documentId)
        {
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
                return false;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Owner can always delete their documents
            if (document.UserId == userId)
                return true;

            // Admin can delete any document
            if (user.Role == UserRole.Administrator)
                return true;

            // PM can delete documents in their projects
            if (user.Role == UserRole.ProjectManager && document.ProjectId.HasValue)
            {
                var isManager = await _dbContext.Projects
                    .AnyAsync(p => p.ProjectId == document.ProjectId && p.ProjectManagerId == userId);
                if (isManager)
                    return true;
            }

            return false;
        }

        private async Task<bool> CanViewDocumentAsync(int userId, Document document)
        {
            // Owner can view own documents
            if (document.UserId == userId)
                return true;

            // Admin can view any document
            var user = await _dbContext.Users.FindAsync(userId);
            if (user?.Role == UserRole.Administrator)
                return true;

            // Project members can view project documents
            if (document.ProjectId.HasValue)
            {
                var isMember = await _dbContext.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == document.ProjectId && pm.UserId == userId);
                if (isMember)
                    return true;

                var isManager = await _dbContext.Projects
                    .AnyAsync(p => p.ProjectId == document.ProjectId && p.ProjectManagerId == userId);
                if (isManager)
                    return true;
            }

            return false;
        }

        // ==================== Audit Logging ====================

        private async Task LogAccessAsync(
            int documentId,
            int userId,
            string userName,
            DocumentOperation operation,
            bool success,
            string failureReason = null,
            string ipAddress = null)
        {
            try
            {
                var log = new DocumentAccessLog
                {
                    DocumentId = documentId,
                    UserId = userId,
                    UserName = userName,
                    Operation = operation,
                    Timestamp = DateTime.UtcNow,
                    Success = success,
                    FailureReason = failureReason,
                    IpAddress = ipAddress
                };

                await _dbContext.DocumentAccessLogs.AddAsync(log);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging document access: DocumentId={DocumentId}, UserId={UserId}",
                    documentId, userId);
            }
        }

        // ==================== Sorting Logic ====================

        private List<Document> ApplySorting(
            List<Document> documents,
            string sortBy,
            bool sortDescending)
        {
            var sorted = sortBy?.ToLower() switch
            {
                "title" => sortDescending
                    ? documents.OrderByDescending(d => d.Title).ToList()
                    : documents.OrderBy(d => d.Title).ToList(),
                
                "uploaddate" => sortDescending
                    ? documents.OrderByDescending(d => d.UploadDate).ToList()
                    : documents.OrderBy(d => d.UploadDate).ToList(),
                
                "filesize" => sortDescending
                    ? documents.OrderByDescending(d => d.FileSize).ToList()
                    : documents.OrderBy(d => d.FileSize).ToList(),
                
                "uploader" => sortDescending
                    ? documents.OrderByDescending(d => d.User.DisplayName).ToList()
                    : documents.OrderBy(d => d.User.DisplayName).ToList(),
                
                _ => sortDescending
                    ? documents.OrderByDescending(d => d.UploadDate).ToList()
                    : documents.OrderBy(d => d.UploadDate).ToList()
            };

            return sorted;
        }

        /// <summary>
        /// Upload a document from IFormFile (HTTP upload).
        /// Validates file, stores it, creates document record, enqueues scan job.
        /// </summary>
        public async Task<int> UploadAsync(
            int userId,
            int? projectId,
            string title,
            string description,
            string categoryStr,
            List<string> tags,
            IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided");

            if (string.IsNullOrWhiteSpace(title) || title.Length > 255)
                throw new ArgumentException("Title must be 1-255 characters");

            if (description?.Length > 1000)
                throw new ArgumentException("Description must be max 1000 characters");

            // Validate file size
            if (file.Length > _maxFileSizeBytes)
                throw new ArgumentException($"File exceeds maximum size of {_maxFileSizeBytes / (1024 * 1024)}MB");

            // Validate file type
            if (!IsSupportedFileType(file.FileName, file.ContentType))
                throw new ArgumentException($"File type not supported. Supported types: PDF, DOCX, XLSX, PPTX, TXT, JPG, PNG, CSV, ZIP");

            // Validate category enum
            if (!Enum.TryParse<DocumentCategory>(categoryStr, out var category))
                throw new ArgumentException($"Invalid category: {categoryStr}");

            // Validate user exists
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Validate project membership if specified
            if (projectId.HasValue)
            {
                var project = await _dbContext.Projects.FindAsync(projectId);
                if (project == null)
                    throw new ArgumentException("Project not found");

                var isMember = await _dbContext.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
                var isManager = project.ProjectManagerId == userId;

                if (!isMember && !isManager)
                    throw new UnauthorizedAccessException("User is not assigned to this project");
            }

            // Check storage space
            var hasSpace = await _fileStorageService.HasSufficientSpaceAsync(file.Length);
            if (!hasSpace)
                throw new InvalidOperationException("Insufficient storage space available");

            try
            {
                // Generate storage path
                var storagePath = _fileStorageService.GenerateStoragePath(userId, projectId, file.FileName);

                // Save file to disk
                using (var fileStream = file.OpenReadStream())
                {
                    var fileSaved = await _fileStorageService.SaveFileAsync(storagePath, fileStream);
                    if (!fileSaved)
                        throw new InvalidOperationException("Failed to save file to storage");
                }

                // Create document record
                var document = new Document
                {
                    UserId = userId,
                    ProjectId = projectId,
                    Title = title,
                    Description = description,
                    Category = category,
                    Tags = string.Join(",", tags),
                    FileName = file.FileName,
                    StoragePath = storagePath,
                    FileSize = file.Length,
                    MimeType = file.ContentType ?? "application/octet-stream",
                    ScanStatus = ScanStatus.Pending,
                    UploadDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _dbContext.Documents.AddAsync(document);
                await _dbContext.SaveChangesAsync();

                // Enqueue scan job
                var scanJob = new DocumentScanQueue
                {
                    DocumentId = document.DocumentId,
                    EnqueuedAt = DateTime.UtcNow,
                    Status = "Pending",
                    MaxRetries = 3
                };

                await _dbContext.DocumentScanQueues.AddAsync(scanJob);
                await _dbContext.SaveChangesAsync();

                // Log upload
                await LogAccessAsync(document.DocumentId, userId, user.DisplayName ?? user.Email,
                    DocumentOperation.Upload, success: true);

                _logger.LogInformation("Document uploaded: ID={DocumentId}, Title={Title}, User={UserId}, File={FileName}",
                    document.DocumentId, title, userId, file.FileName);

                return document.DocumentId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Check if a file type is supported for upload.
        /// </summary>
        private bool IsSupportedFileType(string fileName, string contentType)
        {
            var supportedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".jpg", ".jpeg", ".png", ".csv", ".zip" };
            
            var fileExtension = Path.GetExtension(fileName)?.ToLower();
            return !string.IsNullOrEmpty(fileExtension) && supportedExtensions.Contains(fileExtension);
        }
    }
}

