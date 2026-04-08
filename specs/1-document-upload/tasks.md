---
description: "Implementation tasks for Document Upload and Management feature"
---

# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/1-document-upload/`  
**Prerequisites**: plan.md, spec.md, data-model.md, research.md, quickstart.md  
**Status**: Ready for implementation  
**Date**: April 8, 2026

---

## Task Format

Format: `- [ ] [TaskID] [P?] [Story?] Description with file path`

- **[P]**: Indicates task can run in parallel (different files, no blocking dependencies)
- **[Story]**: Maps to user story (US1, US2, US3, etc.) - enables independent story completion
- **[TaskID]**: Sequential identifier (T001, T002, ..., T0XX) showing execution order

---

## Phase 1: Setup & Project Structure

**Purpose**: Project initialization and infrastructure foundations

- [ ] T001 Create folder structure for document upload feature in ContosoDashboard/
- [ ] T002 [P] Create Models/ subfolder structure with Document-related entities placeholder in ContosoDashboard/Models/
- [ ] T003 [P] Create Services/ subfolder structure for Document-related services in ContosoDashboard/Services/
- [ ] T004 [P] Create Pages/ subfolder structure for document management UI pages in ContosoDashboard/Pages/
- [ ] T005 [P] Create Controllers/ subfolder structure for document download/upload endpoints in ContosoDashboard/
- [ ] T006 Create Data/Migrations/ directory for Entity Framework migrations in ContosoDashboard/Data/Migrations/

---

## Phase 2: Foundational (Database & Core Services)

**Purpose**: Core infrastructure that MUST complete before any user story implementation  
**Critical**: All stories depend on completion of this phase

### Data Models

- [ ] T007 [P] Create Document entity class in ContosoDashboard/Models/Document.cs with 15 columns (DocumentId, UserId, ProjectId, Title, Description, Category, Tags, FileName, StoragePath, FileSize, MimeType, ScanStatus, ScanCompletedDate, UploadDate, CreatedAt, UpdatedAt)
- [ ] T008 [P] Create DocumentAccessLog entity class in ContosoDashboard/Models/DocumentAccessLog.cs with 8 columns (LogId, DocumentId, UserId, UserName, Operation, Timestamp, IpAddress, Success, FailureReason)
- [ ] T009 [P] Create FileQuarantine entity class in ContosoDashboard/Models/FileQuarantine.cs with 8 columns (QuarantineId, DocumentId, ThreatType, ScanDate, QuarantineStoragePath, AdminReviewDate, AdminReviewedBy, AdminNotes, Action)
- [ ] T010 [P] Create enumerations for ScanStatus (Pending, Scanning, Clear, Quarantined), DocumentCategory (ProjectDocuments, TeamResources, PersonalFiles, Reports, Presentations, Other) in ContosoDashboard/Models/Enumerations.cs

### Database Integration

- [ ] T011 Update ApplicationDbContext to register Document, DocumentAccessLog, FileQuarantine entities in ContosoDashboard/Data/ApplicationDbContext.cs
- [ ] T012 Add foreign key relationships (Document→User, Document→Project, DocumentAccessLog→Document, FileQuarantine→Document) to ApplicationDbContext in ContosoDashboard/Data/ApplicationDbContext.cs
- [ ] T013 Create initial EF Core migration "AddDocumentManagement" in ContosoDashboard/Data/Migrations/[timestamp]_AddDocumentManagement.cs
- [ ] T014 Create migration for DocumentScanQueue table (for background job processing) in ContosoDashboard/Data/Migrations/[timestamp]_AddDocumentScanQueue.cs with fields: QueueId, DocumentId, EnqueuedAt, RetryCount, MaxRetries, Status, ErrorMessage, LastAttemptAt, CompletedAt

### Service Interfaces & Base Classes

- [ ] T015 [P] Create IDocumentService interface in ContosoDashboard/Services/IDocumentService.cs with methods: UploadAsync, DownloadAsync, ListAsync, GetByIdAsync, DeleteAsync, SearchAsync, FilterAsync
- [ ] T016 [P] Create IFileStorageService interface in ContosoDashboard/Services/IFileStorageService.cs with methods: SaveFileAsync, GetFileAsync, DeleteFileAsync, QuarantineFileAsync, GenerateStoragePathAsync
- [ ] T017 [P] Create IClamAVService interface in ContosoDashboard/Services/IClamAVService.cs with method: ScanFileAsync returning ScanResult with IsThreatDetected, ThreatType, ScanDate, RawOutput

### Service Implementations (Core)

