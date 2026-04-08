# Quickstart Guide: Document Upload and Management Feature

**Target**: Local development environment (macOS with Docker)  
**Duration**: ~15 minutes to full setup  
**Last Updated**: April 8, 2026

---

## Prerequisites

Before starting, ensure you have:

- ✅ .NET 10 SDK installed (`dotnet --version` should show 10.x)
- ✅ Docker Desktop installed and running
- ✅ SQL Server 2022 container running (see below)
- ✅ Git configured with ContosoDashboard repository cloned
- ✅ VS Code or Visual Studio with C# extension

**Check you have everything**:
```bash
dotnet --version    # Should show 10.x
docker --version    # Should show version info
git --version       # Should show version info
```

---

## Step 1: Start SQL Server in Docker (if not already running)

The document feature requires SQL Server 2022. Run this Docker command:

```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  --name sql-contoso \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

**Verify it's running**:
```bash
docker ps | grep sql-contoso
# Should show container status as "Up"

# Or check connection (if you have sqlcmd installed)
sqlcmd -S localhost,1433 -U sa -P 'YourStrong!Passw0rd' -Q "SELECT @@VERSION"
```

---

## Step 2: Update Database Connection String

The local SQL Server is running on `localhost:1433` with:
- **Username**: `sa`
- **Password**: `YourStrong!Passw0rd`

**Update** [ContosoDashboard/appsettings.Development.json](ContosoDashboard/appsettings.Development.json):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=ContosoDashboard;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=false;TrustServerCertificate=true;Connection Timeout=30;"
  }
}
```

**Key settings**:
- `Encrypt=false` — Development environment (not recommended for production)
- `TrustServerCertificate=true` — Skip certificate validation locally
- `Connection Timeout=30` — Allow database initialization time

---

## Step 3: Run Existing Migrations

The project has existing migrations for User, Project, and Task entities. Apply them:

```bash
cd /path/to/ContosoDashboard/ContosoDashboard

# Restore NuGet packages
dotnet restore

# Apply existing migrations to database
dotnet ef database update

# Verify migration applied successfully
dotnet ef migrations list
```

**Expected output**:
```
Build started...
Build succeeded.
Applying migration '..._Initial'
Done.
```

If you see an error about database connection, verify:
1. SQL Server container is running (`docker ps`)
2. Connection string is correct in appsettings.Development.json
3. Password matches the Docker environment variable

---

## Step 4: Create Document Management Tables

Once existing migrations succeeed, create new migrations for document feature entities:

```bash
# Create migration for document feature
dotnet ef migrations add AddDocumentManagementEntities

# Apply migration
dotnet ef database update
```

**What this does**:
- Creates `Document` table with indexes
- Creates `DocumentAccessLog` table for audit trail
- Creates `FileQuarantine` table for quarantined files
- Sets up foreign key relationships

**Verify tables created**:
```bash
# List all tables (if you have sqlcmd)
sqlcmd -S localhost,1433 -U sa -P 'YourStrong!Passw0rd' \
  -d ContosoDashboard \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'"

# Should include: Document, DocumentAccessLog, FileQuarantine
```

---

## Step 5: Create Upload Storage Directory

Documents are stored outside `wwwroot` for security. Create the directory:

```bash
# Create AppData/uploads directory
mkdir -p ContosoDashboard/AppData/uploads

# Verify it exists
ls -la ContosoDashboard/AppData/
# Should show: uploads (directory)
```

---

## Step 6: Build Project

Build the project to ensure all code compiles:

```bash
cd ContosoDashboard
dotnet build
```

**Expected**:
- ✅ `Build succeeded` with warnings (if any) about nullable references
- ✅ Output in `bin/Debug/net10.0/`

---

## Step 7: Run the Application

Start the Blazor Server application:

```bash
dotnet watch run
```

**Expected output**:
```
Building...
Build succeeded.
watch : Unable to find a project to watch...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to exit
```

The application will automatically reload on file changes.

---

## Step 8: Login and Verify

1. **Open** `https://localhost:5001` in your browser
2. **Handle SSL warning** — Click "Advanced" → "Proceed" (self-signed cert in development)
3. **Login** with test user:
   - Email: `camille.nicole@contoso.com` (Project Manager)
   - Select from dropdown, click "Login"
4. **Verify** you see dashboard with your user info

---

## Step 9: Test Document Upload (Manual)

Once logged in, test the document upload feature:

### 9a. Upload a Document

1. Navigate to **Projects** → select a project
2. Click **"Upload Document"** button
3. Select a test file (PDF, Word doc, image, etc.)
4. Fill in metadata:
   - **Title**: "Test Document"
   - **Description**: "Test upload to verify feature works"
   - **Category**: "Project Documents"
5. Click **"Upload"**
6. Monitor upload progress and confirm completion

### 9b. Verify File Stored

```bash
# Check if file appears in storage directory
ls -la ContosoDashboard/AppData/uploads/

# Should see: {userId}/ directory
# Inside: {projectId}/ directory
# Inside: [guid].pdf (or appropriate extension)
```

