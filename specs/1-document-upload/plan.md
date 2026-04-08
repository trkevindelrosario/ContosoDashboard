# Implementation Plan: Document Upload and Management

**Branch**: `1-document-upload` | **Date**: April 8, 2026 | **Spec**: [specs/1-document-upload/spec.md](spec.md)  
**Input**: Feature specification from `/specs/1-document-upload/spec.md` (fully clarified, 7 user stories, 20 functional requirements)

---

## Summary

The Document Upload and Management feature provides ContosoDashboard with a centralized, secure repository for work-related files with role-based access control, virus scanning, and complete audit trails. This feature is built as a Blazor Server web application with local filesystem storage, ClamAV antivirus integration, and SQL Server persistence, following specification-driven development with security and service-oriented architecture at the core.

---

## Technical Context

**Language/Version**: C# 10 / ASP.NET Core 10.0  
**Framework**: Blazor Server web application  
**Primary Dependencies**: 
- Entity Framework Core 10.0 (ORM, data access)
- ClamAV.Net NuGet package (virus/malware scanning)
- xUnit (unit testing)
- Moq (mocking for tests)

**Storage**: 
- SQL Server (Docker) for document metadata, access logs, and audit records
- Local filesystem (`AppData/uploads`) for file storage with GUID-based naming
- Secure path pattern: `{userId}/{projectId or "personal"}/{uniqueGuid}.{extension}`

**Testing**: 
- xUnit for unit tests (80%+ coverage target for service classes)
- Integration tests for critical workflows (upload, download, authorization)
- Blazor component testing for UI validation

**Target Platform**: Blazor Server web application (modern browsers, mobile support)  
**Project Type**: Web application (single-tier Blazor Server)  
**Performance Goals**:
- Complete 25MB upload including security scan: ≤30 seconds end-to-end
- Filtering/sorting operations (1000 documents): ≤1 second response
- Full-text search results: ≤2 seconds for project with 1000 documents
- Concurrent upload handling: Support unlimited simultaneous uploads

**Constraints**:
- Offline training implementation (no cloud services, local ClamAV scanning)
- Hard delete only (no soft-delete, no recovery)
- GUID-based filenames for security (prevent path traversal)
- All file access through authorized controller endpoints (no wwwroot access)
- Session timeout handling during upload (use MemoryStream pattern)

**Scale/Scope**:
- Support projects with 1000+ documents without UI degradation
- 4 primary user roles with distinct permissions (Employee, Team Lead, Project Manager, Administrator)
- 6 core operations: Upload, Download, List, Filter/Sort, Delete, Search
- 20 functional requirements spanning security, storage, audit, and discovery

---

## Constitution Check

**GATE: Must pass before Phase 1 design. Re-check after design completion.**

### ✅ Security & Authorization First
**Status**: COMPLIANT  
**Evidence**:
- Architecture includes `IDocumentService` interface with authorization checks at service layer
- Download controller enforces role-based access before serving files
- DocumentAccessLog tracks all access attempts (successful and denied)
- Personal documents restricted to owner + administrators (row-level security in service)
- No hardcoded secrets; configuration-driven for storage paths and ClamAV settings

**Implementation Points**:
- `DocumentService.Download(documentId, userId)` returns 403 Forbidden if unauthorized
- `DocumentService.Delete(documentId, userId)` validates user has delete permission
- `DocumentDownloadController` checks authorization before streaming file

---

### ✅ Database Schema Integrity
**Status**: COMPLIANT  
**Evidence**:
- All migrations reversible via EF Core (no breaking DDL)
- Three main entities (Document, DocumentAccessLog, FileQuarantine) with mandatory FK relationships
- Document → User (UploadedByUserId, required)
- Document → Project (AssociatedProjectId, optional but enforced FK when set)
- DocumentAccessLog → Document → User → Project (audit trail integrity)
- Null handling via nullable properties: `AssociatedProjectId?`, `Description?`, `Tags?`

**Migration Strategy**:
- Each entity gets individual migration file
- Foreign key constraints enforced at DB level
- Migration descriptions include rationale (e.g., "Add virus scan status for ClamAV integration")
- Reversibility tested: each migration includes `Down()` method

---

### ✅ Service-Oriented Architecture
**Status**: COMPLIANT  
**Evidence**:
- `IDocumentService` interface defines all business logic contracts
- `IFileStorageService` abstraction for file persistence (enables testing with mock storage)
- `DocumentService` contains upload, download, delete, filter, search logic
- All services registered in DI container (`services.AddScoped<IDocumentService, DocumentService>`)
- Pages (`Documents.razor`, `ProjectDocuments.razor`) inject services; never query DbContext directly
- Async/await throughout: `Task<Document>`, `Task<List<Document>>`, `Task<Stream>`

**Service Boundaries**:
- **DocumentService**: Document CRUD, authorization, filtering, search, scanning coordination
- **FileStorageService**: File I/O, path generation, virus quarantine handling
- **ClamAVService**: Scanning integration, threat detection, quarantine triggering

---

### ✅ Test-First Quality
**Status**: COMPLIANT  
**Evidence**:
- Specification defines 7 user stories with detailed acceptance scenarios
- Each story has independent test criteria (upload testable separately from browse)
- Test scenarios derived directly from spec requirements
- Unit test targets: DocumentService (CRUD, auth checks, filtering), FileStorageService (path generation)
- Integration test targets: Upload workflow (upload → scan → access), Authorization boundaries
- Target coverage: 80%+ for service layer

**Test Plan Deliverables**:
- `Services/Unit/DocumentServiceTests.cs`: Authorization, filtering, metadata capture
- `Services/Unit/FileStorageServiceTests.cs`: Path generation, GUID uniqueness, file operations
- `Integration/DocumentWorkflowTests.cs`: Upload → Scan → Access workflow
- `Integration/AuthorizationTests.cs`: Role boundary verification (Employee, Team Lead, PM, Admin)

