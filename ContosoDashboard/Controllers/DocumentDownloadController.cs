using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ContosoDashboard.Services.Documents;
using System.Security.Claims;
using System.Net.Mime;

namespace ContosoDashboard.Controllers
{
    /// <summary>
    /// Controller for handling document download operations.
    /// Enforces authorization checks and scan status validation before streaming files.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentDownloadController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentDownloadController> _logger;

        public DocumentDownloadController(
            IDocumentService documentService,
            ILogger<DocumentDownloadController> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        /// <summary>
        /// Download a document file if authorized and scan status is Clear.
        /// Streams file with appropriate Content-Type header and Content-Disposition for downloading.
        /// </summary>
        /// <param name="documentId">ID of document to download</param>
        /// <returns>File stream with Content-Type and Content-Disposition headers</returns>
        [HttpGet("download/{documentId}")]
        public async Task<IActionResult> DownloadAsync(int documentId)
        {
            try
            {
                // Get current user ID from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Download attempted with invalid or missing user ID");
                    return Unauthorized(new { error = "User identity could not be determined" });
                }

                // Get document with authorization check
                var document = await _documentService.GetByIdAsync(documentId, userId);
                if (document == null)
                {
                    _logger.LogWarning($"Download denied: Document {documentId} not found or user {userId} not authorized");
                    return NotFound(new { error = "Document not found or access denied" });
                }

                // Check scan status - document must be Clear for download
                if (document.ScanStatus.ToString() != "Clear")
                {
                    _logger.LogWarning($"Download denied: Document {documentId} scan status is {document.ScanStatus}");
                    return BadRequest(new
                    {
                        error = $"Document is not available for download. Status: {document.ScanStatus}",
                        status = document.ScanStatus.ToString(),
                        message = GetStatusMessage(document.ScanStatus.ToString())
                    });
                }

                // Get file stream from service
                var fileStream = await _documentService.DownloadAsync(documentId, userId);
                if (fileStream == null)
                {
                    _logger.LogError($"File stream null for document {documentId} after authorization passed");
                    return StatusCode(500, new { error = "Failed to read file from storage" });
                }

                _logger.LogInformation($"Document downloaded successfully. DocumentId: {documentId}, UserId: {userId}, FileName: {document.FileName}");

                // Return file with proper headers
                return File(
                    fileStream,
                    GetContentType(document.MimeType),
                    document.FileName,
                    enableRangeProcessing: true
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error downloading document {documentId}: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred while downloading the file" });
            }
        }

        /// <summary>
        /// Get document metadata including scan status without downloading the file.
        /// Useful for UI to determine if file is ready for download.
        /// </summary>
        [HttpGet("info/{documentId}")]
        public async Task<IActionResult> GetDocumentInfoAsync(int documentId)
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

                var canDownload = document.ScanStatus.ToString() == "Clear";

                return Ok(new
                {
                    documentId = document.DocumentId,
                    title = document.Title,
                    fileName = document.FileName,
                    fileSize = document.FileSize,
                    mimeType = document.MimeType,
                    uploadDate = document.UploadDate,
                    scanStatus = document.ScanStatus.ToString(),
                    scanCompletedDate = document.ScanCompletedDate,
                    canDownload = canDownload,
                    statusMessage = GetStatusMessage(document.ScanStatus.ToString())
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting document info for {documentId}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve document information" });
            }
        }

        /// <summary>
        /// Get proper MIME type based on file extension or provided MIME type.
        /// Ensures browser handles file correctly.
        /// </summary>
        private string GetContentType(string? mimeType)
        {
            if (!string.IsNullOrEmpty(mimeType))
            {
                return mimeType;
            }

            return "application/octet-stream"; // Default fallback
        }

        /// <summary>
        /// Get user-friendly message for document status.
        /// </summary>
        private string GetStatusMessage(string status)
        {
            return status switch
            {
                "Pending" => "Document is pending virus scan. Please wait...",
                "Scanning" => "Document is currently being scanned for viruses...",
                "Clear" => "Document is ready for download",
                "Quarantined" => "Document contains a threat and has been quarantined",
                _ => "Document status is unknown"
            };
        }
    }
}