- [ ] T018 Create FileStorageService implementation in ContosoDashboard/Services/FileStorageService.cs with file I/O logic, GUID-based naming, path generation (format: `AppData/uploads/{userId}/{projectId or "personal"}/{guid}.{extension}`), and quarantine directory handling
- [ ] T019 Create LocalClamAVService implementation in ContosoDashboard/Services/LocalClamAVService.cs to execute clamscan command-line tool, parse output, detect threats, return ScanResult object
- [ ] T020 Create DocumentService implementation in ContosoDashboard/Services/DocumentService.cs with core CRUD methods (UploadAsync, DownloadAsync, ListAsync, GetByIdAsync, DeleteAsync) including authorization checks at each method
- [ ] T021 Create DocumentAccessLog helper in DocumentService to automatically log all operations (access, upload, download, delete) with user info, operation type, timestamp, success/failure status

### Background Job Infrastructure

- [ ] T022 Create DocumentScanQueue entity in ContosoDashboard/Models/DocumentScanQueue.cs for job persistence
- [ ] T023 Create DocumentScanningHostedService as BackgroundService in ContosoDashboard/Services/DocumentScanningHostedService.cs to poll queue every 5 seconds, dequeue pending jobs, execute ClamAV scanning, update document status, implement 3-retry logic with exponential backoff
- [ ] T024 Register core services in Program.cs: Add IDocumentService, IFileStorageService, IClamAVService, DocumentScanningHostedService to dependency injection container
- [ ] T025 Create DocumentNotificationHub in ContosoDashboard/Services/DocumentNotificationHub.cs (SignalR) for real-time notifications to clients (DocumentReady, DocumentQuarantined events)

### Authorization & Security

- [ ] T026 Create DocumentAuthorizationHelper class in ContosoDashboard/Services/DocumentAuthorizationHelper.cs with methods: CanUploadAsync(userId, projectId), CanDownloadAsync(userId, documentId), CanDeleteAsync(userId, documentId) enforcing role-based access control per specification (Employee, Team Lead, Project Manager, Administrator)
- [ ] T027 Add authorization checks to DocumentService methods: Ensure all Download, Delete, List operations validate user permissions before proceeding

### Configuration & Constants

- [ ] T028 [P] Add configuration entries to appsettings.json: ClamAVPath, StorageDirectory (AppData/uploads), MaxFileSize (25MB), SupportedFileTypes, ClamAVTimeout
- [ ] T029 [P] Create DocumentConstants class in ContosoDashboard/Models/DocumentConstants.cs with file type validations, category enums, scan status constants, size limits
- [ ] T030 [P] Create DocumentValidationRules helper in ContosoDashboard/Services/DocumentValidationRules.cs to validate title length (1-255 chars), file size (≤25MB), supported MIME types, category values, tag count (max 5)

**🚩 FOUNDATIONAL PHASE COMPLETE**: All models, migrations, services, and authorization infrastructure in place. All user stories can now proceed in parallel.

---

## Phase 3: US1 & US7 - Upload Document to Project with Virus Scanning (Priority: P1)

**Goal**: Enable employees to upload documents to projects with automatic virus scanning before files become accessible  
**Independent Test**: Can upload a file to a project, verify it's marked "Pending" scan status, confirm background service scans it, verify available for download when "Clear", or quarantined if threat detected

### Implementation - Upload Controller & Pages

- [ ] T031 [P] [US1] Create DocumentUploadController (or code-behind if using Razor components) in ContosoDashboard/Pages/ProjectDocuments.razor.cs with POST handler: UploadFileAsync(projectId, files, metadata)
- [ ] T032 [P] [US1] Create document upload form component in ContosoDashboard/Pages/Components/DocumentUploadForm.razor with file picker, title/description/category fields, associated project selector, tags input, upload progress indicator
- [ ] T033 [US1] Implement UploadFileAsync in DocumentService to: Validate user is assigned to project (authorization check), validate file type, validate file size, save file to storage via IFileStorageService, create Document record with ScanStatus="Pending", enqueue to DocumentScanQueue table, return DocumentId to UI

### Implementation - Virus Scanning Integration

- [ ] T034 [US7] Enhance DocumentService.UploadAsync to enqueue scanning job immediately after file storage (no waiting for scan to complete)
- [ ] T035 [US7] Implement DocumentScanningHostedService.ProcessScanJobAsync to execute full scanning workflow: Dequeue pending job → Run ClamAV scan → Update document ScanStatus (Clear or Quarantined) → Send SignalR notification → Log operation to DocumentAccessLog

### Implementation - Download Controller

