using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models.Documents
{
    /// <summary>
    /// Represents a virus scanning job in the queue for background processing.
    /// The DocumentScanningHostedService polls this table and processes pending jobs.
    /// Implements retry logic with exponential backoff (max 3 retries).
    /// </summary>
    public class DocumentScanQueue
    {
        /// <summary>
        /// Unique queue entry identifier (auto-increment primary key)
        /// </summary>
        [Key]
        public int QueueId { get; set; }

        /// <summary>
        /// Document to be scanned (foreign key to Document table)
        /// </summary>
        [Required]
        public int DocumentId { get; set; }

        /// <summary>
        /// When this job was enqueued (server timestamp, UTC)
        /// </summary>
        [Required]
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of scan attempts already made (incremented on each retry)
        /// </summary>
        [Required]
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Maximum retry attempts allowed (default 3)
        /// </summary>
        [Required]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Current job status: Pending, Processing, Complete, Failed
        /// Pending: waiting to be processed
        /// Processing: currently being scanned
        /// Complete: scan finished (success or threat found)
        /// Failed: failed after max retries
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Error message if scan failed (only populated if Status = Failed or error during processing)
        /// </summary>
        [StringLength(500)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// When the last scan attempt was made (NULL initially, updated before each attempt)
        /// Used to calculate exponential backoff: wait = 2 ^ RetryCount seconds
        /// </summary>
        public DateTime? LastAttemptAt { get; set; }

        /// <summary>
        /// When the job completed successfully (populated when Status = Complete or Failed)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(DocumentId))]
        public virtual Document Document { get; set; }
    }
}
