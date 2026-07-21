#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SERVER_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
DUMP_PATH=${1:-}

fail() { printf 'Restore failed: %s\n' "$1" >&2; exit 1; }
[ -n "$DUMP_PATH" ] || fail "usage: $0 /secure/path/sale_backup.sql"
[ -f "$DUMP_PATH" ] || fail "dump file does not exist: $DUMP_PATH"
[ -s "$DUMP_PATH" ] || fail "dump file is empty: $DUMP_PATH"
case "$DUMP_PATH" in *.sql) ;; *.sql.gz) fail "decompress .sql.gz before restoring" ;; *) fail "only plain .sql files are supported" ;; esac
[ -f "$SERVER_DIR/.env" ] || fail "$SERVER_DIR/.env does not exist"
command -v docker >/dev/null 2>&1 || fail "Docker is not installed"

cd "$SERVER_DIR"
container_id=$(docker compose ps -q database) || fail "cannot inspect database service"
[ -n "$container_id" ] || fail "database container is not running"
health=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$container_id") || fail "cannot inspect database health"
[ "$health" = healthy ] || fail "database container is not healthy (status: $health)"

table_count=$(docker compose exec -T database sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql --batch --skip-column-names --user=root --database="$MYSQL_DATABASE" --execute="SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();"') || fail "cannot inspect target database"
case "$table_count" in ''|*[!0-9]*) fail "unexpected table count: $table_count" ;; esac
if [ "$table_count" -gt 0 ]; then
  printf 'Target database already contains %s tables. Type RESTORE to continue: ' "$table_count"
  read -r confirmation
  [ "$confirmation" = RESTORE ] || fail "restore cancelled"
fi

printf 'Restoring %s into the configured database...\n' "$DUMP_PATH"
if docker compose exec -T database sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql --default-character-set=utf8mb4 --user=root --database="$MYSQL_DATABASE"' < "$DUMP_PATH"; then
  printf 'Database restore completed successfully. The source dump was not deleted.\n'
else
  fail "mysql import returned a non-zero exit code"
fi
