using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using System.Security.Claims;

namespace ContosoDashboard.Services
{
    /// <summary>
    /// Service for handling document uploads in Blazor Server components.
    /// This service bridges the gap between Blazor components and server-side services,
    /// allowing components to call upload logic without making HTTP requests.
    /// </summary>
    public interface IDocumentUploadService
    {
        /// <summary>
        /// Upload a file to a project or as a personal document
        /// File is stored and queued for virus scanning before becoming accessible
        /// </summary>
        Task<DocumentUploadResult> UploadAsync(
            int userId,
            int? projectId,
            string title,
            string? description,
            string category,
            List<string> tags,
            IFormFile file);
    }

    public class DocumentUploadService : IDocumentUploadService
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentUploadService> _logger;

        public DocumentUploadService(
            IDocumentService documentService,
            ILogger<DocumentUploadService> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a file to a project or as a personal document
        /// File is stored and queued for virus scanning before becoming accessible
        /// </summary>
        public async Task<DocumentUploadResult> UploadAsync(
            int userId,
            int? projectId,
            string title,
            string? description,
            string category,
            List<string> tags,
            IFormFile file)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(title))
                {
                    return new DocumentUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Document title is required"
                    };
                }

                if (string.IsNullOrWhiteSpace(category))
                {
                    return new DocumentUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Document category is required"
                    };
                }

                if (file == null || file.Length == 0)
                {
                    return new DocumentUploadResult
                    {
                        Success = false,
                        ErrorMessage = "No file was provided"
                    };
                }

                // Call the document service to handle upload
                var documentId = await _documentService.UploadAsync(
                    userId,
                    projectId,
                    title.Trim(),
                    description?.Trim() ?? string.Empty,
                    category,
                    tags,
                    file);

                if (documentId <= 0)
                {
                    _logger.LogWarning($"Upload failed for user {userId}: DocumentService returned invalid ID");
                    return new DocumentUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to process document upload"
                    };
                }

                _logger.LogInformation($"Document uploaded successfully. DocumentId: {documentId}, UserId: {userId}, ProjectId: {projectId}, FileName: {file.FileName}");

                return new DocumentUploadResult
                {
                    Success = true,
                    DocumentId = documentId,
                    Message = "Document uploaded successfully and queued for virus scanning",
                    FileName = file.FileName
                };
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Upload validation error for user {userId}: {ex.Message}");
                return new DocumentUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Upload argument error for user {userId}: {ex.Message}");
                return new DocumentUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"Unauthorized upload attempt by user {userId}: {ex.Message}");
                return new DocumentUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Authorization failed: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during document upload for user {userId}: {ex.Message}. StackTrace: {ex.StackTrace}");
                return new DocumentUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Upload failed: {ex.Message}",
                    ErrorDetails = ex.ToString()
                };
            }
        }
    }

    /// <summary>
    /// Result of a document upload operation
    /// </summary>
    public class DocumentUploadResult
    {
        public bool Success { get; set; }
        public int DocumentId { get; set; }
        public string? Message { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetails { get; set; }
    }
}
