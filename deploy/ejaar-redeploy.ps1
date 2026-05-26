# =============================================================
# ejaar / ejraa360 redeploy
# -------------------------------------------------------------
# Run on the EC2 IIS host AS ADMINISTRATOR.
# Prereq: drop ejraa360-App.zip into C:\Temp\ via RDP first.
#
# Backs up tenant-specific files that must survive every redeploy:
#   - appsettings.json                (SQL connection string)
#   - appsettings.Production.json     (AllowedHosts override)
#   - wwwroot\uploads\                (user-uploaded documents)
#   - logs\                           (stdout history)
# Then clears C:\inetpub\ESEMS, extracts the ZIP, and restores
# the preserved files. Finishes with a smoke test against
# https://ejraa360.com/App/health (NOT localhost — that falls
# through to Default Web Site).
# =============================================================

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

# ---- adjust if the host paths ever change ---------------------
$ZipPath       = 'C:\Temp\ejraa360-App.zip'
$AppPool       = 'Ejaar'
$AppRoot       = 'C:\inetpub\ESEMS'
$HealthUrl     = 'https://ejraa360.com/App/health'
$BackupRoot    = "C:\Temp\ejraa360-redeploy-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$PreserveFiles = @('appsettings.json', 'appsettings.Production.json')
$PreserveDirs  = @('logs', 'wwwroot\uploads')
# ---- end of adjustables ---------------------------------------

if (-not (Test-Path $ZipPath))  { throw "ZIP not found at $ZipPath. Drag the file from your laptop into C:\Temp\ first." }
if (-not (Test-Path $AppRoot))  { throw "App root $AppRoot missing. Run 02-iis-setup.ps1 first." }

Write-Host "==> Stopping app pool $AppPool"
Stop-WebAppPool -Name $AppPool
Start-Sleep -Seconds 5

Write-Host "==> Backing up tenant config + writable dirs to $BackupRoot"
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
foreach ($f in $PreserveFiles) {
    $src = Join-Path $AppRoot $f
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $BackupRoot $f) -Force
        Write-Host "    preserved $f"
    } else {
        Write-Host "    (no $f to preserve)"
    }
}
foreach ($d in $PreserveDirs) {
    $src = Join-Path $AppRoot $d
    if (Test-Path $src) {
        $dst = Join-Path $BackupRoot $d
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item -Path "$src\*" -Destination $dst -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "    preserved $d\"
    }
}

Write-Host "==> Clearing $AppRoot"
Get-ChildItem -Path $AppRoot -Force | Remove-Item -Recurse -Force

Write-Host "==> Extracting ZIP to $AppRoot"
Expand-Archive -Path $ZipPath -DestinationPath $AppRoot -Force

Write-Host "==> Restoring tenant config + writable dirs"
foreach ($f in $PreserveFiles) {
    $src = Join-Path $BackupRoot $f
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $AppRoot $f) -Force
        Write-Host "    restored $f"
    }
}
foreach ($d in $PreserveDirs) {
    $src = Join-Path $BackupRoot $d
    if (Test-Path $src) {
        $dst = Join-Path $AppRoot $d
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item -Path "$src\*" -Destination $dst -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "    restored $d\"
    }
}

Write-Host "==> Starting app pool $AppPool"
Start-WebAppPool -Name $AppPool
Start-Sleep -Seconds 8

Write-Host "==> Smoke test $HealthUrl"
try {
    $r = Invoke-WebRequest $HealthUrl -UseBasicParsing -TimeoutSec 30
    Write-Host "    Status: $($r.StatusCode)  Body: $($r.Content)"
    if ($r.StatusCode -ne 200) { throw "Health endpoint returned $($r.StatusCode)" }
    Write-Host ''
    Write-Host '=================================================================='
    Write-Host ' Redeploy successful. Backup retained at:'
    Write-Host "   $BackupRoot"
    Write-Host ' (delete it after verifying the app works, to free disk.)'
    Write-Host '=================================================================='
} catch {
    Write-Host ''
    Write-Host "    SMOKE TEST FAILED: $_"
    Write-Host ''
    Write-Host '    Rollback steps:'
    Write-Host "       Stop-WebAppPool '$AppPool'"
    Write-Host "       Get-ChildItem '$AppRoot' -Force | Remove-Item -Recurse -Force"
    Write-Host "       Copy-Item '$BackupRoot\*' '$AppRoot\' -Recurse -Force"
    Write-Host "       Start-WebAppPool '$AppPool'"
    Write-Host ''
    Write-Host "    Or check stdout logs in $AppRoot\logs\ for the actual exception."
    throw
}
