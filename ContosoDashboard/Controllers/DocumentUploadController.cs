using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Models;
using System.Security.Claims;

namespace ContosoDashboard.Controllers
{
    /// <summary>
    /// Controller for handling document upload operations
    /// Handles multipart file uploads with metadata for documents to be scanned
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentUploadController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentUploadController> _logger;

        public DocumentUploadController(
            IDocumentService documentService,
            ILogger<DocumentUploadController> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a file to a project or as a personal document
        /// File is stored and queued for virus scanning before becoming accessible
        /// </summary>
        /// <param name="projectId">Optional project ID; if null, document is stored as personal</param>
        /// <param name="title">Document title (1-255 characters)</param>
        /// <param name="description">Document description (optional)</param>
        /// <param name="category">Document category (ProjectDocuments, PersonalFiles, etc.)</param>
        /// <param name="tags">Comma-separated document tags (max 5 tags)</param>
        /// <param name="file">The file to upload (max 25MB)</param>
        /// <returns>DocumentId if successful, error message otherwise</returns>
        [HttpPost("upload")]
        [RequestSizeLimit(26214400)] // 25MB
        public async Task<IActionResult> UploadFileAsync(
            [FromForm] int? projectId,
            [FromForm] string title,
            [FromForm] string? description,
            [FromForm] string category,
            [FromForm] string? tags,
            [FromForm] IFormFile file)
        {
            try
            {
                // Get current user ID from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Upload attempted with invalid or missing user ID");
                    return Unauthorized(new { error = "User identity could not be determined" });
                }

                // Validate file was provided
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file was provided" });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(title))
                {
                    return BadRequest(new { error = "Document title is required" });
                }

                if (string.IsNullOrWhiteSpace(category))
                {
                    return BadRequest(new { error = "Document category is required" });
                }

                // Create upload request
                var uploadRequest = new DocumentUploadRequest
                {
                    UserId = userId,
                    ProjectId = projectId,
                    Title = title.Trim(),
                    Description = description?.Trim(),
                    Category = category,
                    Tags = string.IsNullOrWhiteSpace(tags) ? new List<string>() : tags.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList(),
                    File = file
                };

                // Call the document service to handle upload
                var documentId = await _documentService.UploadAsync(
                    uploadRequest.UserId,
                    uploadRequest.ProjectId,
                    uploadRequest.Title,
                    uploadRequest.Description,
                    uploadRequest.Category,
                    uploadRequest.Tags,
                    uploadRequest.File);

                if (documentId <= 0)
                {
                    _logger.LogWarning($"Upload failed for user {userId}: DocumentService returned invalid ID");
                    return StatusCode(500, new { error = "Failed to process document upload" });
                }

                _logger.LogInformation($"Document uploaded successfully. DocumentId: {documentId}, UserId: {userId}, ProjectId: {projectId}, FileName: {file.FileName}");

                return Ok(new
                {
                    success = true,
                    documentId = documentId,
                    message = "Document uploaded successfully and queued for virus scanning",
                    fileName = file.FileName
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Upload validation error for user {User.FindFirst(ClaimTypes.NameIdentifier)?.Value}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Upload argument error for user {User.FindFirst(ClaimTypes.NameIdentifier)?.Value}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during document upload: {ex.Message}");
                return StatusCode(500, new
                {
                    error = "An unexpected error occurred while processing your upload",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get upload status/progress for a document being scanned
        /// </summary>
        [HttpGet("status/{documentId}")]
        public async Task<IActionResult> GetUploadStatusAsync(int documentId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized();
                }

                var document = await _documentService.GetByIdAsync(documentId, userId);
                if (document == null)
                {
                    return NotFound(new { error = "Document not found or access denied" });
                }

                return Ok(new
                {
                    documentId = document.DocumentId,
                    title = document.Title,
                    scanStatus = document.ScanStatus.ToString(),
                    fileName = document.FileName,
                    fileSize = document.FileSize,
                    uploadDate = document.UploadDate,
                    scanCompletedDate = document.ScanCompletedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting upload status for document {documentId}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve upload status" });
            }
        }
    }

    /// <summary>
    /// Request model for document upload containing file and metadata
    /// </summary>
    public class DocumentUploadRequest
    {
        public int UserId { get; set; }
        public int? ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public IFormFile? File { get; set; }
    }
}