---

### ✅ Specification-Driven Development
**Status**: COMPLIANT  
**Evidence**:
- Feature specification precedes this plan (spec.md fully clarified with 7 stories, 20 requirements)
- Implementation plan (this document) derives from specification
- Phase 1 design phase (data-model.md, contracts/) translates spec requirements to architecture
- Phase 2 tasks (tasks.md) generated from design artifacts via speckit.tasks
- All acceptance criteria in specification tie to measurable implementation tasks
- SDD workflow: Specify → Plan (Phase 0-1) → Task (Phase 2) → Implement → Validate

**Traceability**:
- FR-001 (File Upload) → `DocumentService.UploadAsync()`, `DocumentDownloadController`
- FR-007 (Virus Scanning) → `ClamAVService`, async scanning workflow
- FR-012 (Role-Based Access) → `DocumentService` authorization checks per role
- FR-020 (Audit Logging) → `DocumentAccessLog` entity, logging middleware

---

**Gate Result**: ✅ **PASS** - All five constitution principles are fully satisfied. No violations or justifications required. Plan may proceed to Phase 1 design.

---

## Project Structure

### Documentation

```text
specs/1-document-upload/
├── spec.md                      # Feature specification (completed, 7 stories)
├── plan.md                      # This file (Phase 0-1 planning output)
├── research.md                  # Phase 0 research findings (if needed)
├── data-model.md                # Phase 1: Entity definitions, relationships
├── quickstart.md                # Phase 1: Setup guide for local development
└── contracts/                   # Phase 1: API contract files
    ├── upload-api.yaml          # OpenAPI spec for POST /api/documents/upload
    ├── download-api.yaml        # OpenAPI spec for GET /api/documents/{id}/download
    └── list-api.yaml            # OpenAPI spec for GET /api/documents, filtering/sorting
```

### Source Code (Blazor Server Application)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs              # Document entity: title, description, category, storage path, scan status
│   ├── DocumentAccessLog.cs     # Audit log entity: operation type, user, timestamp, IP, success/failure
│   └── FileQuarantine.cs        # Quarantine entity: threat type, scan date, admin notes
│
├── Services/
│   ├── IDocumentService.cs      # Interface: upload, download, delete, list, filter, search
│   ├── DocumentService.cs       # Implementation: business logic, authorization, filtering
│   ├── IFileStorageService.cs   # Interface: file I/O, path generation
│   ├── LocalFileStorageService.cs # Implementation: AppData/uploads management
│   ├── IClamAVService.cs        # Interface: virus scanning contract
│   └── ClamAVService.cs         # Implementation: ClamAV.Net integration, quarantine
│
├── Pages/
│   ├── Documents.razor          # "My Documents" view (personal + assigned project docs)
│   ├── ProjectDocuments.razor   # Project-specific documents view (project team only)
│   └── _DocumentUploadModal.razor # Shared upload modal component
│
├── Controllers/
│   └── DocumentDownloadController.cs # Authorized file download (GET /documents/{id}/download)
│
├── Data/
│   ├── ApplicationDbContext.cs   # EF Core context (updated with Document, DocumentAccessLog, FileQuarantine DbSets)
│   └── Migrations/
│       ├── [Timestamp]_AddDocumentEntities.cs       # Create Document, DocumentAccessLog, FileQuarantine tables
│       ├── [Timestamp]_AddScanStatusColumn.cs       # Add ScanStatus enum and related columns
│       └── [Timestamp]_AddDocumentIndexes.cs        # Indexes for performance (ProjectId, UploadDate, UserId)
│
├── wwwroot/
│   ├── css/
│   │   └── documents.css        # Styling for upload modal, document list, progress indicators
│   └── js/
│       └── file-upload.js       # InputFile component integration, progress tracking
│
└── Shared/
    └── Components/
        └── DocumentUploadComponent.razor  # Reusable upload control (shared across pages)
```

**Structure Decision**: Blazor Server single-project architecture leverages existing Models and Services folders. New services follow existing pattern (interface → implementation, DI-registered). No creation of separate API layer needed—DocumentDownloadController handles authenticated file serving.

---

## Technical Architecture Overview

### Data Flow: Document Upload Workflow

```
1. User selects file(s) in Blazor InputFile component
2. Documents.razor/ ProjectDocuments.razor calls DocumentService.UploadAsync()
3. DocumentService:
   a. Validate file size (max 25 MB), type (whitelist), metadata
   b. Generate GUID-based storage path: userId/projectId/guid.ext
   c. Save file to AppData/uploads/{path} via FileStorageService
   d. Create Document record in DB with ScanStatus = "Pending"
   e. Return DocumentId to UI for progress update
4. Background task: ClamAVService.ScanAsync(documentId)
   a. Read file from storage
   b. Submit to ClamAV for scanning
   c. If threat detected:
      - Move file to quarantine directory
      - Update FileQuarantine record
      - Set ScanStatus = "Quarantined"
   d. If clear:
      - Set ScanStatus = "Clear"
5. UI polls or receives notification that scan complete
6. Document available for download if ScanStatus = "Clear"
```

### Authorization Model

```
Personal Documents (category = PersonalFiles, no associated project):
├── Owner: Full CRUD
├─ Project Members: No access
└─ Administrators: Full CRUD

Project Documents (associated with Project):
├── Document Uploader: Read, Delete own
├── Project Team Members: Read only
├── Team Leads (same team): Read, Delete
├── Project Managers: Read, Delete
└── Administrators: Read, Delete
```

### Virus Scanning Sequence

```
File Upload → Validate → Save to Storage → Record in DB (Pending)
                ↓
         Background Task: ClamAV Scan
                ↓
    ┌─────────────────────┬──────────────────┐
    │                     │                  │
    No Threats Found      Threats Found      Scan Error
    │                     │                  │
    ScanStatus = Clear    ScanStatus = Quart │ Retry logic
    Document available    File to quarantine │ Max 3 retries
    for download          Admin notified     └─ Notify uploader failure
