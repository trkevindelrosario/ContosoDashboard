using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ContosoDashboard.Models;

namespace ContosoDashboard.Models.Documents
{
    /// <summary>
    /// Represents an uploaded document with metadata, access control, and scanning status.
    /// Documents can be associated with a Project (nullable) or stored as Personal files.
    /// All documents are scanned for viruses before becoming available for download.
    /// </summary>
    public class Document
    {
        /// <summary>
        /// Unique identifier for the document (auto-increment primary key)
        /// </summary>
        [Key]
        public int DocumentId { get; set; }

        /// <summary>
        /// User who uploaded the document (required, foreign key to User table)
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Associated project (optional, nullable foreign key for personal documents)
        /// </summary>
        public int? ProjectId { get; set; }

        /// <summary>
        /// Document title provided by uploader (1-255 characters, required)
        /// </summary>
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Title { get; set; }

        /// <summary>
        /// Optional description of document purpose/content (max 1000 characters)
        /// </summary>
        [StringLength(1000)]
        public string Description { get; set; }

        /// <summary>
        /// Category for document organization and filtering
        /// </summary>
        [Required]
        public DocumentCategory Category { get; set; } = DocumentCategory.Other;

        /// <summary>
        /// Comma-separated tags for document organization (max 5 tags, max 500 chars)
        /// </summary>
        [StringLength(500)]
        public string Tags { get; set; }

        /// <summary>
        /// Original filename as provided by uploader (for user-facing display)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        /// <summary>
        /// Server storage path: AppData/uploads/{userId}/{projectId or "personal"}/{guid}.{extension}
        /// GUID-based naming prevents path traversal attacks and handles filename collisions
        /// </summary>
        [Required]
        [StringLength(500)]
        public string StoragePath { get; set; }

        /// <summary>
        /// File size in bytes (used for quota validation and performance estimation)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// MIME type (e.g., "application/pdf", "image/jpeg") derived from file content
        /// </summary>
        [Required]
        [StringLength(255)]
        public string MimeType { get; set; }

        /// <summary>
        /// Current virus scanning status (Pending, Scanning, Clear, or Quarantined)
        /// Documents are NOT available for download until ScanStatus = Clear
        /// </summary>
        [Required]
        public ScanStatus ScanStatus { get; set; } = ScanStatus.Pending;

        /// <summary>
        /// When the ClamAV virus scan completed (NULL if pending/scanning)
        /// </summary>
        public DateTime? ScanCompletedDate { get; set; }

        /// <summary>
        /// When the file was uploaded (server timestamp, UTC)
        /// </summary>
        [Required]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Audit timestamp: when this record was created (UTC)
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Audit timestamp: when this record was last updated (UTC)
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual Project Project { get; set; }

        /// <summary>
        /// Audit trail of all operations (uploads, downloads, access attempts, deletions)
        /// </summary>
        public virtual ICollection<DocumentAccessLog> AccessLogs { get; set; } = new List<DocumentAccessLog>();

        /// <summary>
        /// Quarantine record if document failed security scan (0 or 1 per document)
        /// </summary>
        public virtual FileQuarantine Quarantine { get; set; }

        /// <summary>
        /// Scan queue entry for background job processing (if currently queued)
        /// </summary>
        public virtual DocumentScanQueue ScanQueue { get; set; }
    }
}
