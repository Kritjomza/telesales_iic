[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$ServerDir = Split-Path -Parent $PSScriptRoot
function Fail([string]$Message) { throw "Verification failed: $Message" }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'Docker is not installed' }
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { Fail 'Docker Compose v2 is unavailable' }
$envPath = Join-Path $ServerDir '.env'
if (-not (Test-Path -LiteralPath $envPath)) { Fail "$envPath does not exist" }
$values = @{}
Get-Content -LiteralPath $envPath | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] } }
foreach ($key in 'DB_NAME','DB_USER','DB_PASSWORD','DB_ROOT_PASSWORD','FRONTEND_PORT') { if (-not $values[$key]) { Fail "$key is empty or missing in .env" } }

Push-Location $ServerDir
try {
    & docker compose config --quiet
    if ($LASTEXITCODE -ne 0) { Fail 'Compose configuration is invalid' }
    foreach ($service in 'database','backend','frontend') { $id = (& docker compose ps -q $service).Trim(); if ($LASTEXITCODE -ne 0 -or -not $id) { Fail "$service is not running" } }
    $dbId = (& docker compose ps -q database).Trim()
    if ((& docker inspect --format '{{.State.Health.Status}}' $dbId).Trim() -ne 'healthy') { Fail 'database is not healthy' }
    foreach ($path in '/api/health','/api/health/db') {
        & docker compose exec -T backend bash -c "exec 3<>/dev/tcp/127.0.0.1/8080; printf 'GET $path HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3; grep -q '200 OK' <&3"
        if ($LASTEXITCODE -ne 0) { Fail "backend $path failed" }
    }
    try { Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$($values.FRONTEND_PORT)/" -TimeoutSec 15 | Out-Null } catch { Fail 'frontend HTTP check failed' }
    & docker compose ps
    Write-Host "`nRecent backend error lines (secrets are not printed by this script):"
    & docker compose logs --no-color --since 15m backend 2>&1 | Select-String -Pattern 'fail|fatal|error|exception' | Select-Object -Last 30
    Write-Host "`nDeployment verification completed successfully."
} finally { Pop-Location }