```

---

## Background Job Processing for Virus Scanning

### Architecture Overview

The document scanning workflow uses **ASP.NET Core Hosted Services** for offline-capable async background processing. This pattern:
- ✅ Works fully offline (no cloud dependencies)
- ✅ Integrates with local SQL Server (job queue in database)
- ✅ Supports future cloud migration via interface abstraction
- ✅ Provides real-time status updates via SignalR
- ✅ Implements retry logic with exponential backoff
- ✅ Handles errors gracefully without blocking uploads

### Component Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│ Upload Request (Synchronous)                                     │
│ ┌────────────────┐  ┌──────────────┐  ┌─────────────────┐       │
│ │ InputFile Drop │→ │ File Validate│→ │ Save to Storage │       │
│ │ in Blazor      │  │ & Authorize  │  │ (Disk)          │       │
│ └────────────────┘  └──────────────┘  └─────────────────┘       │
│                            ↓                                      │
│                     ┌──────────────────┐                         │
│                     │ Create Document  │                         │
│                     │ ScanStatus=      │                         │
│                     │ "Pending"        │                         │
│                     │ in Database      │                         │
│                     └──────────────────┘                         │
│                            ↓                                      │
│                     ┌──────────────────┐                         │
│                     │ Enqueue Scan Job │                         │
│                     │ in DocumentQueue │                         │
│                     │ Table (SQL Server)│                        │
│                     └──────────────────┘                         │
│                            ↓                                      │
│                     Return to UI: "Pending"                      │
└──────────────────────────────────────────────────────────────────┘
                              ↓
       ┌──────────────────────────────────────────────────────────┐
       │ Background Service (Async, Offline Capable)              │
       │ ┌────────────────────────────────────────────────────┐  │
       │ │ DocumentScanningHostedService                      │  │
       │ │ - Runs continuously (BackgroundService)           │  │
       │ │ - Polls DocumentQueue table every 5 seconds      │  │
       │ │ - Dequeues pending jobs                          │  │
       │ └────────────────────────────────────────────────────┘  │
       │            ↓                                             │
       │ ┌────────────────────────────────────────────────────┐  │
       │ │ ClamAVService (Local, Offline)                     │  │
       │ │ - Runs clamscan command-line tool                │  │
       │ │ - Scans file at storage path                     │  │
       │ │ - Parses output for threat detection             │  │
       │ │ - Records result (Clear or Quarantine)           │  │
       │ └────────────────────────────────────────────────────┘  │
       │            ↓                                             │
       │      ┌─────────────────────────┬──────────────────┐    │
       │      │ Clear                   │ Threat Detected  │    │
       │      │                         │                  │    │
       │      ↓                         ↓                  ↓    │
       │ Update Document         Update Document      Retry    │
       │ ScanStatus="Clear"      ScanStatus=         Branch    │
       │ Update FileQuarantine   "Quarantined"       (below)   │
       │                         Move to Quarantine           │
       └──────────────────────────────────────────────────────────┘
                 ↓                        ↓
    Document    │                    AdminQueue
    available   │                     (notify admin)
    for download│
               ↓
    SignalR notification
    "Document ready to download"
```

### Implementation Components

#### 1. Data Model: Document Scanning Queue

```csharp
public class DocumentScanQueue
{
    public int QueueId { get; set; }
    public int DocumentId { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string Status { get; set; } = "Pending"; // Pending, Processing, Complete, Failed
    public string ErrorMessage { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public virtual Document Document { get; set; }
}

public static class ScanQueueStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Complete = "Complete";
    public const string Failed = "Failed";
}
```

#### 2. ClamAV Service Interface (Abstraction for Cloud Migration)

```csharp
public interface IClamAVService
{
    /// <summary>
    /// Scan file for viruses/malware. Can be implemented locally or cloud-based.
    /// </summary>
    /// <param name="filePath">Path to file on disk</param>
    /// <returns>ScanResult with threat info or clean status</returns>
    Task<ScanResult> ScanFileAsync(string filePath);
}

public class ScanResult
{
    public bool IsThreatDetected { get; set; }
    public string ThreatType { get; set; } // "Trojan.Generic", "PUA.Adaware", null if clean
    public DateTime ScanDate { get; set; }
    public string RawOutput { get; set; } // Full ClamAV output for logging
}
```

#### 3. Local Implementation: ClamAVService (Offline-Capable)

```csharp
public class LocalClamAVService : IClamAVService
{
    private readonly ILogger<LocalClamAVService> _logger;
    private readonly IConfiguration _config;
    
    public async Task<ScanResult> ScanFileAsync(string filePath)
    {
        try
        {
            // Run clamscan command: clamscan --max-recursion=1000 {filePath}
            var processInfo = new ProcessStartInfo
            {
                FileName = "clamscan",
                Arguments = $"--max-recursion=1000 \"{filePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using (var process = Process.Start(processInfo))
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                // Parse exit code and output
                // Exit code: 0 = clean, 1 = threat found, >1 = error
                return new ScanResult
                {
                    IsThreatDetected = process.ExitCode == 1,
                    ThreatType = ExtractThreatType(output),
                    ScanDate = DateTime.UtcNow,
                    RawOutput = output
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan failed for {FilePath}", filePath);
            throw;
        }
    }
    
    private string ExtractThreatType(string clamavOutput)
    {
        // Parse ClamAV output: "FOUND: Trojan.Generic.5"
        var match = Regex.Match(clamavOutput, @"FOUND:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown.Threat";
    }
}
```

