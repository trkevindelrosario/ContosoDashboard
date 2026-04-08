using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models.Documents
{
    /// <summary>
    /// Audit log entry for document operations. Records all user interactions with documents.
    /// Maintains a 7-year audit trail for compliance and troubleshooting.
    /// </summary>
    public class DocumentAccessLog
    {
        /// <summary>
        /// Unique log entry identifier (auto-increment primary key)
        /// </summary>
        [Key]
        public int LogId { get; set; }

        /// <summary>
        /// Document involved in this operation (foreign key to Document table)
        /// </summary>
        [Required]
        public int DocumentId { get; set; }

        /// <summary>
        /// User who performed the operation (foreign key to User table)
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Username for audit trail (captured at operation time for deleted users)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string UserName { get; set; }

        /// <summary>
        /// Type of operation: Upload, Download, Delete, Access, MetadataUpdate
        /// </summary>
        [Required]
        public DocumentOperation Operation { get; set; }

        /// <summary>
        /// When this operation occurred (server timestamp, UTC)
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Client IP address for security analysis
        /// </summary>
        [StringLength(50)]
        public string IpAddress { get; set; }

        /// <summary>
        /// Whether the operation succeeded
        /// Failed operations typically indicate authorization denials or system errors
        /// </summary>
        [Required]
        public bool Success { get; set; }

        /// <summary>
        /// Reason for failure (only populated if Success = false)
        /// Examples: "User not authorized", "Document in quarantine", "Scan pending"
        /// </summary>
        [StringLength(500)]
        public string FailureReason { get; set; }

        // Navigation properties
        [ForeignKey(nameof(DocumentId))]
        public virtual Document Document { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
    }
}
