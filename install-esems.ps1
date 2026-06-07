<#
  ESEMS - one-shot IIS installer.  RUN ON THE EC2 IIS HOST, in an ELEVATED
  PowerShell window.  Put ESEMS-publish.zip in the SAME folder as this script
  (or pass -ZipPath).

  Does everything: extracts the app, creates/updates the app pool, sets all
  environment variables (in applicationHost.config - they survive future
  redeploys and never sit in a file), wires the /App sub-application, fixes
  folder permissions, and starts the app.

  Idempotent - re-run it to UPGRADE: it stops the app, overwrites the binaries,
  keeps wwwroot\uploads + logs, and restarts.
#>

#Requires -RunAsAdministrator

param(
    [string]$ZipPath = (Join-Path $PSScriptRoot 'ESEMS-publish.zip')
)

# ========================= EDIT THESE =========================
$SiteName      = "Default Web Site"                 # parent IIS site (e.g. esems.mbrhe.gov.ae)
$AppAlias      = "App"                              # keep "App" - must match PathBase /App
$PhysicalPath  = "C:\inetpub\esems\App"            # where the app files will live
$AppPoolName   = "ESEMS"

$SqlServer     = "MBRHE-ERM-Prod"                  # SQL Server host\instance
$SqlDatabase   = "ESEMS"
$SqlUser       = "sa"                              # or a dedicated 'esems_app' login (recommended)
$SqlPassword   = "REPLACE_WITH_DB_PASSWORD"        # real SQL password
$AdminPassword = "REPLACE_WITH_STRONG_ADMIN_PW"    # first-boot 'admin' password (NOT 'Admin123')
# =============================================================

$ErrorActionPreference = "Stop"
Import-Module WebAdministration
function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }

Write-Host "===== ESEMS installer =====" -ForegroundColor Green

# ---- guards -------------------------------------------------
if ($SqlPassword -eq "REPLACE_WITH_DB_PASSWORD" -or $AdminPassword -eq "REPLACE_WITH_STRONG_ADMIN_PW") {
    throw "Edit SqlPassword and AdminPassword at the top of this script first."
}
if ($AdminPassword -eq "Admin123" -or [string]::IsNullOrWhiteSpace($AdminPassword)) {
    throw "AdminPassword must be strong (the app rejects blank / 'Admin123' in Production)."
}
if (-not (Test-Path $ZipPath)) {
    throw "Zip not found: $ZipPath  (copy ESEMS-publish.zip next to this script, or pass -ZipPath)."
}
if (-not (Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue)) {
    throw "AspNetCoreModuleV2 missing. Install the ASP.NET Core 9 Hosting Bundle, run 'iisreset', then re-run."
}

# ---- 1. stop the app if it already exists (release dll locks) ----
if (Test-Path "IIS:\AppPools\$AppPoolName") {
    if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped') {
        Write-Step "Stopping app pool '$AppPoolName' for upgrade"
        Stop-WebAppPool -Name $AppPoolName
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped' -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
        }
        Start-Sleep -Seconds 2   # let w3wp exit and release file handles
    }
}

# ---- 2. extract (overwrite app files, keep uploads + logs) ----
Write-Step "Extracting to $PhysicalPath"
if (-not (Test-Path $PhysicalPath)) { New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    foreach ($entry in $archive.Entries) {
        $target = Join-Path $PhysicalPath $entry.FullName
        if ([string]::IsNullOrEmpty($entry.Name)) {                       # directory entry
            if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }
            continue
        }
        $dir = Split-Path $target -Parent
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)   # $true = overwrite
    }
} finally { $archive.Dispose() }

if (-not (Test-Path (Join-Path $PhysicalPath 'ESEMS.Web.dll'))) {
    throw "ESEMS.Web.dll not present after extract - wrong zip?"
}

# ---- 3. app pool: dedicated, No Managed Code, Integrated ----
Write-Step "Configuring app pool '$AppPoolName'"
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) { New-WebAppPool -Name $AppPoolName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""           # No Managed Code
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode  -Value "Integrated"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode            -Value "AlwaysRunning"

# ---- 4. environment variables on the pool ----
Write-Step "Setting environment variables"
$connString = "Data Source=$SqlServer;Initial Catalog=$SqlDatabase;User ID=$SqlUser;Password=$SqlPassword;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;Pooling=true;Max Pool Size=100;MultipleActiveResultSets=true;"
$keysDir = Join-Path $PhysicalPath "keys"   # data-protection key ring; not in the zip, so it persists across overwrite-deploys (like logs/uploads)
$envMap = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"               = "Production"   # HTTPS-only cookies, HSTS, admin-password guard
    "ConnectionStrings__DefaultConnection" = $connString    # overrides the CHANGE_ME placeholder
    "SeedAdmin__Password"                  = $AdminPassword # consumed on first boot only
    "Bootstrap__RunSeeder"                 = "true"         # flip to false after first successful boot
    "DataProtection__KeyRingPath"          = $keysDir       # auth cookies + antiforgery keys survive redeploys
}
$apphost = "MACHINE/WEBROOT/APPHOST"
$filter  = "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables"
Clear-WebConfiguration -pspath $apphost -filter $filter -ErrorAction SilentlyContinue
foreach ($kv in $envMap.GetEnumerator()) {
    Add-WebConfigurationProperty -pspath $apphost -filter $filter -name "." -value @{ name = $kv.Key; value = $kv.Value }
}

# ---- 5. /App sub-application ----
Write-Step "Wiring '/$AppAlias' under '$SiteName'"
$existing = Get-WebApplication -Site $SiteName -Name $AppAlias -ErrorAction SilentlyContinue
if (-not $existing) {
    New-WebApplication -Site $SiteName -Name $AppAlias -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName\$AppAlias" -Name physicalPath    -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName\$AppAlias" -Name applicationPool -Value $AppPoolName
}

# ---- 6. permissions: pool identity needs write on logs + uploads ----
Write-Step "Granting write on logs + uploads"
$logsDir = Join-Path $PhysicalPath "logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }
$poolId = "IIS AppPool\$AppPoolName"
icacls $logsDir /grant "${poolId}:(OI)(CI)M" | Out-Null
$uploadsDir = Join-Path $PhysicalPath "wwwroot\uploads"
if (Test-Path $uploadsDir) { icacls $uploadsDir /grant "${poolId}:(OI)(CI)M" | Out-Null }

# Data-protection key ring. Not shipped in the zip, so (like logs/ + uploads/) it
# survives the overwrite-extract above. Without a writable, persistent key ring,
# IIS regenerates the keys on every recycle, invalidating all auth cookies
# (users bounced to login each deploy).
if (-not (Test-Path $keysDir)) { New-Item -ItemType Directory -Path $keysDir | Out-Null }
icacls $keysDir /grant "${poolId}:(OI)(CI)M" | Out-Null

# ---- 7. start ----
Write-Step "Starting app pool"
Start-WebAppPool -Name $AppPoolName

Write-Host "`nInstall complete." -ForegroundColor Green
Write-Host "Browse:   https://<your-host>/$AppAlias      (health probe: /$AppAlias/health -> 'ok')"
Write-Host "First boot runs DB migrations + seeding - give it up to a minute before the first page loads."
Write-Host ""
Write-Host "AFTER first login (change the admin password in the UI), disable the seeder:" -ForegroundColor Yellow
Write-Host "  Set-WebConfigurationProperty -pspath '$apphost' -filter `"$filter/add[@name='Bootstrap__RunSeeder']`" -name value -value false"
Write-Host "  Restart-WebAppPool -Name $AppPoolName"
