using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models.Documents
{
    /// <summary>
    /// Records documents that failed virus scanning and were quarantined.
    /// Provides admin interface to review threats, delete permanently, or override and restore.
    /// Zero false negatives accepted: all threats are quarantined.
    /// </summary>
    public class FileQuarantine
    {
        /// <summary>
        /// Unique quarantine record identifier (auto-increment primary key)
        /// </summary>
        [Key]
        public int QuarantineId { get; set; }

        /// <summary>
        /// Document that was quarantined (foreign key to Document table)
        /// One quarantine record per document (soft constraint, enforced by application)
        /// </summary>
        [Required]
        public int DocumentId { get; set; }

        /// <summary>
        /// Type of threat detected (e.g., "Trojan.Generic", "PUA.Adaware", "Virus.Win32")
        /// Extracted from ClamAV scanning output
        /// </summary>
        [Required]
        [StringLength(255)]
        public string ThreatType { get; set; }

        /// <summary>
        /// When ClamAV completed the scan and detected the threat
        /// </summary>
        [Required]
        public DateTime ScanDate { get; set; }

        /// <summary>
        /// Path where quarantined file is moved: AppData/uploads/{userId}/quarantine/{documentId}_{guid}
        /// Separates infected files from normal storage for safety
        /// </summary>
        [Required]
        [StringLength(500)]
        public string QuarantineStoragePath { get; set; }

        /// <summary>
        /// When admin last reviewed this quarantine record (NULL if not yet reviewed)
        /// </summary>
        public DateTime? AdminReviewDate { get; set; }

        /// <summary>
        /// Admin user who reviewed the quarantine (NULL if not yet reviewed)
        /// </summary>
        [StringLength(255)]
        public string AdminReviewedBy { get; set; }

        /// <summary>
        /// Admin notes on the quarantine (e.g., "False positive - safe file", "Confirmed threat")
        /// </summary>
        [StringLength(1000)]
        public string AdminNotes { get; set; }

        /// <summary>
        /// Action taken: "Kept" (deleted), "Released" (restored and cleared), "Retained" (keeping for analysis)
        /// </summary>
        [StringLength(50)]
        public string Action { get; set; }

        // Navigation properties
        [ForeignKey(nameof(DocumentId))]
        public virtual Document Document { get; set; }
    }
}
