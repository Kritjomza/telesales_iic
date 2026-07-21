#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SERVER_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
fail() { printf 'Verification failed: %s\n' "$1" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || fail "Docker is not installed"
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is unavailable"
[ -f "$SERVER_DIR/.env" ] || fail "$SERVER_DIR/.env does not exist"

required='DB_NAME DB_USER DB_PASSWORD DB_ROOT_PASSWORD FRONTEND_PORT'
for key in $required; do
  value=$(sed -n "s/^${key}=//p" "$SERVER_DIR/.env" | tail -n 1)
  [ -n "$value" ] || fail "$key is empty or missing in .env"
done

cd "$SERVER_DIR"
docker compose config --quiet || fail "Compose configuration is invalid"

for service in database backend frontend; do
  id=$(docker compose ps -q "$service") || fail "cannot inspect $service"
  [ -n "$id" ] || fail "$service is not running"
done

db_id=$(docker compose ps -q database)
[ "$(docker inspect --format '{{.State.Health.Status}}' "$db_id")" = healthy ] || fail "database is not healthy"
docker compose exec -T backend bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /api/health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; grep -q "200 OK" <&3' || fail "backend /api/health failed"
docker compose exec -T backend bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /api/health/db HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; grep -q "200 OK" <&3' || fail "backend /api/health/db failed"

frontend_port=$(sed -n 's/^FRONTEND_PORT=//p' .env | tail -n 1)
if command -v curl >/dev/null 2>&1; then
  curl --fail --silent --show-error "http://127.0.0.1:${frontend_port}/" >/dev/null || fail "frontend HTTP check failed"
else
  docker compose exec -T frontend wget -q --spider http://127.0.0.1/ || fail "frontend HTTP check failed"
fi

docker compose ps
printf '\nRecent backend error lines (secrets are not printed by this script):\n'
docker compose logs --no-color --since 15m backend 2>&1 | grep -Ei 'fail|fatal|error|exception' | tail -n 30 || true
printf '\nDeployment verification completed successfully.\n'