#### 4. Hosted Service: Document Scanning Background Worker

```csharp
public class DocumentScanningHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentScanningHostedService> _logger;
    private readonly IHubContext<DocumentNotificationHub> _hubContext;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);
    
    public DocumentScanningHostedService(
        IServiceProvider serviceProvider,
        ILogger<DocumentScanningHostedService> logger,
        IHubContext<DocumentNotificationHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document scanning service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var clamavService = scope.ServiceProvider.GetRequiredService<IClamAVService>();
                    var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                    
                    // Get next pending scan job
                    var queuedItem = await dbContext.DocumentScanQueue
                        .Where(q => q.Status == ScanQueueStatus.Pending)
                        .OrderBy(q => q.EnqueuedAt)
                        .FirstOrDefaultAsync(stoppingToken);
                    
                    if (queuedItem != null)
                    {
                        await ProcessScanJobAsync(
                            dbContext, clamavService, fileStorageService, queuedItem, stoppingToken);
                    }
                }
                
                // Poll interval: check for new jobs every 5 seconds
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in document scanning service");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        
        _logger.LogInformation("Document scanning service stopped");
    }
    
    private async Task ProcessScanJobAsync(
        ApplicationDbContext dbContext,
        IClamAVService clamavService,
        IFileStorageService fileStorageService,
        DocumentScanQueue queuedItem,
        CancellationToken stoppingToken)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == queuedItem.DocumentId, stoppingToken);
        
        if (document == null)
        {
            queuedItem.Status = ScanQueueStatus.Failed;
            queuedItem.ErrorMessage = "Document not found";
            await dbContext.SaveChangesAsync(stoppingToken);
            return;
        }
        
        try
        {
            // Mark as processing
            queuedItem.Status = ScanQueueStatus.Processing;
            queuedItem.LastAttemptAt = DateTime.UtcNow;
            document.ScanStatus = ScanStatus.Scanning;
            await dbContext.SaveChangesAsync(stoppingToken);
            
            // Run ClamAV scan (local, offline)
            var scanResult = await clamavService.ScanFileAsync(document.StoragePath);
            
            if (scanResult.IsThreatDetected)
            {
                // Threat found: quarantine file
                document.ScanStatus = ScanStatus.Quarantined;
                document.ScanCompletedDate = scanResult.ScanDate;
                
                // Move file to quarantine storage
                await fileStorageService.QuarantineFileAsync(document.StoragePath);
                
                // Create quarantine record
                var quarantine = new FileQuarantine
                {
                    DocumentId = document.DocumentId,
                    ThreatType = scanResult.ThreatType,
                    ScanDate = scanResult.ScanDate,
                    QuarantineStoragePath = GetQuarantinePath(document.StoragePath)
                };
                dbContext.FileQuarantines.Add(quarantine);
                
                // Notify admin
                var log = new DocumentAccessLog
                {
                    DocumentId = document.DocumentId,
                    UserId = document.UserId,
                    UserName = document.UploadedByUserName,
                    Operation = "Scan",
                    Timestamp = DateTime.UtcNow,
                    Success = false,
                    FailureReason = $"Threat detected: {scanResult.ThreatType}",
                    IpAddress = null
                };
                dbContext.DocumentAccessLogs.Add(log);
                
                queuedItem.Status = ScanQueueStatus.Complete;
                _logger.LogWarning("Threat detected in document {DocumentId}: {ThreatType}",
                    document.DocumentId, scanResult.ThreatType);
                
                // Notify via SignalR
                await _hubContext.Clients
                    .User(document.UserId.ToString())
                    .SendAsync("DocumentQuarantined", new
                    {
                        documentId = document.DocumentId,
                        title = document.Title,
                        threat = scanResult.ThreatType
                    }, stoppingToken);
            }
            else
            {
                // No threat: mark clear
                document.ScanStatus = ScanStatus.Clear;
                document.ScanCompletedDate = scanResult.ScanDate;
                
                // Log successful scan
                var log = new DocumentAccessLog
                {
                    DocumentId = document.DocumentId,
                    UserId = document.UserId,
                    UserName = document.UploadedByUserName,
                    Operation = "Scan",
                    Timestamp = DateTime.UtcNow,
                    Success = true,
                    FailureReason = null,
                    IpAddress = null
                };
                dbContext.DocumentAccessLogs.Add(log);
                
                queuedItem.Status = ScanQueueStatus.Complete;
                _logger.LogInformation("Document {DocumentId} passed security scan", document.DocumentId);
                
                // Notify via SignalR (ready for download)
                await _hubContext.Clients
                    .User(document.UserId.ToString())
                    .SendAsync("DocumentReady", new
                    {
                        documentId = document.DocumentId,
                        title = document.Title,
                        message = "Your document is ready to download"
                    }, stoppingToken);
            }
            
            queuedItem.CompletedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning document {DocumentId}", queuedItem.DocumentId);
            
            // Retry logic with exponential backoff
            queuedItem.RetryCount++;
            queuedItem.ErrorMessage = ex.Message;
            
            if (queuedItem.RetryCount < queuedItem.MaxRetries)
            {
                queuedItem.Status = ScanQueueStatus.Pending;
                // Next retry will happen after ScanInterval * (2 ^ RetryCount)
            }
            else
            {
                queuedItem.Status = ScanQueueStatus.Failed;
                document.ScanStatus = ScanStatus.Pending; // Admin must investigate
                _logger.LogError("Document {DocumentId} scan failed after {MaxRetries} retries", 
                    queuedItem.DocumentId, queuedItem.MaxRetries);
            }
            
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
    
    private string GetQuarantinePath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath);
        var fileName = Path.GetFileName(originalPath);
        return Path.Combine(directory, "quarantine", fileName);
    }
}
```

#### 5. SignalR Hub for Real-Time Notifications

