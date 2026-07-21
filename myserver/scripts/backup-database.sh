#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SERVER_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
OUTPUT_DIR=${1:-}

fail() { printf 'Backup failed: %s\n' "$1" >&2; exit 1; }
[ -n "$OUTPUT_DIR" ] || fail "usage: $0 /secure/backup/directory"
[ -d "$OUTPUT_DIR" ] || fail "output directory does not exist: $OUTPUT_DIR"
[ -f "$SERVER_DIR/.env" ] || fail "$SERVER_DIR/.env does not exist"
command -v docker >/dev/null 2>&1 || fail "Docker is not installed"

cd "$SERVER_DIR"
container_id=$(docker compose ps -q database) || fail "cannot inspect database service"
[ -n "$container_id" ] || fail "database container is not running"
health=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container_id") || fail "cannot inspect database health"
[ "$health" = healthy ] || fail "database container is not healthy (status: $health)"

timestamp=$(date -u +%Y%m%dT%H%M%SZ)
output_path="$OUTPUT_DIR/sale_backup_$timestamp.sql"
[ ! -e "$output_path" ] || fail "backup already exists: $output_path"

printf 'Creating sensitive database backup at %s\n' "$output_path"
if docker compose exec -T database sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysqldump --user=root --default-character-set=utf8mb4 --single-transaction --quick --no-tablespaces --routines --triggers --events "$MYSQL_DATABASE"' > "$output_path"; then
  if [ -s "$output_path" ]; then
    printf 'Database backup completed successfully. Store it securely off-server.\n'
  else
    rm -f -- "$output_path"
    fail "mysqldump produced an empty file"
  fi
else
  rm -f -- "$output_path"
  fail "mysqldump returned a non-zero exit code"
fi