- [ ] T036 [US1] Create DocumentDownloadController in ContosoDashboard/Components/DocumentDownloadController.cs (or Razor page handler) with GET method: DownloadFileAsync(documentId)
- [ ] T037 [US1] Implement DownloadFileAsync to: Verify user authorization via DocumentAuthorizationHelper, check document ScanStatus == "Clear" (prevent download if Pending/Scanning/Quarantined), stream file from disk via FileStream, log access to DocumentAccessLog, return file with correct Content-Type header

### Implementation - Real-Time UI Updates

- [ ] T038 [US7] Add SignalR integration to Blazor components to listen for DocumentReady and DocumentQuarantined notifications from DocumentNotificationHub
- [ ] T039 [US7] Update ProjectDocuments.razor to refresh document list state when real-time notification received, display scan status badge (Pending, Scanning, Clear, Quarantined) with appropriate styling

### Validation & Error Handling

- [ ] T040 [P] [US1] Add file type validation in upload form (reject unsupported types before server submission) in ContosoDashboard/Pages/Components/DocumentUploadForm.razor
- [ ] T041 [P] [US1] Add file size validation in upload form (reject >25MB before submission) with user-friendly error message in ContosoDashboard/Pages/Components/DocumentUploadForm.razor
- [ ] T042 [US1] Add try-catch error handling in DocumentUploadController to gracefully handle: storage full errors, file I/O errors, database errors, and return meaningful error messages to client
- [ ] T043 [US7] Add error handling in DocumentScanningHostedService: Catch exceptions during ClamAV execution, implement retry logic (max 3 retries with exponential backoff), mark job as Failed if max retries exceeded

### Logging & Audit

- [ ] T044 [P] [US1] Log all upload operations in DocumentService with: UserId, DocumentId, ProjectId, FileName, FileSize, Status, Timestamp, IpAddress to DocumentAccessLog
- [ ] T045 [P] [US7] Log all scan operations (start, completion, threat detection) to DocumentAccessLog with threat type if detected

### Testing

- [ ] T046 [P] [US1] Create integration test for successful file upload workflow in tests/Integration/DocumentUploadTests.cs: Upload file → Verify document created with ScanStatus="Pending" → Verify file exists on disk → Verify DocumentAccessLog entry created
- [ ] T047 [P] [US1] Create integration test for file size validation in tests/Integration/DocumentUploadTests.cs: Attempt upload of 30MB file → Verify rejected with appropriate error
- [ ] T048 [P] [US1] Create integration test for unsupported file type validation in tests/Integration/DocumentUploadTests.cs: Attempt upload of .exe file → Verify rejected
- [ ] T049 [P] [US7] Create integration test for virus scanning workflow in tests/Integration/DocumentScanningTests.cs: Upload file → Verify scanned by ClamAV → Verify ScanStatus updated → Verify DocumentAccessLog entry created
- [ ] T050 [P] [US7] Create integration test for threat detection in tests/Integration/DocumentScanningTests.cs: Use test virus signature (EICAR) → Verify threat detected → Verify file moved to quarantine → Verify ScanStatus="Quarantined"

**🎯 USER STORY 1 & 7 COMPLETE**: Employees can upload documents to projects with automatic virus scanning. Files are unavailable during scan, available for download when cleared, quarantined if threats detected.

---

## Phase 4: US2 - Personal Document Library (Priority: P1)

**Goal**: Enable employees to store and manage personal documents not associated with projects  
**Independent Test**: Can upload a personal document, verify it appears in "My Documents" view, confirm other users cannot see it, test filtering/sorting in personal library

### Implementation - Personal Documents Page

- [ ] T051 [P] [US2] Create Documents.razor page (My Documents view) in ContosoDashboard/Pages/Documents.razor with: List of user's personal documents, metadata display (title, upload date, file size, category), sorting options (by title, upload date, category, size), filtering by category and date range
- [ ] T052 [P] [US2] Create DocumentsPage code-behind in ContosoDashboard/Pages/Documents.razor.cs to: Load user's personal documents via IDocumentService.ListAsync(userId, null), display with pagination if >100 documents

### Implementation - Personal Upload Handler

- [ ] T053 [US2] Enhance DocumentUploadForm to support "Personal" documents (ProjectId=null) in ContosoDashboard/Pages/Components/DocumentUploadForm.razor, add option to upload without project association
- [ ] T054 [US2] Modify DocumentService.UploadAsync to handle ProjectId=null (personal documents), store with ProjectId=null, route to AppData/uploads/{userId}/personal/{guid}.{extension}

### Implementation - Authorization for Personal Documents

- [ ] T055 [US2] Update DocumentAuthorizationHelper to enforce personal document isolation: CanDownloadAsync returns true only for document owner or admin, CanDeleteAsync returns true only for document owner or admin, CanListAsync filters to only personal documents for non-admin users
- [ ] T056 [US2] Modify DocumentService.ListAsync to apply authorization filters: If not admin, only return documents where UserId=currentUserId or (ProjectId != null and user is project member)

