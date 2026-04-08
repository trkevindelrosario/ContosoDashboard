# Data Model: Document Upload and Management Feature

**Date**: April 8, 2026  
**Specification**: specs/1-document-upload/spec.md  
**Implementation Plan**: specs/1-document-upload/plan.md

---

## Overview

The document management feature introduces three new entities to the database:
1. **Document** — Represents an uploaded file with metadata
2. **DocumentAccessLog** — Audit trail of all document operations
3. **FileQuarantine** — Tracks documents that failed security scanning

These entities integrate with existing **User** and **Project** entities through foreign key relationships.

---

## Entity Definitions

### 1. Document

Represents a single uploaded file with associated metadata, access control information, and scanning status.

#### Table Structure

```sql
CREATE TABLE [dbo].[Document] (
    [DocumentId]           INT          NOT NULL PRIMARY KEY IDENTITY(1,1),
    [UserId]               INT          NOT NULL,
    [ProjectId]            INT          NULL,
    [Title]                NVARCHAR(255) NOT NULL,
    [Description]          NVARCHAR(1000) NULL,
    [Category]             NVARCHAR(50) NOT NULL,
    [Tags]                 NVARCHAR(500) NULL,
    [FileName]             NVARCHAR(255) NOT NULL,
    [StoragePath]          NVARCHAR(500) NOT NULL UNIQUE,
    [FileSize]             BIGINT       NOT NULL,
    [MimeType]             NVARCHAR(255) NOT NULL,
    [ScanStatus]           NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    [ScanCompletedDate]    DATETIME2    NULL,
    [UploadDate]           DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    [CreatedAt]            DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]            DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    
    -- Foreign Keys
    CONSTRAINT [FK_Document_User] FOREIGN KEY ([UserId]) 
        REFERENCES [dbo].[User]([UserId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Document_Project] FOREIGN KEY ([ProjectId]) 
        REFERENCES [dbo].[Project]([ProjectId]) ON DELETE SET NULL,
    
    -- Indexes for querying
    INDEX [IX_Document_UserId] ([UserId]),
    INDEX [IX_Document_ProjectId] ([ProjectId]),
    INDEX [IX_Document_Category] ([Category]),
    INDEX [IX_Document_UploadDate] ([UploadDate] DESC),
    INDEX [IX_Document_ScanStatus] ([ScanStatus]),
    INDEX [IX_Document_UserAndProject] ([UserId], [ProjectId])
);
```

#### Column Definitions

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| **DocumentId** | INT | PK, IDENTITY | Unique document identifier (auto-increment) |
| **UserId** | INT | NOT NULL, FK | User who uploaded document (FK to User table) |
| **ProjectId** | INT | NULL, FK | Associated project (NULL for personal documents) |
| **Title** | NVARCHAR(255) | NOT NULL | Document title provided by uploader |
| **Description** | NVARCHAR(1000) | NULL | Optional document description |
| **Category** | NVARCHAR(50) | NOT NULL | Document category: "ProjectDocuments", "TeamResources", "PersonalFiles", "Reports", "Presentations", "Other" |
| **Tags** | NVARCHAR(500) | NULL | Comma-separated tags for organization (max 5) |
| **FileName** | NVARCHAR(255) | NOT NULL | Original filename (user-facing, for display) |
| **StoragePath** | NVARCHAR(500) | NOT NULL, UNIQUE | Server file path: `{userId}/{projectId or "personal"}/{guid}.{ext}` |
| **FileSize** | BIGINT | NOT NULL | File size in bytes |
| **MimeType** | NVARCHAR(255) | NOT NULL | MIME type (e.g., "application/pdf", "image/jpeg") |
| **ScanStatus** | NVARCHAR(20) | NOT NULL, DEFAULT 'Pending' | Scan status: "Pending", "Scanning", "Clear", "Quarantined" |
| **ScanCompletedDate** | DATETIME2 | NULL | When ClamAV scan completed (NULL if pending/scanning) |
| **UploadDate** | DATETIME2 | NOT NULL | When file was uploaded (UTC) |
| **CreatedAt** | DATETIME2 | NOT NULL | Audit: when record created (UTC) |
| **UpdatedAt** | DATETIME2 | NOT NULL | Audit: when record last updated (UTC) |

