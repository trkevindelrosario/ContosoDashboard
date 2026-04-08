# Research Phase: Document Upload and Management Feature

**Date**: April 8, 2026  
**Status**: Complete — All research questions resolved via clarifications  
**Input Source**: Stakeholder requirements + clarification questions (5 Q&A)

---

## Executive Summary

All critical research questions have been resolved through the clarification workflow (Session 2026-04-08). No blocking unknowns remain. The feature is ready for full design and implementation.

---

## Research Questions & Findings

### 1. Virus Scanning Service Selection

**Question**: Which antivirus/malware scanning service should be used?

**Context**: Training environment requires offline operation (no external APIs). Local file scanning is preferred.

**Options Evaluated**:
- ❌ **VirusTotal API** — Cloud-based, requires internet, breaks offline mode
- ❌ **Windows Defender API** — Platform-specific, not available on macOS
- ❌ **Mock/Stub** — No real security scanning, not acceptable
- ✅ **ClamAV** — Open-source, local processing, cross-platform

**Decision**: ClamAV (Open-Source Antivirus Engine)

**Rationale**: 
- Supports offline training environment (no external API calls required)
- Cross-platform compatible (Linux/macOS/Windows)
- Free and open-source (no licensing complexity)
- Reliable malware detection with regular definition updates
- Easy integration via ClamAV.Net NuGet package

**Implementation Pattern**:
```csharp
// Use ClamAV.Net package or command-line interface
// Workflow: Upload file → Run ClamAV scan → Mark as Clear or Quarantine
// Scanning: Run as background async task (don't block upload response)
// Database integration: ScanStatus enum (Pending, Scanning, Clear, Quarantined)
```

**Risks Mitigated**:
- Scan duration: Use async/background tasks to prevent UI blocking
- Signature updates: ClamAV provides regular definition updates via freshclam
- False positives: Maintain quarantine queue for administrator review

---

### 2. Storage Quota Strategy

**Question**: Should per-user or per-project storage quotas be enforced?

**Options Evaluated**:
- ✅ **No quotas** — Unlimited storage for all users (training focus)
- ❌ **Per-user quota** — Each user with 500MB limit (adds complexity)
- ❌ **Per-project quota** — Each project with 1GB limit (requires tracking)
- ❌ **Tiered quotas** — Role-based limits (adds policy management)

**Decision**: No Quotas (Unlimited Storage)

**Rationale**:
- Training focus is on security and architecture, not quota management
- Simplified DocumentService validation logic (no quota checks)
- Server storage available for training scenarios
- Quota management can be added in Phase 2 if needed

**Future Scope**: Phase 2 could add:
- Per-user storage tracking
- Quota enforcement with clear error messages
- Admin dashboard for storage usage monitoring

---

### 3. Document Deletion Strategy

**Question**: Should deleted documents be recoverable (soft delete) or permanently removed (hard delete)?

**Options Evaluated**:
- ✅ **Hard delete** — Immediate permanent removal from DB and filesystem
- ❌ **Soft delete (7 years)** — Keep in DB, recoverable by admins, expensive storage
- ❌ **Soft delete (30 days)** — Archive with auto-purge, adds complexity
- ❌ **Configurable** — Admin choice per document (unnecessary complexity)

**Decision**: Hard Delete (Immediate Permanent Removal)

**Rationale**:
- Training simplicity: No deleted_at or IsDeleted columns needed
- Matches specification intent: "permanently removed after user confirmation"
- Clear user expectation: Deletion means gone, not hidden
- Audit trail preserved: DocumentAccessLog records deletion event with timestamp

**Implementation Pattern**:
```csharp
// Delete workflow:
// 1. User confirms deletion
// 2. Check authorization (own doc or Project Manager)
// 3. Create audit log entry: Operation=Delete, timestamp, user ID
// 4. Delete from filesystem: File.Delete(storagePath)
// 5. Delete from database: _context.Documents.Remove(document)
// 6. Commit transaction
// Result: Document gone forever, but audit trail preserved
```

**Data Retention**:
- Deleted document audit records retained for 7 years (GDPR compliance)
- After 7 years, audit records can be purged administratively

---

### 4. Upload Rate Limiting

**Question**: Should concurrent upload limits be enforced per user or system-wide?