### View & Filtering

- [ ] T057 [P] [US2] Add category filter dropdown to Documents.razor to filter by: Project Documents, Team Resources, Personal Files, Reports, Presentations, Other
- [ ] T058 [P] [US2] Add date range filter to Documents.razor to filter by: Last 7 days, Last 30 days, Last 6 months, Last year, Custom range

### Testing

- [ ] T059 [P] [US2] Create integration test for personal document upload in tests/Integration/PersonalDocumentTests.cs: Upload personal document → Verify ProjectId=null → Verify accessible in My Documents → Verify not visible to other users
- [ ] T060 [P] [US2] Create integration test for personal document authorization in tests/Integration/PersonalDocumentTests.cs: Upload as User A → Attempt access as User B → Verify 403 Forbidden

**🎯 USER STORY 2 COMPLETE**: Employees can maintain personal document libraries isolated from projects.

---

## Phase 5: US3 - Browse and Filter Project Documents (Priority: P1)

**Goal**: Enable team members to quickly discover project documents through filtering and sorting  
**Independent Test**: Can filter documents by category, sort by various fields, apply multiple filters simultaneously, verify correct results

### Implementation - Project Documents Page Enhancement

- [ ] T061 [P] [US3] Create ProjectDocuments.razor page (project-specific documents view) in ContosoDashboard/Pages/ProjectDetails/ProjectDocuments.razor with: List of all project documents with metadata, sorting controls (title, upload date, category, uploader, file size)
- [ ] T062 [P] [US3] Create ProjectDocumentsPage code-behind in ContosoDashboard/Pages/ProjectDetails/ProjectDocuments.razor.cs to: Load project documents via IDocumentService, apply sorting/filtering logic, handle pagination if >100 documents

### Implementation - Filtering Services

- [ ] T063 [US3] Enhance DocumentService.FilterAsync(projectId, filterCriteria) in ContosoDashboard/Services/DocumentService.cs to support: Filter by category (exact match), Filter by upload date range, Filter by uploader (if PM), Combine multiple filters with AND logic
- [ ] T064 [US3] Implement sorting logic in DocumentService with options: Sort by Title (A-Z), Sort by Upload Date (newest first), Sort by File Size (largest first), Sort by Uploader (A-Z)

### Implementation - UI Components

- [ ] T065 [P] [US3] Create category filter component in ContosoDashboard/Pages/Components/CategoryFilter.razor with checkboxes for each category type, multi-select capability, apply/reset buttons
- [ ] T066 [P] [US3] Create date range filter component in ContosoDashboard/Pages/Components/DateRangeFilter.razor with preset options (Last 7 days, Last 30 days, Last 6 months, etc.) and custom date picker
- [ ] T067 [P] [US3] Create sort dropdown component in ContosoDashboard/Pages/Components/SortSelector.razor with sort field selection and ascending/descending toggle

### Implementation - Real-Time Filtering

- [ ] T068 [US3] Implement real-time filter application in ProjectDocuments.razor (debounced filter execution on filter change), no page reload required
- [ ] T069 [US3] Add visual feedback for active filters (display active filter counts, show "Clear Filters" button)

### Testing

- [ ] T070 [P] [US3] Create integration test for category filtering in tests/Integration/DocumentFilterTests.cs: Create documents with different categories → Apply filter → Verify results match filter
- [ ] T071 [P] [US3] Create integration test for date range filtering in tests/Integration/DocumentFilterTests.cs: Create documents over time → Apply date range filter → Verify results within date range
- [ ] T072 [P] [US3] Create integration test for sorting in tests/Integration/DocumentFilterTests.cs: Create documents with various properties → Apply each sort option → Verify correct order
- [ ] T073 [P] [US3] Create integration test for combined filters in tests/Integration/DocumentFilterTests.cs: Apply category AND date range filters simultaneously → Verify results match both criteria
- [ ] T074 [P] [US3] Create performance test in tests/Integration/DocumentFilterTests.cs: Create 1000 documents → Apply filter AND sort → Verify results returned in <1 second

**🎯 USER STORY 3 COMPLETE**: Team members can efficiently discover project documents through flexible filtering and sorting.

---

## Phase 6: US4 - Project Manager Document Governance (Priority: P2)

**Goal**: Provide Project Managers with visibility and control over all documents in their projects  
**Independent Test**: Can view all team member uploads as PM, modify/delete documents as PM, verify Team Leads cannot modify PM documents

### Implementation - PM Document Management UI