#### Relationships

- **User** (1:Many) — One user uploads many documents
- **Project** (0:Many) — One project has many documents; NULL indicates personal document
- **DocumentAccessLog** (1:Many) — One document has many audit log entries
- **FileQuarantine** (0:1) — One document can have at most one quarantine record (soft constraint)

#### Entity Class (C#)

```csharp
public class Document
{
    public int DocumentId { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    public int? ProjectId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string Title { get; set; }
    
    [StringLength(1000)]
    public string Description { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Category { get; set; }
    
    [StringLength(500)]
    public string Tags { get; set; }
    
    [Required]
    [StringLength(255)]
    public string FileName { get; set; }
    
    [Required]
    [StringLength(500)]
    public string StoragePath { get; set; }
    
    [Required]
    public long FileSize { get; set; }
    
    [Required]
    [StringLength(255)]
    public string MimeType { get; set; }
    
    [Required]
    [StringLength(20)]
    public string ScanStatus { get; set; } = "Pending";
    
    public DateTime? ScanCompletedDate { get; set; }
    
    [Required]
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Foreign key navigation properties
    public virtual User User { get; set; }
    public virtual Project Project { get; set; }
    public virtual ICollection<DocumentAccessLog> AccessLogs { get; set; } = new List<DocumentAccessLog>();
    public virtual FileQuarantine Quarantine { get; set; }
}

// Category constants (for strong typing)
public static class DocumentCategory
{
    public const string ProjectDocuments = "ProjectDocuments";
    public const string TeamResources = "TeamResources";
    public const string PersonalFiles = "PersonalFiles";
    public const string Reports = "Reports";
    public const string Presentations = "Presentations";
    public const string Other = "Other";
}

// Scan status constants
public static class ScanStatus
{
    public const string Pending = "Pending";
    public const string Scanning = "Scanning";
    public const string Clear = "Clear";
    public const string Quarantined = "Quarantined";
}
```

---

### 2. DocumentAccessLog

Audit trail recording all document-related operations for compliance and security investigations.

#### Table Structure

```sql
CREATE TABLE [dbo].[DocumentAccessLog] (
    [LogId]           INT          NOT NULL PRIMARY KEY IDENTITY(1,1),
    [DocumentId]      INT          NOT NULL,
    [UserId]          INT          NOT NULL,
    [UserName]        NVARCHAR(255) NOT NULL,
    [Operation]       NVARCHAR(50) NOT NULL,
    [Timestamp]       DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    [IpAddress]       NVARCHAR(45) NULL,
    [Success]         BIT          NOT NULL DEFAULT 1,
    [FailureReason]   NVARCHAR(500) NULL,
    
    -- Foreign Keys
    CONSTRAINT [FK_DocumentAccessLog_Document] FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Document]([DocumentId]) ON DELETE CASCADE,
    CONSTRAINT [FK_DocumentAccessLog_User] FOREIGN KEY ([UserId])
        REFERENCES [dbo].[User]([UserId]) ON DELETE CASCADE,
    
    -- Indexes for querying
    INDEX [IX_DocumentAccessLog_DocumentId] ([DocumentId]),
    INDEX [IX_DocumentAccessLog_UserId] ([UserId]),
    INDEX [IX_DocumentAccessLog_Timestamp] ([Timestamp] DESC),
    INDEX [IX_DocumentAccessLog_Operation] ([Operation]),
    INDEX [IX_DocumentAccessLog_Success] ([Success])
);
```

#### Column Definitions

| Column | Type | Description |
|--------|------|-------------|
| **LogId** | INT | PK, auto-increment |
| **DocumentId** | INT | FK to Document (cascading delete when document deleted) |
| **UserId** | INT | FK to User performing operation |
| **UserName** | NVARCHAR(255) | User name snapshot (for deleted user records) |
| **Operation** | NVARCHAR(50) | "Upload", "Download", "Access", "AccessDenied", "Delete", "Share", "Scan" |
| **Timestamp** | DATETIME2 | When operation occurred (UTC) |
| **IpAddress** | NVARCHAR(45) | Client IP address (for security audit) |
| **Success** | BIT | 1=success, 0=failed |
| **FailureReason** | NVARCHAR(500) | Reason if operation failed (auth check, file format, etc.) |

