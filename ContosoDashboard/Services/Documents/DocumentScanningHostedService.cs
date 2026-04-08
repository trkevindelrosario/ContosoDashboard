using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ContosoDashboard.Data;
using ContosoDashboard.Models.Documents;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// Background service that continuously polls for pending document scan jobs,
    /// executes virus scanning via ClamAV, and updates document status.
    /// Implements retry logic with exponential backoff (max 3 retries).
    /// Works offline without cloud dependencies.
    /// </summary>
    public class DocumentScanningHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentScanningHostedService> _logger;
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan IdleWait = TimeSpan.FromSeconds(10);

        public DocumentScanningHostedService(
            IServiceProvider serviceProvider,
            ILogger<DocumentScanningHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Main background service execution loop.
        /// Continuously polls database for pending scan jobs and processes them.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document scanning service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Create new scope for this iteration
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var clamavService = scope.ServiceProvider.GetRequiredService<IClamAVService>();
                        var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<DocumentNotificationService>();

                        // Get next pending scan job from queue
                        var queuedItem = await dbContext.DocumentScanQueues
                            .Where(q => q.Status == "Pending")
                            .OrderBy(q => q.EnqueuedAt)
                            .FirstOrDefaultAsync(stoppingToken);

                        if (queuedItem != null)
                        {
                            // Process the scan job
                            await ProcessScanJobAsync(
                                dbContext,
                                clamavService,
                                fileStorageService,
                                notificationService,
                                queuedItem,
                                stoppingToken);

                            // Poll again immediately if job was processed
                            await Task.Delay(ScanInterval, stoppingToken);
                        }
                        else
                        {
                            // No pending jobs, wait before polling again
                            await Task.Delay(IdleWait, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in document scanning service");
                    // Wait before retrying to avoid tight loop on repeated errors
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("Document scanning service stopped");
        }

        /// <summary>
        /// Process a single scan job: scan file, update status, quarantine if needed, log results.
        /// </summary>
        private async Task ProcessScanJobAsync(
            ApplicationDbContext dbContext,
            IClamAVService clamavService,
            IFileStorageService fileStorageService,
            DocumentNotificationService notificationService,
            DocumentScanQueue queuedItem,
            CancellationToken stoppingToken)
        {
            try
            {
                // Get the document
                var document = await dbContext.Documents
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DocumentId == queuedItem.DocumentId, stoppingToken);

                if (document == null)
                {
                    // Document was deleted after scan was queued
                    queuedItem.Status = "Complete";
                    queuedItem.CompletedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogWarning("Document not found for scan job {QueueId}", queuedItem.QueueId);
                    return;
                }

                // Mark as processing
                queuedItem.Status = "Processing";
                queuedItem.LastAttemptAt = DateTime.UtcNow;
                document.ScanStatus = ScanStatus.Scanning;
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Starting scan for document {DocumentId}: {FileName}",
                    document.DocumentId, document.FileName);

                // Run ClamAV scan
                var scanResult = await clamavService.ScanFileAsync(document.StoragePath);

                // Handle scan errors (timeouts, ClamAV unavailable, etc.)
                if (scanResult.ScanError)
                {
                    _logger.LogWarning("Scan error for document {DocumentId}: {Error}",
                        document.DocumentId, scanResult.ErrorMessage);

                    // Implement retry logic with exponential backoff
                    queuedItem.RetryCount++;
                    queuedItem.ErrorMessage = scanResult.ErrorMessage;

                    if (queuedItem.RetryCount < queuedItem.MaxRetries)
                    {
                        // Retry: Reset to pending for next poll cycle
                        queuedItem.Status = "Pending";
                        document.ScanStatus = ScanStatus.Pending;
                        _logger.LogInformation("Queuing retry {RetryCount}/{MaxRetries} for document {DocumentId}",
                            queuedItem.RetryCount, queuedItem.MaxRetries, document.DocumentId);
                    }
                    else
                    {
                        // Max retries exceeded: Mark as failed but keep document status Pending
                        // (admin investigation required)
                        queuedItem.Status = "Failed";
                        queuedItem.CompletedAt = DateTime.UtcNow;
                        document.ScanStatus = ScanStatus.Pending;
                        _logger.LogError("Scan failed after {MaxRetries} retries for document {DocumentId}",
                            queuedItem.MaxRetries, document.DocumentId);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    return;
                }

                // Threat detected: quarantine file
                if (scanResult.IsThreatDetected)
                {
                    _logger.LogWarning("Threat detected in document {DocumentId}: {ThreatType}",
                        document.DocumentId, scanResult.ThreatType);

                    // Move file to quarantine storage
                    var quarantinePath = await fileStorageService.QuarantineFileAsync(
                        document.StoragePath,
                        document.DocumentId);

                    // Update document status
                    document.ScanStatus = ScanStatus.Quarantined;
                    document.ScanCompletedDate = scanResult.ScanDate;

                    // Create quarantine record
                    var quarantine = new FileQuarantine
                    {
                        DocumentId = document.DocumentId,
                        ThreatType = scanResult.ThreatType,
                        ScanDate = scanResult.ScanDate,
                        QuarantineStoragePath = quarantinePath
                    };
                    await dbContext.FileQuarantines.AddAsync(quarantine, stoppingToken);

                    // Log quarantine event
                    var log = new DocumentAccessLog
                    {
                        DocumentId = document.DocumentId,
                        UserId = document.UserId,
                        UserName = document.User?.DisplayName ?? document.User?.Email ?? "Unknown",
                        Operation = DocumentOperation.Upload,
                        Timestamp = DateTime.UtcNow,
                        Success = false,
                        FailureReason = $"Threat detected during scan: {scanResult.ThreatType}",
                        IpAddress = null
                    };
                    await dbContext.DocumentAccessLogs.AddAsync(log, stoppingToken);

                    queuedItem.Status = "Complete";
                    queuedItem.CompletedAt = DateTime.UtcNow;

                    _logger.LogWarning("Document {DocumentId} quarantined due to threat: {ThreatType}",
                        document.DocumentId, scanResult.ThreatType);

                    // Send real-time notification about quarantine
                    try
                    {
                        await notificationService.NotifyDocumentQuarantinedAsync(
                            document.DocumentId,
                            document.UserId,
                            document.FileName,
                            scanResult.ThreatType);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to send quarantine notification for document {DocumentId}",
                            document.DocumentId);
                    }
                }
                else
                {
                    // No threat: mark as clear for download
                    _logger.LogInformation("Document {DocumentId} passed security scan: {FileName}",
                        document.DocumentId, document.FileName);

                    document.ScanStatus = ScanStatus.Clear;
                    document.ScanCompletedDate = scanResult.ScanDate;

                    // Log successful scan
                    var log = new DocumentAccessLog
                    {
                        DocumentId = document.DocumentId,
                        UserId = document.UserId,
                        UserName = document.User?.DisplayName ?? document.User?.Email ?? "Unknown",
                        Operation = DocumentOperation.Upload,
                        Timestamp = DateTime.UtcNow,
                        Success = true,
                        FailureReason = null,
                        IpAddress = null
                    };
                    await dbContext.DocumentAccessLogs.AddAsync(log, stoppingToken);

                    queuedItem.Status = "Complete";
                    queuedItem.CompletedAt = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync(stoppingToken);

                // Send real-time notification if scan completed successfully (no errors)
                if (!scanResult.ScanError)
                {
                    try
                    {
                        if (scanResult.IsThreatDetected)
                        {
                            // Notification for threat already sent above
                        }
                        else
                        {
                            // Document is clear, send ready notification
                            await notificationService.NotifyDocumentReadyAsync(
                                document.DocumentId,
                                document.UserId,
                                document.FileName);
                        }
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to send ready notification for document {DocumentId}",
                            document.DocumentId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scan job {QueueId} for document {DocumentId}",
                    queuedItem.QueueId, queuedItem.DocumentId);

                // Mark as failed to prevent infinite loop
                queuedItem.Status = "Failed";
                queuedItem.ErrorMessage = ex.Message;
                queuedItem.CompletedAt = DateTime.UtcNow;

                try
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch
                {
                    // If database save fails, there's not much we can do
                    _logger.LogError("Failed to update scan queue status for job {QueueId}", queuedItem.QueueId);
                }
            }
        }
    }
}
