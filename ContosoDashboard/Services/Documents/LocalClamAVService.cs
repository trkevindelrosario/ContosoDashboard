using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Local implementation of antivirus scanning using ClamAV command-line tool.
    /// Executes clamscan command and parses output to detect threats.
    /// This implementation works offline without external cloud services.
    /// </summary>
    public class LocalClamAVService : IClamAVService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LocalClamAVService> _logger;
        private readonly string _clamavPath;
        private readonly int _scanTimeoutSeconds;

        public LocalClamAVService(IConfiguration configuration, ILogger<LocalClamAVService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // Read ClamAV configuration
            _clamavPath = _configuration["ClamAV:Path"] ?? "clamscan";
            _scanTimeoutSeconds = int.Parse(_configuration["ClamAV:TimeoutSeconds"] ?? "60");
        }

        /// <summary>
        /// Scan file for viruses using clamscan.
        /// Executes: clamscan --max-recursion=1000 {filePath}
        /// Exit codes:
        /// 0 = clean (no threats)
        /// 1 = threat found
        /// >1 = error (file not found, ClamAV error, etc.)
        /// </summary>
        public async Task<ScanResult> ScanFileAsync(string filePath)
        {
            try
            {
                // Validate file exists
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for scanning: {FilePath}", filePath);
                    return new ScanResult
                    {
                        IsThreatDetected = false,
                        ErrorMessage = "File not found",
                        ScanError = true,
                        ScanDate = DateTime.UtcNow,
                        RawOutput = null
                    };
                }

                // Build clamscan command
                var arguments = $"--max-recursion=1000 \"{filePath}\"";
                
                _logger.LogInformation("Starting ClamAV scan: {FilePath}", filePath);

                // Execute clamscan
                var processInfo = new ProcessStartInfo
                {
                    FileName = _clamavPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    // Wait for process with timeout
                    bool completedInTime = process.WaitForExit(_scanTimeoutSeconds * 1000);

                    if (!completedInTime)
                    {
                        process.Kill();
                        _logger.LogError("ClamAV scan timed out after {TimeoutSeconds} seconds: {FilePath}",
                            _scanTimeoutSeconds, filePath);
                        
                        return new ScanResult
                        {
                            IsThreatDetected = false,
                            ErrorMessage = $"Scan timeout after {_scanTimeoutSeconds} seconds",
                            ScanError = true,
                            ScanDate = DateTime.UtcNow,
                            RawOutput = null
                        };
                    }

                    // Read output and error
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    // Parse exit code
                    var exitCode = process.ExitCode;
                    var isThreatFound = exitCode == 1;
                    var threatType = ExtractThreatType(output);

                    var result = new ScanResult
                    {
                        IsThreatDetected = isThreatFound,
                        ThreatType = isThreatFound ? threatType : null,
                        ScanDate = DateTime.UtcNow,
                        RawOutput = output,
                        ScanError = false
                    };

                    if (isThreatFound)
                    {
                        _logger.LogWarning("Threat detected in {FilePath}: {ThreatType}", 
                            filePath, threatType);
                    }
                    else if (exitCode == 0)
                    {
                        _logger.LogInformation("File passed security scan: {FilePath}", filePath);
                    }
                    else
                    {
                        _logger.LogError("ClamAV scan error (exit code {ExitCode}): {FilePath}. Error: {Error}",
                            exitCode, filePath, error);
                        result.ScanError = true;
                        result.ErrorMessage = $"ClamAV error (exit code {exitCode})";
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during ClamAV scan: {FilePath}", filePath);
                return new ScanResult
                {
                    IsThreatDetected = false,
                    ErrorMessage = $"Scan failed: {ex.Message}",
                    ScanError = true,
                    ScanDate = DateTime.UtcNow,
                    RawOutput = null
                };
            }
        }

        /// <summary>
        /// Extract threat type from ClamAV output.
        /// Output format: "/path/to/file: Trojan.Generic.5 FOUND"
        /// Extracts the threat signature (e.g., "Trojan.Generic.5")
        /// </summary>
        private string ExtractThreatType(string clamavOutput)
        {
            if (string.IsNullOrEmpty(clamavOutput))
                return "Unknown.Threat";

            // Match pattern: "filename: ThreatName FOUND"
            var match = Regex.Match(clamavOutput, @":\s+([^\s]+)\s+FOUND", RegexOptions.Multiline);
            
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            // Fallback: look for any threat pattern
            match = Regex.Match(clamavOutput, @"FOUND", RegexOptions.IgnoreCase);
            return match.Success ? "Unknown.Threat" : null;
        }
    }
}