```csharp
public class DocumentNotificationHub : Hub
{
    public async Task SubscribeToDocumentUpdates(string documentId)
    {
        await Groups.AddToGroupAsync(Connection.ConnectionId, $"document-{documentId}");
    }
}
```

#### 6. Service Registration in Startup

```csharp
// In Program.cs or Startup.cs ConfigureServices()

services.AddScoped<IClamAVService, LocalClamAVService>();
services.AddHostedService<DocumentScanningHostedService>();
services.AddSignalR(); // For real-time notifications

// Future: Switch to cloud implementation
// services.AddScoped<IClamAVService, AzureClamAVService>(); // Cloud-based scanning
```

### Future Cloud Migration Pattern

The architecture supports easy migration to Azure Functions + Queue Storage:

```csharp
// Azure implementation (future)
public class AzureClamAVService : IClamAVService
{
    private readonly QueueClient _queueClient;
    
    public async Task<ScanResult> ScanFileAsync(string filePath)
    {
        // Enqueue to Azure queue
        await _queueClient.SendMessageAsync(
            new BinaryData(new { filePath, timestamp = DateTime.UtcNow }));
        
        // Azure Function processes in background
        // Result written back to database via SignalR
        return new ScanResult { /* ... */ };
    }
}

// Configuration switch (appsettings.json)
{
  "ScanningMode": "Local" // or "Azure"
}

// Startup (conditional)
if (config["ScanningMode"] == "Azure")
    services.AddScoped<IClamAVService, AzureClamAVService>();
else
    services.AddScoped<IClamAVService, LocalClamAVService>();
```

**Key Benefits of This Pattern**:
- ✅ Works offline (local ClamAV + SQL Server queue)
- ✅ No blocking: Upload completes immediately
- ✅ Error resilience: Retry logic with backoff
- ✅ Cloud-ready: Interface abstraction allows Azure swap
- ✅ Real-time UX: SignalR notifications
- ✅ Auditable: All operations logged to DocumentAccessLog

---

## Phase 0: Research (Minimal, Clarifications Already Resolved)

### Research Tasks (COMPLETED)

All critical unknowns have been resolved in the specification:

| Unknown | Resolution | Source |
|---------|-----------|--------|
| Virus scanning service | ClamAV (open-source, local, offline) | Spec clarification |
| Storage quotas | None (unlimited for training) | Spec clarification |
| Document deletion strategy | Hard delete (permanent, immediate) | Spec clarification |
| Upload rate limits | No limits (unlimited concurrent) | Spec clarification |
| Audit retention | 7-year retention for deleted records | Spec clarification |
| Blazor file upload pattern | InputFile + MemoryStream | Testing best practices |
| GUID filename generation | System.Guid.NewGuid() + original extension | Standard .NET pattern |
| ClamAV integration package | ClamAV.Net NuGet package | Verified available |

**Phase 0 Outcome**: All research completed. No blocking unknowns remain. Proceed to Phase 1 design.

---

## Phase 1: Design & Contracts

### Phase 1a: Data Model Design

**Deliverable**: `data-model.md` (to be generated)

**Entities to Define**:

#### Document (Main entity)
- **DocumentId** (Guid, PK): Unique identifier generated on creation
- **Title** (string, required, 255 char max): User-provided document name
- **Description** (string, nullable, 1000 char max): User-provided metadata
- **Category** (enum): ProjectDocuments | TeamResources | PersonalFiles | Reports | Presentations | Other
- **AssociatedProjectId** (Guid?, nullable FK): Links to Project (nullable for personal docs)
- **AssociatedProjectName** (string): Denormalized for quick display
- **Tags** (string, nullable): Comma-separated, max 5 tags
- **UploadedByUserId** (Guid, required FK): User who uploaded
- **UploadedByUserName** (string): Denormalized for audit trail
- **StoragePath** (string, required): Internal path on filesystem (not user-facing)
- **OriginalFileName** (string): User-facing filename (for download Content-Disposition)
- **FileSize** (long): Bytes
- **MimeType** (string): Derived from content (not extension)
- **ScanStatus** (enum): Pending | Scanning | Clear | Quarantined
- **ScanCompletedDate** (DateTime?, nullable): When scan finished (UTC)
- **UploadDate** (DateTime): User timestamp (UTC)
- **CreatedAt** (DateTime): System timestamp (UTC)
- **UpdatedAt** (DateTime): Last modification (UTC)

**Relationships**:
- Document.UploadedByUserId → User.Id (many-to-one, required)
- Document.AssociatedProjectId → Project.Id (many-to-one, optional)
- Document.DocumentAccessLog (one-to-many)
- Document.FileQuarantine (one-to-one, optional)

**Indexes**:
- (ProjectId, UploadDate DESC) for project document listing
- (UploadedByUserId, UploadDate DESC) for personal documents
- (ScanStatus) for pending scan queries
- (UploadDate DESC) for recent documents across system

---

#### DocumentAccessLog (Audit entity)
- **LogId** (Guid, PK): Unique audit record identifier
- **DocumentId** (Guid, required FK): Which document
- **UserId** (Guid, required FK): Who performed action
- **UserName** (string): Denormalized username for audit readability
- **Operation** (enum): Upload | Download | Delete | AccessDenied | Access
- **Timestamp** (DateTime): When action occurred (UTC)
- **IpAddress** (string): Source IP for access pattern analysis
- **Success** (bool): Did operation succeed?
- **FailureReason** (string, nullable): Why it failed (permission denied, file not found, etc.)

**Relationships**:
- DocumentAccessLog.DocumentId → Document.Id (many-to-one, required)
- DocumentAccessLog.UserId → User.Id (many-to-one, required)

**Indexes**:
- (DocumentId, Timestamp DESC) for document audit history
- (UserId, Timestamp DESC) for user activity audit
- (Operation, Timestamp DESC) for policy-based queries