#### Entity Class (C#)

```csharp
public class DocumentAccessLog
{
    public int LogId { get; set; }
    
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string UserName { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Operation { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [StringLength(45)]
    public string IpAddress { get; set; }
    
    [Required]
    public bool Success { get; set; } = true;
    
    [StringLength(500)]
    public string FailureReason { get; set; }
    
    // Foreign key navigation properties
    public virtual Document Document { get; set; }
    public virtual User User { get; set; }
}

// Operation type constants
public static class DocumentOperation
{
    public const string Upload = "Upload";
    public const string Download = "Download";
    public const string Access = "Access";
    public const string AccessDenied = "AccessDenied";
    public const string Delete = "Delete";
    public const string Share = "Share";
    public const string Scan = "Scan";
}
```

---

### 3. FileQuarantine

Tracks documents that failed security scanning and require administrator review.

#### Table Structure

```sql
CREATE TABLE [dbo].[FileQuarantine] (
    [QuarantineId]      INT          NOT NULL PRIMARY KEY IDENTITY(1,1),
    [DocumentId]        INT          NOT NULL UNIQUE,
    [ThreatType]        NVARCHAR(255) NOT NULL,
    [ScanDate]          DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    [QuarantineStoragePath] NVARCHAR(500) NOT NULL,
    [AdminReviewDate]   DATETIME2    NULL,
    [AdminReviewedBy]   INT          NULL,
    [AdminNotes]        NVARCHAR(1000) NULL,
    [Action]            NVARCHAR(50) NULL,
    
    -- Foreign Keys
    CONSTRAINT [FK_FileQuarantine_Document] FOREIGN KEY ([DocumentId])
        REFERENCES [dbo].[Document]([DocumentId]) ON DELETE CASCADE,
    CONSTRAINT [FK_FileQuarantine_ReviewedBy] FOREIGN KEY ([AdminReviewedBy])
        REFERENCES [dbo].[User]([UserId]) ON DELETE SET NULL,
    
    -- Indexes
    INDEX [IX_FileQuarantine_DocumentId] ([DocumentId]),
    INDEX [IX_FileQuarantine_ScanDate] ([ScanDate] DESC),
    INDEX [IX_FileQuarantine_AdminReviewDate] ([AdminReviewDate])
);
```

#### Column Definitions

| Column | Type | Description |
|--------|------|-------------|
| **QuarantineId** | INT | PK, auto-increment |
| **DocumentId** | INT | FK to Document (unique constraint: one quarantine per doc) |
| **ThreatType** | NVARCHAR(255) | Description of detected threat (e.g., "Trojan.Generic", "PUA.Win32.Toolbar") |
| **ScanDate** | DATETIME2 | When ClamAV detected threat |
| **QuarantineStoragePath** | NVARCHAR(500) | Location where quarantined file is stored (separate from active uploads) |
| **AdminReviewDate** | DATETIME2 | When administrator reviewed quarantine (NULL if pending) |
| **AdminReviewedBy** | INT | User ID of admin who reviewed (FK to User) |
| **AdminNotes** | NVARCHAR(1000) | Admin comments about threat and action taken |
| **Action** | NVARCHAR(50) | "Approved", "Deleted", "Archived" |

#### Entity Class (C#)

```csharp
public class FileQuarantine
{
    public int QuarantineId { get; set; }
    
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string ThreatType { get; set; }
    
    [Required]
    public DateTime ScanDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    [StringLength(500)]
    public string QuarantineStoragePath { get; set; }
    
    public DateTime? AdminReviewDate { get; set; }
    
    public int? AdminReviewedBy { get; set; }
    
    [StringLength(1000)]
    public string AdminNotes { get; set; }
    
    [StringLength(50)]
    public string Action { get; set; }
    
    // Foreign key navigation properties
    public virtual Document Document { get; set; }
    public virtual User ReviewedByUser { get; set; }
}

// Quarantine action constants
public static class QuarantineAction
{
    public const string Approved = "Approved";
    public const string Deleted = "Deleted";
    public const string Archived = "Archived";
}
```

