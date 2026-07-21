[CmdletBinding()]
param([Parameter(Mandatory)][string]$DumpPath)
$ErrorActionPreference = 'Stop'
$ServerDir = Split-Path -Parent $PSScriptRoot
function Fail([string]$Message) { throw "Restore failed: $Message" }

$dump = Get-Item -LiteralPath $DumpPath -ErrorAction SilentlyContinue
if (-not $dump) { Fail "dump file does not exist: $DumpPath" }
if ($dump.Length -eq 0) { Fail "dump file is empty: $DumpPath" }
if ($dump.Extension -eq '.gz') { Fail 'decompress .sql.gz before restoring' }
if ($dump.Extension -ne '.sql') { Fail 'only plain .sql files are supported' }
if (-not (Test-Path -LiteralPath (Join-Path $ServerDir '.env'))) { Fail "$ServerDir\.env does not exist" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'Docker is not installed' }

Push-Location $ServerDir
try {
    $containerId = (& docker compose ps -q database).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $containerId) { Fail 'database container is not running' }
    $health = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' $containerId).Trim()
    if ($health -ne 'healthy') { Fail "database container is not healthy (status: $health)" }
    $count = (& docker compose exec -T database sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql --batch --skip-column-names --user=root --database="$MYSQL_DATABASE" --execute="SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();"').Trim()
    if ($LASTEXITCODE -ne 0 -or $count -notmatch '^\d+$') { Fail 'cannot inspect target database' }
    if ([int]$count -gt 0) {
        $confirmation = Read-Host "Target database contains $count tables. Type RESTORE to continue"
        if ($confirmation -cne 'RESTORE') { Fail 'restore cancelled' }
    }

    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = 'docker'
    foreach ($argument in 'compose','exec','-T','database','sh','-c','MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql --default-character-set=utf8mb4 --user=root --database="$MYSQL_DATABASE"') { [void]$psi.ArgumentList.Add($argument) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $process = [Diagnostics.Process]::Start($psi)
    $stream = [IO.File]::OpenRead($dump.FullName)
    try { $stream.CopyTo($process.StandardInput.BaseStream); $process.StandardInput.Close(); $process.WaitForExit(); $exitCode = $process.ExitCode } finally { $stream.Dispose(); $process.Dispose() }
    if ($exitCode -ne 0) { Fail 'mysql import returned a non-zero exit code' }
    Write-Host 'Database restore completed successfully. The source dump was not deleted.'
} finally { Pop-Location }