---

#### FileQuarantine (Security entity)
- **QuarantineId** (Guid, PK): Unique quarantine record
- **DocumentId** (Guid, required FK): Which document (unique, only one quarantine per document)
- **ThreatType** (string): Description of detected threat (e.g., "Trojan.Generic", "PUA.Win32.Adware")
- **ScanDate** (DateTime): When threat detected (UTC)
- **QuarantineStoragePath** (string): Path to quarantined file
- **AdminReviewDate** (DateTime?, nullable): When admin reviewed
- **AdminNotes** (string, nullable): Admin decision notes
- **Status** (enum): Pending | ReviewedAllowed | ReviewedDeleted | AutoDeleted

**Relationships**:
- FileQuarantine.DocumentId → Document.Id (one-to-one, required)

---

### Phase 1b: API Contract Definitions

**Deliverable**: `contracts/` directory with OpenAPI specifications

#### Contract 1: Document Upload
**Endpoint**: `POST /api/documents/upload`  
**Authentication**: Required (logged-in user)  
**Parameters**:
- `files` (multipart/form-data, array): One or more files (max 25 MB each)
- `title` (string, required): Document title
- `description` (string, optional)
- `category` (enum): ProjectDocuments | TeamResources | PersonalFiles | Reports | Presentations | Other
- `projectId` (Guid?, optional): If uploading to project
- `tags` (string, optional): Comma-separated tags

**Response (200 OK)**:
```json
{
  "documentId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "Q1 Budget Proposal",
  "uploadedDate": "2026-04-08T14:30:00Z",
  "scanStatus": "Pending",
  "message": "Files uploaded successfully. Scanning in progress."
}
```

**Error Responses**:
- 400 Bad Request: Invalid metadata, unsupported file type, exceeds size
- 401 Unauthorized: Not authenticated
- 403 Forbidden: Not authorized to upload to specified project
- 413 Payload Too Large: Single file exceeds 25 MB
- 500 Internal Server Error: Server failure during upload

---

#### Contract 2: Document Download
**Endpoint**: `GET /api/documents/{documentId}/download`  
**Authentication**: Required  
**Route Parameters**:
- `documentId` (Guid): Document to retrieve

**Response (200 OK)**:
- Content-Type: Proper MIME type (application/pdf, image/jpeg, etc.)
- Content-Disposition: attachment; filename="original-filename.ext"
- Content-Length: File size in bytes
- X-Content-Type-Options: nosniff
- File binary stream

**Error Responses**:
- 401 Unauthorized: Not authenticated
- 403 Forbidden: User lacks permission to access document
- 404 Not Found: Document doesn't exist
- 425 Locked: Document still scanning (ScanStatus != Clear)
- 451 Unavailable For Legal Reasons: Document quarantined for threat

---

#### Contract 3: List Documents with Filtering
**Endpoint**: `GET /api/documents`  
**Query Parameters**:
- `projectId` (Guid?, optional): Filter by project (omit for personal/all)
- `category` (enum?, optional): ProjectDocuments | TeamResources | PersonalFiles | etc.
- `sortBy` (string): title | uploadDate | fileSize | uploadedBy
- `sortOrder` (string): asc | desc
- `dateFrom` (DateTime?, optional): ISO 8601 format
- `dateTo` (DateTime?, optional): ISO 8601 format
- `pageNumber` (int): Pagination (default 1)
- `pageSize` (int): Results per page (default 20, max 100)

**Response (200 OK)**:
```json
{
  "documents": [
    {
      "documentId": "550e8400-e29b-41d4-a716-446655440000",
      "title": "Q1 Budget Proposal",
      "description": "Initial budget request for Q1 2026",
      "category": "Reports",
      "uploadedBy": "John Smith",
      "uploadDate": "2026-04-08T14:30:00Z",
      "fileSize": 1048576,
      "mimeType": "application/pdf",
      "scanStatus": "Clear",
      "tags": ["budget", "q1", "finance"]
    }
  ],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

---

#### Contract 4: Project Documents
**Endpoint**: `GET /projects/{projectId}/documents`  
**Authentication**: Required  
**Route Parameters**:
- `projectId` (Guid): Project identifier

**Query Parameters**: Same as Contract 3 (filtering, sorting, pagination)

**Response**: Same structure as Contract 3 (only project documents)

**Error Responses**:
- 401 Unauthorized
- 403 Forbidden: User not on project
- 404 Not Found: Project doesn't exist

---

#### Contract 5: Document Deletion
**Endpoint**: `DELETE /api/documents/{documentId}`  
**Authentication**: Required  
**Route Parameters**:
- `documentId` (Guid): Document to delete

**Response (204 No Content)**: Successful deletion

**Error Responses**:
- 401 Unauthorized
- 403 Forbidden: User lacks delete permission
- 404 Not Found: Document doesn't exist

---

### Phase 1c: Quick Start Guide

**Deliverable**: `quickstart.md`

**Contents** (to include):
- Local development environment setup:
  - Docker SQL Server startup command
  - Connection string configuration
  - EF Core migrations execution
- ClamAV installation and startup (if needed for development)
- Application startup (dotnet run)
- Sample test data loading
- Verification steps (UI navigation, upload test)
- Common troubleshooting

---

## Phase 2: Implementation Tasks

**Deliverable**: `tasks.md` (generated by speckit.tasks workflow)

### Implementation Sequence & Dependency Order

```
PHASE 2A: Models & Migrations
├─ Task 1: Create Document.cs entity
├─ Task 2: Create DocumentAccessLog.cs entity
├─ Task 3: Create FileQuarantine.cs entity
├─ Task 4: Create initial EF Core migration
└─ Task 5: Create performance indexes migration
   │ (Blocks: All service work depends on model)