**Options Evaluated**:
- ✅ **No rate limits** — Unlimited concurrent uploads (training)
- ❌ **Per-user limit** — Max 5 concurrent uploads per user (adds throttling)
- ❌ **System-wide limit** — Max 20 concurrent uploads total (resource management)
- ❌ **Tiered limits** — Role-based limits (policy complexity)

**Decision**: No Rate Limits (Unlimited Concurrent Uploads)

**Rationale**:
- Training focus is architecture, not performance throttling
- Simplifies DocumentService: No concurrency checking logic
- Rate limiting can be added later with Redis/distributed caching
- Server resources sufficient for training load

**Future Scope**: Phase 2 could add:
- Distributed rate limiter using DistributedCache
- Per-user concurrent upload limits
- System-wide capacity limits with queue system
- Admin controls for rate limit policy

---

### 5. Audit Record Retention Period

**Question**: How long should deleted document audit records be retained?

**Context**: Hard-deleted documents are permanently gone, but audit trails should be kept for compliance.

**Options Evaluated**:
- ❌ **Indefinite** — Keep all records forever (maximum compliance, expensive)
- ❌ **1 year** — Minimal retention, may not satisfy audits
- ✅ **7 years** — Standard data retention for work documents (GDPR compliance)

**Decision**: 7-Year Retention for Deleted Document Audit Records

**Rationale**:
- Matches GDPR standard data retention requirement
- Typical corporate compliance period for work documents
- Balances legal coverage with storage efficiency
- Allows purging of old records administratively

**Implementation Pattern**:
```csharp
// Audit log retention policy:
// 1. All DocumentAccessLog records retained indefinitely by default
// 2. For deleted documents: Retain Operation=Delete audit records for 7 years
// 3. After 7 years: Admin can run purge job to delete old deletion records
// 4. Non-deletion operations: Retained indefinitely (downloads, access denied, etc.)

// Admin purge job (manual or scheduled):
// DELETE FROM DocumentAccessLog 
// WHERE Operation = 'Delete' AND Timestamp < DateTime.UtcNow.AddYears(-7)
```

---

## Technical Context - RESOLVED

| Category | Finding | Status |
|----------|---------|--------|
| **Antivirus** | ClamAV (local, async scanning) | ✅ Resolved |
| **Quotas** | None (unlimited storage) | ✅ Resolved |
| **Deletion** | Hard delete (permanent) | ✅ Resolved |
| **Rate Limiting** | No limits (unlimited concurrent) | ✅ Resolved |
| **Retention** | 7 years for deletion audit records | ✅ Resolved |
| **File Storage** | AppData/uploads with GUID filenames | ✅ Specified in plan |
| **Framework** | ASP.NET Core 10.0 / Blazor Server | ✅ Specified in plan |
| **Database** | SQL Server 2022 (Docker) | ✅ Specified in plan |
| **Testing** | xUnit (unit), integration tests | ✅ Specified in plan |

---

## Findings Summary with Dependencies

**No blocking research items identified.** All research questions have been resolved:

1. ✅ **ClamAV antivirus** — Ready for NuGet integration
2. ✅ **No quotas** — Simplifies service layer
3. ✅ **Hard delete** — Removes need for soft-delete columns
4. ✅ **No rate limiting** — Removes throttling logic
5. ✅ **7-year retention** — Compatible with existing audit log design

**Implementation can proceed directly to Phase 1 design and Phase 2 tasks.**

---

## Phase 0 Completion Checklist

- ✅ Clarification questions answered (5/5)
- ✅ Unknown technical decisions resolved
- ✅ Architecture patterns identified
- ✅ Risk mitigation strategies defined
- ✅ No blocking dependencies identified
- ✅ All answers integrated into plan.md

**Phase 0 Status**: 🟢 **COMPLETE**

---

## Next Phases

**Phase 1 Design** (in progress):
- ✅ plan.md (implementation plan) — Complete
- ⏳ data-model.md (entity definitions) — Ready for generation
- ⏳ contracts/ (API specifications) — Ready for generation
- ⏳ quickstart.md (setup guide) — Ready for generation

**Phase 2 Implementation** (pending plan completion):
- ⏳ tasks.md (35 actionable tasks) — Ready via speckit.tasks

---

**Research Session Owner**: AI Agent  
**Session Date**: 2026-04-08  
**Approval Status**: Ready for design and implementation