- [ ] T075 [P] [US4] Create project document management page in ContosoDashboard/Pages/ProjectDetails/DocumentGovernance.razor (PM-only view) showing: All project documents with uploader info, delete buttons, edit metadata option
- [ ] T076 [P] [US4] Add bulk operations component in ContosoDashboard/Pages/Components/BulkDocumentActions.razor for: Select multiple documents, bulk delete, bulk category update

### Implementation - PM Authorization

- [ ] T077 [US4] Update DocumentAuthorizationHelper.CanDeleteAsync to allow Project Manager to delete any document in their projects in ContosoDashboard/Services/DocumentAuthorizationHelper.cs
- [ ] T078 [US4] Update DocumentAuthorizationHelper.CanEditMetadataAsync to allow Project Manager to edit any document metadata in their projects
- [ ] T079 [US4] Modify DocumentService.ListAsync to return metadata including uploader name when called by Project Manager on their projects

### Implementation - Delete & Update Operations

- [ ] T080 [US4] Implement hard delete in DocumentService.DeleteAsync: Remove file from storage, remove Document record from database, create deletion audit log entry in DocumentAccessLog
- [ ] T081 [US4] Implement metadata update in DocumentService.UpdateAsync: Update title, description, category, tags; validate updated values; log update operation to DocumentAccessLog

### Testing

- [ ] T082 [P] [US4] Create integration test for PM document deletion in tests/Integration/DocumentGovernanceTests.cs: Create document as Employee → Delete as PM → Verify document removed and audit logged
- [ ] T083 [P] [US4] Create integration test for PM metadata edit in tests/Integration/DocumentGovernanceTests.cs: Create document → Edit as PM → Verify metadata updated and audit logged
- [ ] T084 [P] [US4] Create integration test for Team Lead authorization boundary in tests/Integration/DocumentGovernanceTests.cs: Create document in other team's project → Verify Team Lead cannot delete

**🎯 USER STORY 4 COMPLETE**: Project Managers have full visibility and control over project documents.

---

## Phase 7: US5 - Team Leads Share Team Resources (Priority: P2)

**Goal**: Enable Team Leads to manage shared team resource documents  
**Independent Test**: Can upload to Team Resources category, verify team members see it in Team Resources view, confirm it's separate from project documents

### Implementation - Team Resources View

- [ ] T085 [P] [US5] Create Team Resources page in ContosoDashboard/Pages/Team/TeamResources.razor showing: All team resource documents (filtered by category="TeamResources" AND team), metadata display, upload form for Team Leads
- [ ] T086 [P] [US5] Create TeamResourcesPage code-behind in ContosoDashboard/Pages/Team/TeamResources.razor.cs to: Load team resource documents via IDocumentService.FilterAsync(), display only to team members

### Implementation - Team Resource Upload

- [ ] T087 [US5] Enhance DocumentUploadForm to include "Team Resources" category option (only shown to Team Leads) in ContosoDashboard/Pages/Components/DocumentUploadForm.razor
- [ ] T088 [US5] Modify DocumentService.UploadAsync to handle TeamResources category: Store with ProjectId=null, Category="TeamResources", enforce uploader is Team Lead via authorization check

### Implementation - Team Authorization

- [ ] T089 [US5] Update DocumentAuthorizationHelper to implement team-scoped access: CanViewTeamResource returns true for team members, CanUploadTeamResource returns true for Team Leads, CanDeleteTeamResource returns true for Team Lead who uploaded or any admin
- [ ] T090 [US5] Modify DocumentService.ListAsync to support filtering by team: If category="TeamResources", return documents from user's team only

### Testing

- [ ] T091 [P] [US5] Create integration test for team resource upload in tests/Integration/TeamResourcesTests.cs: Upload as Team Lead with category="TeamResources" → Verify accessible to team members → Verify not visible to other teams
- [ ] T092 [P] [US5] Create integration test for team resource authorization in tests/Integration/TeamResourcesTests.cs: Attempt team resource upload as Employee → Verify rejected, Employee cannot access upload form

**🎯 USER STORY 5 COMPLETE**: Team Leads can manage shared team resource documents separate from projects.

---

## Phase 8: US6 - Document Metadata and Full-Text Search (Priority: P3)

**Goal**: Enable users to search documents by title, description, tags, and full-text content  
**Independent Test**: Can search documents by title/tags and verify results match query, search across multiple projects

### Implementation - Search Service

- [ ] T093 [US6] Create DocumentSearchService in ContosoDashboard/Services/DocumentSearchService.cs with: SearchAsync(query, userId, projectId) method, support title/description search, support tag search, apply authorization filters
- [ ] T094 [US6] Implement search logic to: Perform case-insensitive substring match on title and description, support comma-separated tag search (any tag match), combine results, apply user authorization filters

