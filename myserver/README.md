# Telesale Docker deployment

This directory contains the production deployment for the React and ASP.NET Core application. Run all commands in this guide from `telesale_iic_new/myserver`.

```text
Browser -> Nginx/React frontend -> /api proxy -> ASP.NET Core backend -> MySQL
```

Only Nginx publishes an application port. The backend and database are private Compose services. MySQL data and staged import files live in named Docker volumes.

## Prerequisites

- Git, Docker Engine or Docker Desktop, and Docker Compose v2 (`docker compose`).
- Port `${FRONTEND_PORT}` (80 by default) must be available. Port 8890 is needed only for optional phpMyAdmin.
- A small deployment should have at least 2 CPU cores, 4 GB RAM, and enough durable disk for the database, backups, images, and growth. Size production resources from measured usage.
- Bash for `.sh` scripts or PowerShell 7+ for `.ps1` scripts.

## First deployment

Clone or pull the repository, enter `myserver`, and create the untracked runtime environment file:

```sh
cp .env.example .env
```

PowerShell:

```powershell
Copy-Item .env.example .env
```

Edit `.env`. Set strong, unique `DB_PASSWORD` and `DB_ROOT_PASSWORD` values and any required AI provider key. Never commit `.env`.

Validate configuration, start only MySQL, restore the full logical dump, then build and start the application:

```sh
docker compose config
docker compose up -d database
./scripts/restore-database.sh /secure/path/sale_backup.sql
docker compose up -d --build
docker compose ps
./scripts/verify-deployment.sh
```

PowerShell:

```powershell
docker compose config
docker compose up -d database
.\scripts\restore-database.ps1 C:\secure\sale_backup.sql
docker compose up -d --build
docker compose ps
.\scripts\verify-deployment.ps1
```

The restore scripts require a healthy database container. They prompt before importing into a database that already contains tables. `.sql.gz` files must be decompressed outside the repository first.

## Normal operation

```sh
# Start or reconcile existing containers
docker compose up -d

# Rebuild after Frontend/ or Backend/ changes
docker compose up -d --build

# Status and logs
docker compose ps
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f database

# Stop and restart without deleting containers or volumes
docker compose stop
docker compose start

# Remove containers and the network, preserving named volumes
docker compose down
```

**Never run `docker compose down -v` unless you intentionally want to delete all MySQL data and staged TempImports data.**

## Database restore

A migration creates or changes schema. A seed inserts predefined initial data. A full dump of the existing `sale` database already contains its schema and current data, so do not run Laravel `php artisan migrate`, Laravel `php artisan db:seed`, EF migrations, or automatic initialization for the normal restore.

Do not copy the old `mysql/data` directory. Physical MySQL files depend on exact server/storage state and are not a safe portable migration method. Use a logical dump:

```sh
./scripts/restore-database.sh /secure/path/sale_backup.sql
```

```powershell
.\scripts\restore-database.ps1 C:\secure\sale_backup.sql
```

Keep dumps outside the repository. Before cutover, record table names and row counts on the old system. After restore, compare them on the new system, allowing only understood differences such as activity after the initial test dump. Check representative Thai text and collation-sensitive searches.

`RUN_DATABASE_INITIALIZER=false` is the production default. Set it to `true` only for a separately reviewed operation. Importing a full database normally does not need the custom initializer.

## Database backup

Choose a secure directory outside the repository:

```sh
./scripts/backup-database.sh /secure/backups
```

```powershell
.\scripts\backup-database.ps1 C:\secure\backups
```

The result is a timestamped logical dump using a consistent transaction, routines, triggers, and events. Backups contain sensitive customer data. Encrypt them, restrict access, test restoration periodically, and copy them to approved off-server storage.

## Optional phpMyAdmin

phpMyAdmin does not store database data; it connects to the `database` service. It does not start normally. Start it explicitly:

```sh
docker compose --profile tools up -d phpmyadmin
```

It binds to `127.0.0.1:${PHPMYADMIN_PORT}` only. For remote administration, use an SSH tunnel or an approved authenticated reverse proxy; never publish it directly to the internet.

## Environment variables

All values are runtime settings; none are frontend build-time secrets.

| Variable | Required | Secret | Purpose |
|---|---:|---:|---|
| `COMPOSE_PROJECT_NAME` | No | No | Compose resource prefix; default `telesale_iic`. |
| `FRONTEND_PORT` | No | No | Public Nginx host port; default `80`. |
| `MYSQL_IMAGE` | No | No | Database image; compatibility default `mysql:5.7`. |
| `DB_NAME` | Yes | No | Database name; keep `sale`. |
| `DB_USER` | Yes | No | Non-root application database user. |
| `DB_PASSWORD` | Yes | Yes | Strong application database password. |
| `DB_ROOT_PASSWORD` | Yes | Yes | Strong MySQL administrative password used by lifecycle scripts inside the container. |
| `RUN_DATABASE_INITIALIZER` | No | No | Production default `false`; explicit `true` permits startup initializer. |
| `ASPNETCORE_ENVIRONMENT` | No | No | Backend environment; production default `Production`. |
| `AI_PROVIDER` | No | No | `Gemini`, `Claude`, `OpenAI`, or `OpenRouter` as supported by the API. |
| `GEMINI_API_KEY` | Provider-dependent | Yes | Gemini credential. |
| `CLAUDE_API_KEY` | Provider-dependent | Yes | Claude credential. |
| `OPENAI_API_KEY` | Provider-dependent | Yes | OpenAI credential. |
| `OPENROUTER_API_KEY` | Provider-dependent | Yes | OpenRouter credential. |
| `OPENROUTER_BASE_URL` | No | No | OpenRouter API endpoint. |
| `OPENROUTER_MODEL` | No | No | OpenRouter model identifier. |
| `OPENROUTER_TIMEOUT_SECONDS` | No | No | Provider request timeout. |
| `OPENROUTER_MAX_TOKENS` | No | No | Provider output limit. |
| `PHPMYADMIN_PORT` | No | No | Localhost-only tools port; default `8890`. |

## Security

- The backend uses the non-root `DB_USER`; MySQL root credentials are not supplied to the application.
- MySQL and the backend publish no host ports.
- Secrets, dumps, raw data, certificates, keys, logs, build output, and TempImports are ignored by Git.
- Terminate TLS in approved host/infrastructure configuration outside this repository. Forward the original scheme so secure cookies behave correctly.
- Database dumps are sensitive production data.
- Rotate and revoke the database password that was previously present in tracked development configuration. Decide with the security owner whether company policy requires Git history cleanup; this repository does not rewrite history automatically.
- MySQL 5.7 is end-of-life. Upgrade only through a separate, tested migration project.

## Cutover checklist

1. Schedule a maintenance window and confirm rollback owners.
2. Stop writes to the old system.
3. Create and secure the final logical dump.
4. Restore it into the new database volume.
5. Compare table and row counts.
6. Verify Thai text, encoding, and collation-sensitive search.
7. Verify login, customer search, edit, import, reports, and template downloads.
8. Confirm backup and monitoring procedures.
9. Enable the approved reverse proxy/DNS route.
10. Retain the final dump and rollback backup securely.

## Rollback

Keep the old server unchanged during the initial release window. If rollback is required, stop the new frontend/backend, restore traffic to the old system, and investigate before retrying. Never allow the old and new databases to accept independent writes after cutover; choose one authoritative system to avoid irreconcilable data divergence.