---

## Database Relationships Diagram

```
┌─────────────┐
│    User     │
└──────┬──────┘
       │ (1:Many)
       │
       ├─────────────────────────┐
       │                         │
       ▼ UserId                  ▼ UserId
┌──────────────────┐    ┌─────────────────────┐
│   Document       │    │ DocumentAccessLog   │
│ ─────────────    │    │ ─────────────────── │
│ DocumentId   (PK)│    │ LogId           (PK)│
│ UserId       (FK)├───┐│ DocumentId      (FK)│
│ ProjectId (FK,?) │   ├│ UserId          (FK)│
│ Title            │   │└─────────────────────┘
│ StoragePath (UQ) │   │
│ ScanStatus       │   │
└──────┬───────────┘   │
       │               │
       │ (1:Many)      │
       │               │
       ├─────────────────────────┐
       │                         │
       ▼ DocumentId             │
┌──────────────────┐            │
│ FileQuarantine   │ (1:1)      │
│ ─────────────    │            │
│ QuarantineId (PK)│            │
│ DocumentId   (UQ)├─────┐      │
│ ThreatType       │     │      │
└──────────────────┘     │      │
                         │      │
                    (FK) │      │
                         ▼      │
                    ┌────────┐  │
                    │Document│◄─┘
                    └────────┘
       ▲
       │ ProjectId (FK, NULL)
       │ (0:Many)
       │
┌──────┴──────┐
│  Project    │
└─────────────┘
```

---

## Database Constraints & Indexes

### Primary Keys
- `Document.DocumentId` — INT IDENTITY (auto-increment)
- `DocumentAccessLog.LogId` — INT IDENTITY (auto-increment)
- `FileQuarantine.QuarantineId` — INT IDENTITY (auto-increment)

### Foreign Keys
| Constraint | References | Behavior |
|-----------|-----------|----------|
| `FK_Document_User` | User.UserId | CASCADE DELETE |
| `FK_Document_Project` | Project.ProjectId | SET NULL |
| `FK_DocumentAccessLog_Document` | Document.DocumentId | CASCADE DELETE |
| `FK_DocumentAccessLog_User` | User.UserId | CASCADE DELETE |
| `FK_FileQuarantine_Document` | Document.DocumentId | CASCADE DELETE |
| `FK_FileQuarantine_ReviewedBy` | User.UserId | SET NULL |

### Unique Constraints
- `Document.StoragePath` — Storage paths must be unique to prevent file overwrites

### Indexes for Query Performance

**Document Table**:
- `IX_Document_UserId` — Lookup user's documents (My Documents view)
- `IX_Document_ProjectId` — Lookup project documents (Project view)
- `IX_Document_Category` — Filter by category
- `IX_Document_UploadDate DESC` — Sort by date (newest first)
- `IX_Document_ScanStatus` — Filter by scanning status
- `IX_Document_UserAndProject` — Composite for user+project filtering

**DocumentAccessLog Table**:
- `IX_DocumentAccessLog_DocumentId` — Access history for specific document
- `IX_DocumentAccessLog_UserId` — All operations by user
- `IX_DocumentAccessLog_Timestamp DESC` — Recent operations (audit trail)
- `IX_DocumentAccessLog_Operation` — Group by operation type
- `IX_DocumentAccessLog_Success` — Filter failed operations

**FileQuarantine Table**:
- `IX_FileQuarantine_DocumentId` — Lookup quarantine status
- `IX_FileQuarantine_ScanDate DESC` — Recent quarantines
- `IX_FileQuarantine_AdminReviewDate` — Pending reviews (WHERE NULL)

---

## Data Validation Rules

### Document

1. **Title** — Required, 1-255 characters
2. **Category** — Required, one of predefined values
3. **FileSize** — Required, > 0 bytes, ≤ 25 MB (26,214,400 bytes)
4. **StoragePath** — Required, unique, follows pattern `{userId}/{projectId or "personal"}/{guid}.{ext}`
5. **MimeType** — Required, from approved list (see FR-003)
6. **ScanStatus** — Required, one of: Pending, Scanning, Clear, Quarantined
7. **ProjectId** — Optional; if provided, user must be project member

