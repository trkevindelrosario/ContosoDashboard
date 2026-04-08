namespace ContosoDashboard.Models.Documents
{
    /// <summary>
    /// Document scanning status in the virus scanning workflow
    /// </summary>
    public enum ScanStatus
    {
        Pending = 0,      // Waiting to be scanned
        Scanning = 1,     // Currently being scanned
        Clear = 2,        // Passed security scan, safe for download
        Quarantined = 3   // Failed security scan, isolated in quarantine storage
    }

    /// <summary>
    /// Document categories for organization and filtering
    /// </summary>
    public enum DocumentCategory
    {
        ProjectDocuments = 0,  // Documents associated with a project
        TeamResources = 1,     // Team-wide resource documents
        PersonalFiles = 2,     // Personal/private documents
        Reports = 3,           // Report documents
        Presentations = 4,     // Presentation documents
        Other = 5              // Uncategorized documents
    }

    /// <summary>
    /// Background job processing status for virus scanning queue
    /// </summary>
    public enum ScanQueueStatus
    {
        Pending = 0,    // Waiting to be processed
        Processing = 1, // Currently being processed
        Complete = 2,   // Successfully completed
        Failed = 3      // Failed after max retries
    }

    /// <summary>
    /// Audit log operations for document access tracking
    /// </summary>
    public enum DocumentOperation
    {
        Upload = 0,       // Document uploaded
        Download = 1,     // Document downloaded
        Delete = 2,       // Document deleted
        Access = 3,       // Document accessed/viewed
        MetadataUpdate = 4 // Document metadata updated
    }
}
