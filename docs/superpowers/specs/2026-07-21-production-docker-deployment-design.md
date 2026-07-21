# Production Docker Deployment Design

## Goal

Add a secure, reviewable Docker deployment under `myserver/` so an existing logical MySQL 5.7 dump can be restored explicitly and the application can subsequently run with `docker compose up -d` or rebuild with `docker compose up -d --build`.

## Scope and constraints

- Keep `Frontend/` and `Backend/` in place and use the repository root as each Docker build context.
- Do not change UI behavior, API contracts, database models, or initializer business logic.
- Do not run containers, migrations, the database initializer, or any operation against the legacy database.
- Never commit environment secrets, database dumps, raw database files, certificates, private keys, logs, build output, or staged imports.
- Preserve same-origin `/api` frontend requests and development CORS for `http://localhost:5173`.

## Architecture

The default Compose project contains three services on a private bridge network:

1. `frontend` builds the React/Vite application and serves `dist/` through Nginx. It is the only publicly exposed application service and proxies `/api/` to `backend:8080`.
2. `backend` publishes and runs the ASP.NET Core 8 API as an unprivileged user. It is reachable only within the Compose network, writes imports to the `temp-imports` named volume, and connects to MySQL using the `database` hostname.
3. `database` runs a configurable MySQL image defaulting to MySQL 5.7, stores data in the `mysql-data` named volume, and exposes no host port.

An optional `phpmyadmin` service is available only through the `tools` profile and binds to `127.0.0.1`.

Traffic flows as follows:

```text
Browser -> Nginx/React -> /api proxy -> ASP.NET Core -> MySQL
```

## Container images

The frontend Dockerfile uses a pinned Node Alpine build stage, installs from the lock file with `npm ci`, runs the existing build script, and copies only `dist/` into a pinned Nginx Alpine runtime. Nginx provides SPA fallback, non-stale `index.html`, long-lived caching for hashed assets, safe baseline headers, streaming-friendly API proxy settings, and a request limit above 10 MiB.

The backend Dockerfile uses matching .NET 8 SDK and ASP.NET 8 runtime stages. It restores and publishes `Backend/Telesale.Api/Telesale.Api.csproj`, preserves the already configured `templates/**` publish assets, creates writable `/app/TempImports`, and runs `Telesale.Api.dll` as an unprivileged runtime user.

Repository-root build contexts are filtered with service-specific Docker ignore files. Sensitive and generated paths are excluded even though the context is the repository root.

## Configuration and secrets

`myserver/.env.example` contains safe defaults and empty secret placeholders. Operators copy it to the ignored `myserver/.env` and provide strong unique values. Compose assembles `ConnectionStrings__TelesaleDb` from `DB_NAME`, `DB_USER`, and `DB_PASSWORD`, using `database` as the host and the existing Pomelo-compatible `Allow User Variables=true` option.

The application database account is distinct from MySQL root. MySQL and the backend are not published on host ports. Provider keys remain runtime environment variables and are never included in frontend build arguments.

The tracked development appsettings connection string will retain local host/database/user defaults but use an empty password. The previously exposed credential must be rotated and revoked manually; Git history is not rewritten automatically.

## Database initializer safety

Startup evaluates `RUN_DATABASE_INITIALIZER` with these rules:

- Explicit `true`: run the existing initializer.
- Explicit `false`: skip it.
- Missing in Development: run it to preserve current behavior.
- Missing in Production or any non-Development environment: skip it.
- Invalid non-empty value: skip it safely and log a warning.

The application logs whether initialization is enabled or skipped. The initializer implementation itself remains unchanged.

## Reverse proxy and cookies

ASP.NET Core processes `X-Forwarded-For` and `X-Forwarded-Proto` before authentication. Trust is limited to private network ranges used for container networking rather than arbitrary internet proxies. This preserves local development and permits `CookieSecurePolicy.SameAsRequest` to see the original HTTPS scheme when TLS terminates at an approved upstream reverse proxy and Nginx forwards that scheme.

Production requests remain same-origin, so CORS is not broadened and credentialed wildcard origins are not introduced.

## Database lifecycle

MySQL starts with an empty named volume and no automatic init-directory import. First deployment starts only `database`, then an operator explicitly restores a logical `.sql` dump with the provided shell or PowerShell script. The scripts verify the file, container health, and whether the target database is non-empty; confirmation is required before overwriting a non-empty target.

Backup scripts run `mysqldump` inside the database container with transaction-safe options, write a timestamped non-empty dump to an operator-selected directory outside the repository, and never overwrite silently. Passwords are read from the container environment rather than process arguments.

No Laravel migrations, Laravel seeds, EF migrations, schema conversion, or automatic initializer run is part of restore. Mixed legacy charset and collation behavior is preserved.

## Operational scripts

Equivalent Bash and PowerShell scripts provide restore, backup, and read-only deployment verification. They resolve Compose relative to `myserver/`, validate prerequisites and configuration, return non-zero on failure, and avoid printing secrets. Verification checks Compose state, database health, backend health and DB health routes through the internal service, frontend HTTP response, status summaries, and recent backend error-level log lines.

## Error handling

- Compose health checks gate dependent service startup where supported.
- Scripts fail early for missing tools, missing configuration, empty required values, unavailable or unhealthy services, invalid files, and failed database commands.
- Restore never deletes its source dump.
- Backup verifies the output and removes only a newly created empty/failed output artifact.
- Verification is read-only and does not start, stop, or modify services.

## Documentation

`myserver/README.md` documents prerequisites, first deployment, daily operation, backup/restore, phpMyAdmin, environment variables, security, cutover, rollback, and the MySQL 5.7 end-of-life risk. `myserver/mysql/README.md` records compatibility constraints and the separate testing required for a future MySQL 8 migration. The root README links to the deployment guide.

## Validation

Safe validation consists of:

- Frontend dependency installation and production build.
- Backend restore, Release build, test suite, and publish to an ignored temporary directory.
- Compose configuration rendering with a temporary validation environment containing dummy secrets.
- Docker image builds if Docker is available, without starting containers.
- Static checks for required deployment structure, initializer gating, forwarded-header ordering, ignored sensitive paths, and absence of known credentials from tracked files.

No database is contacted during validation.

## Manual deployment actions

The System Engineer must create `myserver/.env`, generate strong secrets, rotate and revoke the exposed legacy password, decide whether company policy requires Git history cleanup, create and secure the final logical dump, restore it during a maintenance window, validate counts and Thai text, configure approved TLS termination, complete the cutover checklist, and retain rollback backups and the unchanged old server during the initial release window.