### DocumentAccessLog

1. **Operation** — Required, one of predefined values
2. **Timestamp** — Required, server-side generated (not user-supplied)
3. **IpAddress** — Optional, extracted from HttpContext

### FileQuarantine

1. **ThreatType** — Required, non-empty string
2. **QuarantineStoragePath** — Required, different from Document.StoragePath

---

## Migration Plan

### EF Core DbContext Configuration

```csharp
protection class ApplicationDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentAccessLog> DocumentAccessLogs { get; set; }
    public DbSet<FileQuarantine> FileQuarantines { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Document entity configuration
        modelBuilder.Entity<Document>()
            .HasKey(d => d.DocumentId);
        
        modelBuilder.Entity<Document>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Document>()
            .HasOne(d => d.Project)
            .WithMany()
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.StoragePath)
            .IsUnique();
        
        // Additional indexes...
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.UserId);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => new { d.UserId, d.ProjectId });
        
        // DocumentAccessLog configuration
        modelBuilder.Entity<DocumentAccessLog>()
            .HasKey(dal => dal.LogId);
        
        modelBuilder.Entity<DocumentAccessLog>()
            .HasOne(dal => dal.Document)
            .WithMany(d => d.AccessLogs)
            .HasForeignKey(dal => dal.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // FileQuarantine configuration
        modelBuilder.Entity<FileQuarantine>()
            .HasKey(fq => fq.QuarantineId);
        
        modelBuilder.Entity<FileQuarantine>()
            .HasIndex(fq => fq.DocumentId)
            .IsUnique();
    }
}
```

### Migration Generation

```bash
# Generate migration from model changes
dotnet ef migrations add AddDocumentManagementEntities

# Apply migration to database
dotnet ef database update

# Verify migration applied
dotnet ef migrations list
```

---

## Data Integrity Guarantees

1. **Cascade Delete**: Deleting a user or document automatically removes related audit logs
2. **Unique Storage Paths**: No two documents can have identical storage paths
3. **Foreign Key Constraints**: All document references must point to valid users/projects
4. **Temporal Data**: UploadDate, CreatedAt, UpdatedAt automatically set by server
5. **Immutable Audit Trail**: DocumentAccessLog records cannot be modified (only inserted)

---

## Performance Considerations

### Query Patterns

**My Documents** (frequent):
```csharp
var documents = await _context.Documents
    .Where(d => d.UserId == userId && d.ProjectId == null)
    .OrderByDescending(d => d.UploadDate)
    .ToListAsync();
```
*Uses: IX_Document_UserId, IX_Document_UploadDate*

**Project Documents** (frequent):
```csharp
var documents = await _context.Documents
    .Where(d => d.ProjectId == projectId)
    .OrderBy(d => d.Title)
    .ToListAsync();
```
*Uses: IX_Document_ProjectId*

**Filter by Category** (frequent):
```csharp
var documents = await _context.Documents
    .Where(d => d.ProjectId == projectId && d.Category == category)
    .ToListAsync();
```
*Uses: IX_Document_Category, IX_Document_ProjectId*

### Index Coverage

All indexes chosen to support filtering, sorting, and lookup operations. Composite indexes on (UserId, ProjectId) reduce need for multiple index scans.

---

## Testing Considerations

### Unit Test Data Fixtures

```csharp
var testUser = new User { UserId = 1, Name = "Test User" };
var testProject = new Project { ProjectId = 1, ProjectManagerId = 1 };
var testDocument = new Document
{
    DocumentId = 1,
    UserId = 1,
    ProjectId = 1,
    Title = "Test Doc",
    Category = DocumentCategory.ProjectDocuments,
    StoragePath = "1/1/test-guid.pdf",
    FileSize = 1024,
    MimeType = "application/pdf",
    ScanStatus = ScanStatus.Clear
};
```

---

**Status**: ✅ Complete  
**Next**: Generate API contracts in `contracts/` directory