### Implementation - Search UI

- [ ] T095 [P] [US6] Create search component in ContosoDashboard/Pages/Components/DocumentSearch.razor with: Search input field, search scope selector (My Documents, Project Documents, All Accessible), debounced search execution, results display with highlights
- [ ] T096 [P] [US6] Add search to Documents.razor and ProjectDocuments.razor pages to enable search across views

### Implementation - Search Performance

- [ ] T097 [US6] Add database indexes for search optimization in Data Migrations: Index on Title, Index on Description, Index on Tags to improve search query performance
- [ ] T098 [US6] Implement result pagination in DocumentSearchService to limit results (e.g., first 50 results, suggest refinement if >1000 matches)

### Testing

- [ ] T099 [P] [US6] Create integration test for title search in tests/Integration/DocumentSearchTests.cs: Create documents with various titles → Search by partial title → Verify correct results
- [ ] T100 [P] [US6] Create integration test for tag search in tests/Integration/DocumentSearchTests.cs: Create documents with tags → Search by tag → Verify matching documents returned
- [ ] T101 [P] [US6] Create integration test for search authorization in tests/Integration/DocumentSearchTests.cs: Search as User A → Verify personal documents of User B not in results
- [ ] T102 [P] [US6] Create performance test in tests/Integration/DocumentSearchTests.cs: Create 1000 documents → Search → Verify results in <2 seconds

**🎯 USER STORY 6 COMPLETE**: Users can efficiently search documents across the system.

---

## Phase 9: Testing & Quality Assurance

**Purpose**: Comprehensive test coverage and cross-story integration validation

### Unit Tests (Service Layer)

- [ ] T103 [P] Create unit tests for DocumentService authorization methods in tests/Unit/DocumentServiceAuthorizationTests.cs: Test CanDownloadAsync, CanDeleteAsync, CanUploadAsync with various roles
- [ ] T104 [P] Create unit tests for FileStorageService in tests/Unit/FileStorageServiceTests.cs: Test path generation uniqueness, GUID collision prevention, file I/O error handling
- [ ] T105 [P] Create unit tests for DocumentValidationRules in tests/Unit/DocumentValidationTests.cs: Test title length validation, file size limits, MIME type validation, category validation
- [ ] T106 [P] Create unit tests for LocalClamAVService in tests/Unit/ClamAVServiceTests.cs: Test output parsing with various ClamAV responses, threat extraction, error handling

### Integration Tests (Cross-Story Workflows)

- [ ] T107 Create cross-story integration test in tests/Integration/DocumentWorkflowIntegrationTests.cs: Employee uploads project document → Virus scan completes → Team Lead browses documents → Project Manager deletes document → Verify audit trail shows all operations
- [ ] T108 Create authorization integration test in tests/Integration/AuthorizationCrossTests.cs: Verify all role boundaries (Employee, Team Lead, PM, Admin) across all operations

### Background Job Tests

- [ ] T109 [P] Create background service test in tests/Integration/BackgroundJobTests.cs: Enqueue scan job → Verify service processes → Verify document status updated → Verify email notification sent
- [ ] T110 [P] Create background job retry test in tests/Integration/BackgroundJobTests.cs: Simulate ClamAV timeout → Verify retry logic executes → Verify exponential backoff timing

### UI/Component Tests

- [ ] T111 [P] Create Blazor component test for DocumentUploadForm in tests/UI/DocumentUploadFormTests.cs: Test file selection, metadata input validation, upload progress display, error messages
- [ ] T112 [P] Create Blazor component test for filtering components in tests/UI/DocumentFilterComponentTests.cs: Test category filter selection, date range selection, sort option changes

### Security Tests

- [ ] T113 [P] Create security test for path traversal prevention in tests/Security/PathTraversalTests.cs: Attempt upload with special characters (../, ..\\, etc.) → Verify GUID naming prevents traversal
- [ ] T114 [P] Create security test for direct file access in tests/Security/DirectFileAccessTests.cs: Attempt to access storage file directly via URL → Verify 404 (files not in wwwroot)
- [ ] T115 [P] Create security test for authorization bypass in tests/Security/AuthBypassTests.cs: Attempt operations with forged projectId/userId → Verify authorization check prevents access

### Performance Tests

- [ ] T116 Create performance test for upload endpoint in tests/Performance/UploadPerformanceTests.cs: Upload 25MB file → Verify completes in <30 seconds (including background job enqueue)
- [ ] T117 Create performance test for filtering in tests/Performance/FilterPerformanceTests.cs: Query with 1000+ documents → Verify filter+sort returns in <1 second
- [ ] T118 Create performance test for search in tests/Performance/SearchPerformanceTests.cs: Search across 1000+ documents → Verify results in <2 seconds

