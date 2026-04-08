<!-- 
============================================================================
SYNC IMPACT REPORT — Constitution Created (v1.0.0)
============================================================================

VERSION CHANGE: None → 1.0.0 (New Constitution)

NEW CONTENT:
✅ 5 Core Principles established:
   • Security & Authorization First
   • Database Schema Integrity
   • Service-Oriented Architecture
   • Test-First Quality
   • Specification-Driven Development (SDD)

✅ Development Standards section with:
   • Language/framework specifications (C#, ASP.NET Core 10.0, Blazor Server)
   • Security requirements (Auth, HTTPS, Headers, Isolation)
   • Data isolation patterns

✅ Workflow & Amendments section with:
   • Amendment process requiring proposal, rationale, impact analysis
   • Compliance review as quality gate

RATIFICATION DATE: 2026-04-08
LAST AMENDED DATE: 2026-04-08

DEPENDENT TEMPLATES STATUS:
✅ spec-template.md — Aligned: User story prioritization matches SDD principle
✅ plan-template.md — Aligned: Constitution Check gate present, SDD workflow referenced
✅ tasks-template.md — Aligned: Task organization by user story supports SDD phases
⚠️  agents/speckit.*.agent.md — Review for any hardcoded version references (none found)
⚠️  README.md — Update training context section to reference constitution

FOLLOW-UP ITEMS:
• Document compliance verification process in project wiki
• Add constitution review to specification quality gates (speckit.analyze)
• Establish amendment approval workflow for production use

NO BREAKING CHANGES: This is the initial constitution, no prior versions exist.

============================================================================
-->

# ContosoDashboard Constitution

A specification-driven development framework for building the ContosoDashboard training application with emphasis on security, database integrity, service-oriented architecture, and comprehensive testing.

## Core Principles

### I. Security & Authorization First
All features MUST implement role-based access control (RBAC) from inception. Authorization checks are non-negotiable at service layer, page layer, and data access layer. Never trust client-side authorization. Implement defense-in-depth: multiple layers of authorization verification prevent single points of failure. User isolation requirements: each user sees only authorized data. No hardcoded secrets; configuration-driven for all credentials.

### II. Database Schema Integrity
Database migrations MUST be reversible and tested before deployment. Schema changes require entity model updates and EF Core migration files. Foreign key relationships are mandatory for data integrity. Use DbContext validation to enforce business rules. Null reference handling via nullable properties in models. All database changes documented in migration descriptions with rationale. Breaking schema changes prohibited without major version bump.

### III. Service-Oriented Architecture
Business logic lives in service classes, never in page/component code. Services define clear contracts via interfaces. All services registered in dependency injection. Service methods support async/await patterns. Data access ONLY through services—pages never query DbContext directly. Inter-service communication through published methods, not direct coupling. Service implementations testable in isolation.

### IV. Test-First Quality
Specification-driven development: specifications precede implementation. Unit tests required for all service classes (minimum 80% code coverage). Integration tests for critical user workflows (authentication, authorization, data access). Test scenarios defined in specification acceptance criteria. Test failures treated as bugs, not documentation issues. Testing frameworks standardized: xUnit for unit tests, Selenium/Playwright for E2E.

### V. Specification-Driven Development (SDD)
All work originates from written specifications. Specification workflow: Specify → Plan → Task → Implement → Validate. Specifications document user stories, acceptance criteria, and business rules before code begins. Implementation tasks derive from specifications; no work without task tickets. All specifications undergo quality review via speckit.analyze. Changes to existing features require updated specifications.

## Development Standards

### Code Quality
- Language: C# 10+, nullable reference types enabled
- Framework: ASP.NET Core 10.0 (Blazor Server)
- Database: SQL Server (Docker) for development
- All changed code must build without errors
- Warnings addressed or documented with justification

### Security Requirements
- Authentication: Cookie-based (training) or OAuth 2.0 (production)
- HTTPS enforced in all non-development environments
- Password requirements: minimum 8 characters, complexity
- Session timeout: 8 hours with sliding expiration
- SQL injection prevention: parameterized queries only
- CSRF protection enabled on form submissions
- Security headers: Content-Security-Policy, X-Frame-Options, X-XSS-Protection

### Data Isolation
- Users see only their data or data they're authorized to access
- Service methods include userId parameter for row-level security
- No queryable data endpoints without authorization verification
- Audit logging for sensitive operations (file uploads, deletions)

## Workflow & Amendments

### Amendment Process
Constitution changes require:
1. Written proposal documenting principle additions/modifications
2. Rationale explaining business or technical necessity
3. Impact analysis on existing implementations
4. Approval from project lead (training context optional)
5. Documentation in amendment comment with date
6. Version bump per semantic versioning

### Compliance Review
All specifications, plans, and implementations validated against constitution as quality gate. Failed compliance blocks task closure. Principle violations treated as bugs requiring remediation.

---

**Version**: 1.0.0 | **Ratified**: 2026-04-08 | **Last Amended**: 2026-04-08