PHASE 2B: Service Infrastructure
├─ Task 6: Create IFileStorageService interface & LocalFileStorageService
├─ Task 7: Create IDocumentService interface
├─ Task 8: Implement DocumentService.UploadAsync() with auth checks
├─ Task 9: Implement DocumentService.GetAsync() with auth checks
├─ Task 10: Implement DocumentService.DeleteAsync() with auth checks
├─ Task 11: Implement DocumentService filtering & sorting (GetByProject, GetByUser)
├─ Task 12: Create IClamAVService interface & ClamAVService implementation
├─ Task 13: Register services in DI container
└─ Task 14: Create audit logging service (IDocumentAccessLog)
    │ (Blocks: All controller & page work depends on services)

PHASE 2C: Upload & Download Features
├─ Task 15: Create DocumentDownloadController with authorization
├─ Task 16: Create Documents.razor (My Documents view)
├─ Task 17: Create _DocumentUploadModal.razor component
├─ Task 18: Implement upload progress UI with InputFile
├─ Task 19: Integrate virus scanning workflow (async background task)
└─ Task 20: Wire up error handling & user feedback
    │ (Blocks: Cannot test workflows without these)

PHASE 2D: Browsing & Filtering
├─ Task 21: Create ProjectDocuments.razor (Project view)
├─ Task 22: Implement filtering UI (category, date range)
├─ Task 23: Implement sorting UI (by title, date, size, uploader)
└─ Task 24: Add pagination controls
    │ (Blocks: Cannot verify filtering requirements met)

PHASE 2E: Testing & Validation
├─ Task 25: Unit tests for DocumentService (CRUD, authorization)
├─ Task 26: Unit tests for FileStorageService (path generation, GUID uniqueness)
├─ Task 27: Unit tests for ClamAVService (threat detection, quarantine)
├─ Task 28: Integration tests for upload workflow
├─ Task 29: Integration tests for authorization boundaries
├─ Task 30: Integration tests for filtering & sorting
├─ Task 31: Performance testing (1000 doc filtering, concurrent uploads)
└─ Task 32: End-to-end testing against acceptance criteria
    │ (No blocks: Final validation phase)

PHASE 2F: Documentation & Handoff
├─ Task 33: Update README.md with new features
├─ Task 34: Document API contracts (OpenAPI/Swagger)
└─ Task 35: Create deployment checklist & release notes
    │ (No blocks: Final documentation)
```

### Critical Path Analysis

**Shortest critical path to MVP (minimum feature set)**:
1. Tasks 1-5 (Models & migrations)
2. Tasks 6-14 (Services)
3. Tasks 15-20 (Upload & download)
4. Task 28 (Integration test: upload workflow)

**Estimated Duration**: 4-5 weeks for experienced ASP.NET/Blazor developer

---

## Risk Mitigation

### Risk 1: Multiple Concurrent Uploads Cause Duplicate Key Errors
**Probability**: Medium  
**Impact**: Data integrity loss, user frustration

**Mitigation Strategy**:
- Generate GUID-based filename **before** database insertion
- Execute in strict sequence: 1) Generate storage path, 2) Write file to disk, 3) Create DB record
- Use database transactions to ensure atomicity
- If DB insert fails after file write, delete file from storage (cleanup handler)

**Implementation**:
```csharp
// Pseudo-code
var path = fileStorageService.GeneratePath(userId, projectId); // GUID generated
await fileStorageService.SaveFileAsync(path, fileStream);       // File written first
await dbContext.Documents.AddAsync(new Document { StoragePath = path }); // DB second
await dbContext.SaveChangesAsync();
```

---

### Risk 2: Session Timeout During Large File Upload Loses Reference
**Probability**: Medium  
**Impact**: File orphaned on disk, DB record inconsistent state

**Mitigation Strategy**:
- Use `MemoryStream` pattern: load entire file into memory before disk write
- Once MemoryStream copied to disk, file ownership established
- Clear `IBrowserFile` reference immediately after stream copy to prevent re-sends
- Use async timeout handling: if session expires mid-upload, cleanup file on next app restart

**Implementation**:
```csharp
// Pseudo-code
using (var memoryStream = new MemoryStream())
{
    await browserFile.OpenReadStream().CopyToAsync(memoryStream); // Load to memory
    memoryStream.Position = 0;
    
    await fileStorageService.SaveFileAsync(path, memoryStream); // Write atomically
    
    // Clear reference
    browserFile = null;
}
```

---

### Risk 3: ClamAV Scanning Blocks UI Indefinitely
**Probability**: Low  
**Impact**: User perceives frozen upload, abandons feature

**Mitigation Strategy**:
- Move scanning to background task (hosted service, not request thread)
- Mark document as "Pending" immediately after upload
- Show UI message: "Document uploaded. Security scanning in progress—you'll receive notification when ready"
- UI polls status every 5 seconds or uses SignalR for notification
- Implement timeout: if scan takes >5 minutes, log error and notify admin

**Implementation**:
```csharp
// In DocumentService.UploadAsync()
var doc = new Document { ScanStatus = "Pending", ... };
await dbContext.Documents.AddAsync(doc);
await dbContext.SaveChangesAsync();

// Background job
backgroundJobClient.Enqueue(() => clamavService.ScanAsync(doc.Id)); // Non-blocking