### Accessibility Tests

- [ ] T119 [P] Create accessibility test for upload form in tests/Accessibility/UploadFormA11yTests.cs: Verify form labels, error messages, progress indicators are screen-reader accessible
- [ ] T120 [P] Create accessibility test for filter components in tests/Accessibility/FilterComponentsA11yTests.cs: Verify keyboard navigation, ARIA labels, focus management

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final quality improvements, documentation, and feature validation

### Documentation & Handoff

- [ ] T121 [P] Document Document Upload feature in README.md at repository root with: Feature overview, user roles and permissions, supported file types, size limits, virus scanning requirements
- [ ] T122 [P] Create user guide in docs/DOCUMENT_UPLOAD_GUIDE.md: Step-by-step upload instructions for each role (Employee, Team Lead, PM, Admin), screenshots, troubleshooting
- [ ] T123 [P] Create administrator guide in docs/DOCUMENT_ADMIN_GUIDE.md: Configuration options (ClamAV path, storage directory), maintenance tasks, troubleshooting, backup/recovery procedures
- [ ] T124 [P] Document API contracts in docs/API_CONTRACTS.md: Upload endpoint, download endpoint, list endpoint, filter parameters, authorization headers, error responses

### Configuration & Environment Setup

- [ ] T125 [P] Update appsettings.Development.json with example configuration for local development: ClamAV path, storage directory path, database connection string test values
- [ ] T126 [P] Update appsettings.Production.json with production configuration needs: Storage directory on production server, ClamAV configuration for production, logging levels
- [ ] T127 [P] Create .env.example template for developers in repository root with required environment variables for local setup

### Logging & Monitoring

- [ ] T128 [P] Add comprehensive logging throughout DocumentService using ILogger: Log every upload, download, delete operation with user, document, and status
- [ ] T129 [P] Add error logging in DocumentScanningHostedService for: Scan failures, retry attempts, max retry failures, unhanded exceptions
- [ ] T130 [P] Create logging configuration in appsettings to set appropriate log levels for document operations (Info for normal ops, Error for failures)

### Error Handling & Edge Cases

- [ ] T131 Add graceful handling for storage full scenario in FileStorageService: Check available disk space before upload, return meaningful error if insufficient space
- [ ] T132 Add handling for session timeout during upload in DocumentUploadController: If session expires during upload, invalidate incomplete file, return error to client
- [ ] T133 Add handling for ClamAV service unavailability in DocumentScanningHostedService: If ClamAV not available, log error, retry with backoff, notify admin if persistent

### Validation & Testing Against Specification

- [ ] T134 [P] Verify all user story acceptance scenarios pass: Run through each scenario in spec.md as manual acceptance test, document results
- [ ] T135 [P] Run quickstart.md procedures end-to-end in fresh environment: Follow setup instructions, verify all steps work, document any issues
- [ ] T136 [P] Verify all functional requirements implemented in Features List in spec.md (FR-001 through FR-020): Each requirement mapped to implementation, tested

### Branch & Deployment Preparation

- [ ] T137 Run full test suite: Execute all unit tests, integration tests, UI tests, verify 0 failures
- [ ] T138 [P] Code review: Review all code changes for style, standards, potential issues
- [ ] T139 [P] Merge PR: Code reviewed → all tests passing → merge feature branch to main
- [ ] T140 Create deployment checklist for feature rollout: Server directory setup, database migration execution, ClamAV configuration, storage directory permissions, environment variables, smoke tests

---

## Phase 11: Implementation Strategy & Execution Plan

### MVP Scope (Minimum Viable Product)

**Phases to complete for MVP**:
1. Phase 1: Setup & Project Structure ✅
2. Phase 2: Foundational (Database & Core Services) ✅
3. Phase 3: US1 & US7 - Upload with Virus Scanning ✅
4. Phase 4: US2 - Personal Document Library ✅
5. Phase 5: US3 - Browse and Filter ✅

**MVP Deliverable**: Employees can upload documents to projects or personal storage with automatic virus scanning. Team members can browse and filter documents. Governance and search are Phase 2 features.

**Effort Estimate**: 2-3 weeks (pair or distributed team of 2)

### Incremental Release Plan

| Release | Phases | User Stories | Effort |
|---------|--------|--------------|--------|
| MVP (v1.0) | 1-5 | US1, US2, US3, US7 | 2-3w |
| Phase 2 (v1.1) | 6 | US4 (governance) | 1w |
| Phase 2 (v1.2) | 7 | US5 (team resources) | 1w |
| Phase 3 (v2.0) | 8 | US6 (search) | 1w |