### 9c. Verify Database Record

```bash
# Query documents (if using sqlcmd)
sqlcmd -S localhost,1433 -U sa -P 'YourStrong!Passw0rd' \
  -d ContosoDashboard \
  -Q "SELECT TOP 5 DocumentId, Title, Category, StoragePath, ScanStatus FROM Document ORDER BY UploadDate DESC"
```

**Expected**: New document appears with:
- **ScanStatus**: "Pending" (ClamAV scanning in progress) or "Clear" (scanning complete)
- **StoragePath**: Valid path matching pattern
- **Title**: What you entered

---

## Troubleshooting

### "Connection to database failed"

**Problem**: `SqlException: Connection timeout`

**Solutions**:
1. Verify SQL Server is running: `docker ps | grep sql-contoso`
2. If not running, start it: `docker start sql-contoso`
3. Check connection string in `appsettings.Development.json`
4. Try connecting manually: `sqlcmd -S localhost,1433 -U sa -P 'YourStrong!Passw0rd'`

### "Migration pending"

**Problem**: `There are pending migrations`

**Solution**:
```bash
dotnet ef database update
```

### "AppData/uploads directory not found"

**Problem**: File upload fails with "Path not found"

**Solution**:
```bash
mkdir -p ContosoDashboard/AppData/uploads
chmod 755 ContosoDashboard/AppData/uploads
```

### "ClamAV not found" (future when integrated)

**Problem**: `ClamAV.Net package error`

**Solution**:
1. Ensure ClamAV.Net NuGet package is installed: `dotnet package list`
2. Restore packages: `dotnet restore`
3. For offline testing, ClamAV scanning can be mocked during development

### SSL Certificate Issues

**Problem**: Browser shows "not secure" or refuses connection

**Solution**:
- Development uses self-signed certificate (expected)
- In **Chrome/Edge**: Click advanced → proceed anyway
- In **Firefox**: Click advanced → accept risk and continue
- Certificates stored in `ContosoDashboard/Properties/launchSettings.json`

---

## Development Workflow

### Running Tests (once test project created)

```bash
cd ContosoDashboard.Tests
dotnet test
```

### Code Changes

Changes to `.cs` files automatically recompile via `dotnet watch run`. Browser may need refresh to see Blazor page changes.

### Database Schema Changes

If modifying Document/DocumentAccessLog/FileQuarantine models:

```bash
# Create migration
dotnet ef migrations add DescribingYourChange

# Apply migration
dotnet ef database update

# To undo last migration
dotnet ef database update [previous-migration-name]
```

---

## Debugging

### Debug Mode

To debug with breakpoints:

1. **In VS Code**:
   - Open `.vscode/launch.json`
   - Click "Run and Debug" (Ctrl+Shift+D)
   - Select ".NET Core" configuration
   - Set breakpoints in code
   - Click play (F5) to debug

2. **In Visual Studio**:
   - Click Debug → Start Debugging (F5)
   - Set breakpoints in code editor
   - App runs with debugger attached

### View Database in SSMS

If you have SQL Server Management Studio installed:

1. **Connect** to `localhost,1433`
2. **Login** with: Username `sa`, Password `YourStrong!Passw0rd`
3. **Browse** database `ContosoDashboard`
4. **Explore** tables: Document, DocumentAccessLog, FileQuarantine

---

## Docker Database Management

### Stop SQL Server

```bash
docker stop sql-contoso
```

### Start SQL Server (after stopping)

```bash
docker start sql-contoso
```

### Delete Container (fully reset database)

```bash
docker stop sql-contoso
docker rm sql-contoso

# Then run the docker run command again to create fresh database
```

### View Container Logs

```bash
docker logs sql-contoso
```

---

## Environment Checklist

Before considering setup complete, verify:

- ✅ Docker SQL Server running (`docker ps` shows sql-contoso)
- ✅ Database `ContosoDashboard` created
- ✅ Existing migrations applied successfully
- ✅ Document management tables created (Document, DocumentAccessLog, FileQuarantine)
- ✅ AppData/uploads directory exists and is writable
- ✅ Application builds without errors (`dotnet build`)
- ✅ Application runs (`dotnet watch run`)
- ✅ Can login with test user
- ✅ Dashboard displays without errors
- ✅ Can navigate to Projects and upload test document

---

## Next Steps

Once setup is complete:

1. **Review** [specs/1-document-upload/plan.md](../plan.md) for architecture overview
2. **Review** [specs/1-document-upload/data-model.md](../data-model.md) for database schema
3. **Start implementation** using tasks from [specs/1-document-upload/tasks.md](../tasks.md)
4. **Run tests** as you implement each component

---

## Additional Resources

- [Microsoft SQL Server in Docker](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker?view=sql-server-ver16)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [ASP.NET Core Blazor Server](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-8.0)
- [ClamAV Documentation](https://www.clamav.net/)

---

**Setup Status**: Ready for development ✅

If you encounter issues, check the **Troubleshooting** section or review the full specification at [specs/1-document-upload/spec.md](../spec.md).