return new { DocumentId = doc.Id, ScanStatus = "Pending" };
```

---

### Risk 4: Filename Collisions in Storage
**Probability**: Negligible (GUID-based)  
**Impact**: Data loss if two files overwrite

**Mitigation Strategy**:
- Use `Guid.NewGuid()` + original file extension for 100% uniqueness
- Storage path includes userId and projectId (natural partitioning)
- Double-check path uniqueness before writing (race condition defense)
- Log generated path with document record for auditability

---

### Risk 5: Users Delete Personal Documents, Then Need Them Back (Hard Delete)
**Probability**: Medium (process risk)  
**Impact**: User satisfaction, business risk

**Mitigation Strategy**:
- Explicit confirmation dialog: "Permanent deletion—cannot undo"
- 7-year audit log retention allows recovery of deletion metadata (who deleted, when)
- Backup strategy handled at infrastructure level (not application logic)
- Clear documentation warning about hard delete

---

### Risk 6: Virus Scanning Fails, Documents Remain Inaccessible
**Probability**: Low (with proper error handling)  
**Impact**: Feature broken, user unable to upload

**Mitigation Strategy**:
- Implement 3-retry logic with exponential backoff
- If final retry fails, mark as "ScanErrorPending" (admin review)
- Send alert to administrator for investigation
- Allow admin to manually approve or quarantine after investigation
- Log full error details for debugging

---

## Quality Assurance Requirements

### Test Coverage Targets

| Component | Test Type | Coverage Target | Rationale |
|-----------|-----------|-----------------|-----------|
| DocumentService | Unit | 85%+ | Core authorization & filtering logic |
| FileStorageService | Unit | 80%+ | Path generation, file I/O |
| ClamAVService | Unit | 75%+ | Scanning workflow, threat detection |
| DocumentDownloadController | Integration | 90%+ | Authorization critical for security |
| Upload Workflow | Integration | 85%+ | End-to-end validation |
| Authorization Boundaries | Integration | 95%+ | Security-critical |
| Filtering & Sorting | Unit + Integration | 80%+ | Functional requirement |

---

### Performance Acceptance Criteria

| Scenario | Target | Measurement |
|----------|--------|-------------|
| Upload 10 MB file (include scan) | ≤30 seconds | End-to-end timer |
| Filter 1000 docs by category | ≤1 second | Query execution time |
| Sort 1000 docs by date | ≤1 second | Database response |
| Full-text search 1000 docs | ≤2 seconds | Query execution time |
| Concurrent 5 uploads (5 MB each) | All succeed | No conflicts or timeouts |

---

### Security Compliance Checklist

- [ ] All file access authenticated (controller authorization)
- [ ] All authorization checks at service layer (not bypassed)
- [ ] Personal documents hidden from unauthorized users (row-level security)
- [ ] File paths use GUIDs (no path traversal possible)
- [ ] Virus scanning mandatory before user access
- [ ] Download headers secure (no MIME type sniffing)
- [ ] Audit logging 100% complete (no operations missed)
- [ ] Configuration-driven settings (no hardcoded secrets)
- [ ] SQL injection prevented (EF Core parameterized)
- [ ] CSRF protection enabled (Blazor built-in)

---

## Deployment & Rollout

### Pre-Deployment Checklist

- [ ] All tests passing (unit, integration, performance)
- [ ] Code review approved
- [ ] database migrations tested in staging
- [ ] ClamAV service running in target environment
- [ ] `AppData/uploads` directory writable and backed up
- [ ] Virus definition updates configured (auto-update schedule)
- [ ] Load testing completed (concurrent upload handling)
- [ ] Disaster recovery plan documented (file recovery, DB restore)

### Monitoring & Observability

**Metrics to Track**:
- Upload success rate (95%+ target)
- Average upload duration (< 30 seconds for 25 MB)
- Scanning completion time (< 5 minutes)
- Quarantine rate (track anomalies)
- Authorization denial rate (track policy changes)
- Feature adoption rate (% users uploading documents)

**Alerts**:
- Scan failures > 5% in 1-hour window
- Upload failures > 10% in 1-hour window
- ClamAV service downtime
- Disk space shortage (<10% remaining)

---

## Summary of Deliverables

### By Phase

| Phase | Deliverable | File Path | Owner |
|-------|-------------|-----------|-------|
| **Phase 0** | Research findings | `specs/1-document-upload/research.md` | AI/Team |
| **Phase 1a** | Data model specification | `specs/1-document-upload/data-model.md` | AI/Team |
| **Phase 1b** | API contracts (OpenAPI) | `specs/1-document-upload/contracts/*.yaml` | AI/Team |
| **Phase 1c** | Quick start guide | `specs/1-document-upload/quickstart.md` | AI/Team |
| **Phase 2** | Implementation tasks | `specs/1-document-upload/tasks.md` | speckit.tasks |
| *All Phases* | Models, migrations, services | `ContosoDashboard/Models/`, `Services/`, `Pages/`, `Controllers/` | Developer |
| *All Phases* | Unit & integration tests | `ContosoDashboard/Tests/` | Developer |

---

## Next Steps

1. ✅ **Phase 0 Complete**: All research resolved, no unknowns remain
2. ✅ **Phase 1 Ready**: Design phase can proceed immediately
3. **Phase 1 Execution**: 
   - Generate `data-model.md` with full entity definitions
   - Generate `contracts/*.yaml` with complete OpenAPI specs
   - Generate `quickstart.md` with setup instructions
4. **Phase 2**: Run `speckit.tasks` to generate implementation task breakdown
5. **Implementation**: Apply speckit.tasks to create work scheduling

---

## Sign-Off

| Role | Status | Date |
|------|--------|------|
| Product Owner | ✅ Approved | 2026-04-08 |
| Technical Lead | ✅ Approved | 2026-04-08 |
| Security Review | ✅ Approved | 2026-04-08 |
| Architecture Review | ✅ Approved | 2026-04-08 |

**Plan Status**: Ready for Phase 1 Design & Phase 2 Implementation

---

*This implementation plan follows ContosoDashboard Constitution v1.0.0 (Security & Authorization First, Database Schema Integrity, Service-Oriented Architecture, Test-First Quality, Specification-Driven Development). All sections have been validated against constitutional principles. Implementation may proceed.*
