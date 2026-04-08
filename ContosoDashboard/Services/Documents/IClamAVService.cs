using System;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Service interface for antivirus scanning.
    /// Can be implemented locally (command-line clamscan) or cloud-based (Azure security services).
    /// </summary>
    public interface IClamAVService
    {
        /// <summary>
        /// Scan a file for viruses and malware.
        /// Returns result indicating if threats were detected.
        /// </summary>
        /// <param name="filePath">Path to file on disk</param>
        /// <returns>ScanResult with threat detection info</returns>
        Task<ScanResult> ScanFileAsync(string filePath);
    }

    /// <summary>
    /// Result of a virus/malware scan.
    /// Contains threat detection status and details for audit/admin review.
    /// </summary>
    public class ScanResult
    {
        /// <summary>
        /// Whether a threat (virus/malware/PUA) was detected in the file.
        /// False = file is clean and safe for download.
        /// True = file detected as threat and should be quarantined.
        /// </summary>
        public bool IsThreatDetected { get; set; }

        /// <summary>
        /// Type of threat detected (only populated if IsThreatDetected = true).
        /// Examples: "Trojan.Generic", "PUA.Adaware", "Virus.Win32.Malexp"
        /// Null/empty if file is clean.
        /// </summary>
        public string ThreatType { get; set; }

        /// <summary>
        /// When the scan completed (server timestamp, UTC).
        /// </summary>
        public DateTime ScanDate { get; set; }

        /// <summary>
        /// Raw scanner output for logging/debugging.
        /// Full ClamAV output for reference purposes.
        /// </summary>
        public string RawOutput { get; set; }

        /// <summary>
        /// Error message if scan failed (timeout, ClamAV unavailable, etc).
        /// Null/empty if scan succeeded.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Whether the scan encountered an error (timeout, service unavailable, file read error).
        /// If true, caller should retry the scan job.
        /// </summary>
        public bool ScanError { get; set; }
    }
}
