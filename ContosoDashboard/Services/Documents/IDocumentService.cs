using ContosoDashboard.Models.Documents;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Service interface for document management operations.
    /// Defines contracts for upload, download, modification, and querying of documents.
    /// All methods include authorization enforcement at the service layer.
    /// </summary>
    public interface IDocumentService
    {
        /// <summary>
        /// Upload a file and create document record with metadata.
        /// File is saved to secure storage immediately.
        /// Document is marked as Pending and enqueued for virus scanning.
        /// Returns DocumentId to caller.
        /// </summary>
        /// <param name="userId">User uploading the document</param>
        /// <param name="projectId">Optional project association (null for personal documents)</param>
        /// <param name="title">Document title (1-255 chars)</param>
        /// <param name="description">Optional description</param>
        /// <param name="category">Document category</param>
        /// <param name="tags">Optional comma-separated tags</param>
        /// <param name="fileName">Original filename</param>
        /// <param name="fileStream">File content stream</param>
        /// <param name="mimeType">MIME type from file content</param>
        /// <returns>Created Document with DocumentId</returns>
        Task<Document> UploadAsyncAsync(
            int userId, 
            int? projectId, 
            string title, 
            string description, 
            DocumentCategory category, 
            string tags, 
            string fileName, 
            Stream fileStream, 
            string mimeType);

        /// <summary>
        /// Download a document file if user has authorization and document is cleared for download.
        /// Validates user permissions, checks scan status, logs access attempt.
        /// </summary>
        /// <param name="documentId">Document to download</param>
        /// <param name="userId">User requesting download</param>
        /// <returns>File stream if authorized and clear, null if unauthorized/unavailable</returns>
        Task<Stream> DownloadAsync(int documentId, int userId);

        /// <summary>
        /// Get document by ID with all metadata.
        /// Returns null if document not found or user lacks authorization.
        /// </summary>
        Task<Document> GetByIdAsync(int documentId, int userId);

        /// <summary>
        /// List documents matching filter criteria and user authorization.
        /// Applies role-based access control: employees see own docs + project docs,
        /// PMs see all project docs, admins see all docs.
        /// </summary>
        /// <param name="userId">Current user (for authorization filtering)</param>
        /// <param name="projectId">Filter to project (null = personal documents only when userScoped = true)</param>
        /// <param name="category">Optional category filter</param>
        /// <param name="skip">Pagination offset</param>
        /// <param name="take">Pagination size</param>
        /// <returns>List of documents user is authorized to view</returns>
        Task<List<Document>> ListAsync(
            int userId, 
            int? projectId = null, 
            DocumentCategory? category = null, 
            int skip = 0, 
            int take = 50);

        /// <summary>
        /// Delete a document (hard delete - no recovery).
        /// Removes file from storage and document record from database.
        /// Only owner or admin can delete.
        /// </summary>
        /// <param name="documentId">Document to delete</param>
        /// <param name="userId">User requesting deletion</param>
        /// <returns>True if deleted successfully, false if unauthorized</returns>
        Task<bool> DeleteAsync(int documentId, int userId);

        /// <summary>
        /// Update document metadata (title, description, category, tags).
        /// Only owner or PM (if project doc) or admin can update.
        /// </summary>
        Task<Document> UpdateMetadataAsync(
            int documentId, 
            int userId, 
            string title, 
            string description, 
            DocumentCategory category, 
            string tags);

        /// <summary>
        /// Search documents by title, description, or tags.
        /// Applies authorization filters (only returns accessible documents).
        /// </summary>
        /// <param name="query">Search term to match against title/description/tags</param>
        /// <param name="userId">Current user (for authorization)</param>
        /// <param name="projectId">Optional project scope</param>
        /// <returns>Matching documents user is authorized to view</returns>
        Task<List<Document>> SearchAsync(string query, int userId, int? projectId = null);

        /// <summary>
        /// Filter and sort documents by multiple criteria.
        /// Supports category, date range, uploader (if PM), file size.
        /// </summary>
        Task<List<Document>> FilterAsync(
            int userId, 
            int? projectId, 
            FilterCriteria criteria);
    }

    /// <summary>
    /// Filter criteria for advanced document queries.
    /// All parameters are optional (null = no filter).
    /// </summary>
    public class FilterCriteria
    {
        public DocumentCategory? Category { get; set; }
        public DateTime? UploadDateFrom { get; set; }
        public DateTime? UploadDateTo { get; set; }
        public string SortBy { get; set; } = "UploadDate"; // Title, UploadDate, FileSize, Uploader
        public bool SortDescending { get; set; } = true;
        public int? UploaderUserId { get; set; }
        public long? MinFileSize { get; set; }
        public long? MaxFileSize { get; set; }
    }
}
