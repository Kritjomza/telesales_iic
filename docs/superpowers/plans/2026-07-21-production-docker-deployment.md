# Production Docker Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a secure Docker Compose deployment under `myserver/` that supports explicit MySQL 5.7 logical restore, one-command normal startup, and rebuilds after source changes.

**Architecture:** Nginx serves the compiled React application and is the only public service. It proxies same-origin API traffic to a private ASP.NET Core service, which connects to a private MySQL service and uses named volumes for MySQL data and staged imports.

**Tech Stack:** Docker Compose, Docker multi-stage builds, Nginx Alpine, Node Alpine, React 19/Vite 5, ASP.NET Core 8, EF Core/Pomelo, MySQL 5.7, Bash, PowerShell.

## Global Constraints

- Do not run containers, migrations, database initialization, database restore, or access the legacy database.
- Do not expose or commit secrets, dumps, raw database files, certificates, private keys, logs, build output, or staged imports.
- Do not change UI behavior, API contracts, database models, or initializer business logic.
- Preserve frontend `/api`, cookie authentication, and local development CORS.
- Use repository-root Docker build contexts and keep `Frontend/` and `Backend/` in place.

---

### Task 1: Backend startup safety

**Files:**
- Create: `Backend/Telesale.Api/Helpers/DatabaseInitializerPolicy.cs`
- Create: `Backend/Telesale.Api.Tests/DatabaseInitializerPolicyTests.cs`
- Modify: `Backend/Telesale.Api/Program.cs`
- Modify: `Backend/Telesale.Api/appsettings.Development.json`

**Interfaces:**
- Produces: `DatabaseInitializerPolicy.ShouldRun(string? setting, bool isDevelopment, out bool invalid)`.
- Consumes: `RUN_DATABASE_INITIALIZER`, `IHostEnvironment.IsDevelopment()`, ASP.NET forwarded-header middleware.

- [ ] Write policy tests for explicit true, explicit false, Development default, Production default, and invalid input.
- [ ] Run the focused tests and verify they fail because the policy does not exist.
- [ ] Implement the policy and rerun the focused tests.
- [ ] Gate the existing initializer call with the policy and clear enabled/skipped/invalid logging.
- [ ] Configure `ForwardedHeadersOptions` for `X-Forwarded-For` and `X-Forwarded-Proto`, clearing broad defaults and trusting RFC1918/Compose private networks only.
- [ ] Place `UseForwardedHeaders()` before CORS, authentication, and authorization.
- [ ] Blank the tracked development password without changing local host/database/user defaults.
- [ ] Run backend tests and Release build.

### Task 2: Compose and container images

**Files:**
- Create: `myserver/compose.yaml`
- Replace: `myserver/.env.example`
- Create: `myserver/.gitignore`
- Create: `myserver/frontend/Dockerfile`
- Create: `myserver/frontend/.dockerignore`
- Create: `myserver/frontend/nginx.conf`
- Create: `myserver/backend/Dockerfile`
- Create: `myserver/backend/.dockerignore`

**Interfaces:**
- Compose services: `frontend`, `backend`, `database`, optional `phpmyadmin` profile `tools`.
- Named volumes: `mysql-data`, `temp-imports`.
- Private network: `app-network`.

- [ ] Add a static deployment validator that checks required files and Compose invariants, then run it to observe missing-file failures.
- [ ] Implement Compose with repository-root build contexts, health-gated dependencies, private backend/database, localhost-only optional phpMyAdmin, and named volumes.
- [ ] Implement pinned multi-stage frontend and backend images.
- [ ] Implement Nginx SPA routing, API streaming proxy, upload/timeouts, headers, and cache policy.
- [ ] Add safe environment placeholders and service-specific ignore rules.
- [ ] Rerun static validation and render `docker compose config` with dummy secrets when Docker is available.

### Task 3: Database lifecycle scripts

**Files:**
- Create: `myserver/scripts/restore-database.sh`
- Create: `myserver/scripts/restore-database.ps1`
- Create: `myserver/scripts/backup-database.sh`
- Create: `myserver/scripts/backup-database.ps1`
- Create: `myserver/scripts/verify-deployment.sh`
- Create: `myserver/scripts/verify-deployment.ps1`

**Interfaces:**
- Restore accepts one explicit `.sql` path.
- Backup accepts one explicit output directory.
- Verify accepts no mutation options and reads `FRONTEND_PORT` from `.env`.

- [ ] Extend static validation with script safety invariants and observe failures before files exist.
- [ ] Implement restore scripts with file checks, healthy-container requirement, non-empty database confirmation, stdin import, and container-environment credentials.
- [ ] Implement backup scripts with required `mysqldump` flags, `utf8mb4`, timestamp collision protection, and non-empty output verification.
- [ ] Implement read-only verification scripts for prerequisites, environment, Compose config, service/health endpoints, status, and sanitized recent errors.
- [ ] Parse PowerShell scripts and run shell syntax checking when their interpreters are available.

### Task 4: Documentation and repository safeguards

**Files:**
- Create: `myserver/README.md`
- Create: `myserver/mysql/README.md`
- Modify: `.gitignore`
- Modify: `README.md`

**Interfaces:**
- Documentation commands operate from `telesale_iic_new/myserver`.

- [ ] Extend static validation with required documentation topics and ignore patterns, then observe failures.
- [ ] Document architecture, prerequisites, first deployment for Bash and PowerShell, daily operations, restore/backup, optional phpMyAdmin, every environment variable, security, cutover, rollback, and manual credential remediation.
- [ ] Document MySQL 5.7 compatibility, EOL status, mixed charset preservation, and separate MySQL 8 migration requirements.
- [ ] Add comprehensive root ignore patterns and a concise root README deployment link.
- [ ] Verify sensitive sample paths are ignored and no forbidden credential remains in tracked content.

### Task 5: Full safe validation

**Files:**
- Create temporarily, then leave ignored: `.tmp/publish/`

**Interfaces:**
- Consumes all files from Tasks 1-4.

- [ ] Run `npm ci` and `npm run build` in `Frontend/`.
- [ ] Run `dotnet restore`, `dotnet build -c Release`, the specified backend test project, and `dotnet publish` to `.tmp/publish/`.
- [ ] Run the deployment static validator and script syntax checks.
- [ ] Run `docker compose -f myserver/compose.yaml config` with dummy secret values if Docker is available.
- [ ] Build Docker images if Docker is available; do not start them.
- [ ] Run `git diff --check`, inspect the complete diff, and report exact pass/fail/skip results without claiming production readiness if a required check fails.