### Parallel Execution Example

**Week 1 (Setup + Foundational)**:
- Developer A & B together: Phase 1 (Setup)
- Developers continue together: Phase 2 (Foundational)

**Week 2 (User Stories in Parallel)**:
- Developer A: Phase 3 (US1 Upload + US7 Scanning)
- Developer B: Phase 4 (US2 Personal Library)
- Code review and merge daily

**Week 3 (Remaining Stories)**:
- Developer A: Phase 5 (US3 Browse/Filter)
- Developer B: Phase 6 (US4 Governance) or Phase 7 (US5 Team Resources)
- Phase 9 & 10 (testing, documentation) by both

### Task Dependencies

**Hard Blocks** (cannot proceed without):
- T001-T006 (Setup) → blocks all other phases
- T007-T030 (Foundational) → blocks all user story phases

**Soft Dependencies** (can start in parallel after Foundational):
- US1 (Upload) independent of US2 (Personal), US3 (Browse), US4 (Governance), US5 (Team Resources)
- US2 (Personal Library) uses same upload/download infrastructure as US1, but independent feature
- US3 (Browse/Filter) depends on documents existing (US1/US2 must complete first for meaningful test data)
- US4 (Governance) can proceed in parallel with US3, but benefits from data in system
- US6 (Search) can proceed after US3 completes (needs documents)

### Testing Strategy

- **TDD Approach**: Write tests first (T046-T102), ensure they FAIL before implementation
- **Continuous Testing**: Run test suites after each task or daily
- **Integration Validation**: After each phase, run cross-phase integration tests
- **Manual UAT**: After MVP (Phase 5), conduct user acceptance testing against scenarios in spec.md

---

## Summary & Metrics

### Task Count by Type

| Category | Count | IDs |
|----------|-------|-----|
| Setup | 6 | T001-T006 |
| Foundational | 24 | T007-T030 |
| US1 & US7 (Upload + Scanning) | 19 | T031-T049 |
| US2 (Personal Library) | 9 | T050-T058 |
| US3 (Browse & Filter) | 14 | T061-T074 |
| US4 (Governance) | 10 | T075-T084 |
| US5 (Team Resources) | 8 | T085-T092 |
| US6 (Search) | 10 | T093-T102 |
| Testing & QA | 18 | T103-T120 |
| Polish & Documentation | 20 | T121-T140 |
| **Total** | **138 tasks** | **T001-T140** |

### Parallel Opportunities

- **Setup Phase**: T002-T006 can run in parallel (6 tasks)
- **Foundational Phase**: T007-T030 can be grouped into parallel batches:
  - T007-T010: Models (4 parallel)
  - T015-T017: Service interfaces (3 parallel)
  - T028-T030: Configuration (3 parallel)
- **User Story Phases**: All US3-US6 can run in parallel after Foundational (US1 and US2 are sequential on US3 due to data dependency, but US4-US6 can proceed independently)
- **Testing Phase**: All unit tests and performance tests can run in parallel

### Effort Estimates

- **Foundational (Blocking)**: 3-4 days (critical path)
- **User Stories (Parallel)**: 2-3 days each × 4 stories = 2-3 days total (parallel execution)
- **Testing**: 2-3 days
- **Polish & Documentation**: 1-2 days
- **Total MVP**: 2-3 weeks (with team coordination)
- **Full Feature**: 4-6 weeks (including all stories + full testing/documentation)

---

## Success Criteria (from specification)

✅ **Achievement Checklist**:

- [ ] Upload 25MB file completes in ≤30 seconds (including background scan)
- [ ] Browse 1000 documents with filter/sort in ≤1 second response time
- [ ] All 7 user story acceptance scenarios pass 100%
- [ ] 100% of unauthorized access attempts blocked
- [ ] All virus-scanned threats quarantined (0 false negatives accepted)
- [ ] 7-year audit trail maintained (no deleted records until 7 years)
- [ ] All 20 functional requirements (FR-001 through FR-020) verified implemented
- [ ] Test coverage ≥80% for service layer (DocumentService, FileStorageService, ClamAVService)
- [ ] All documentation complete (user guides, admin guides, API contracts)

---

## Notes

- Tasks marked `[P]` can run in parallel with others at same phase level
- Tasks marked `[Story]` (US1-US6) are grouped by user story for independent delivery
- Foundational phase must complete before ANY user story work begins
- After each phase, run integration tests to validate cross-phase interactions
- Commit after each task or 2-3 related tasks to maintain checkpoint history
- All tests must pass before advancing to next phase
