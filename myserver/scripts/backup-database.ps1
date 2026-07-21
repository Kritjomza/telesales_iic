[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$ServerDir = Split-Path -Parent $PSScriptRoot
function Fail([string]$Message) { throw "Backup failed: $Message" }

$directory = Get-Item -LiteralPath $OutputDirectory -ErrorAction SilentlyContinue
if (-not $directory -or -not $directory.PSIsContainer) { Fail "output directory does not exist: $OutputDirectory" }
if (-not (Test-Path -LiteralPath (Join-Path $ServerDir '.env'))) { Fail "$ServerDir\.env does not exist" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'Docker is not installed' }

Push-Location $ServerDir
try {
    $containerId = (& docker compose ps -q database).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $containerId) { Fail 'database container is not running' }
    $health = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' $containerId).Trim()
    if ($health -ne 'healthy') { Fail "database container is not healthy (status: $health)" }
    $outputPath = Join-Path $directory.FullName ("sale_backup_{0}.sql" -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
    if (Test-Path -LiteralPath $outputPath) { Fail "backup already exists: $outputPath" }

    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = 'docker'
    foreach ($argument in 'compose','exec','-T','database','sh','-c','MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysqldump --user=root --default-character-set=utf8mb4 --single-transaction --quick --no-tablespaces --routines --triggers --events "$MYSQL_DATABASE"') { [void]$psi.ArgumentList.Add($argument) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $process = [Diagnostics.Process]::Start($psi)
    $output = [IO.File]::Create($outputPath)
    try { $process.StandardOutput.BaseStream.CopyTo($output); $process.WaitForExit(); $exitCode = $process.ExitCode } finally { $output.Dispose(); $process.Dispose() }
    if ($exitCode -ne 0 -or (Get-Item -LiteralPath $outputPath).Length -eq 0) { Remove-Item -LiteralPath $outputPath -Force; Fail 'mysqldump failed or produced an empty file' }
    Write-Host "Database backup completed: $outputPath"
    Write-Warning 'This file contains sensitive customer data. Store it securely off-server.'
} finally { Pop-Location }
