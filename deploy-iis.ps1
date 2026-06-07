<#
  ESEMS — IIS deployment / configuration script
  Run ON the EC2 IIS host, in an ELEVATED PowerShell window, AFTER you have
  extracted ESEMS-publish.zip into $PhysicalPath.

  It is idempotent — safe to re-run (e.g. after changing a value below).

  What it does:
    1. Verifies the ASP.NET Core Hosting Bundle is installed.
    2. Creates / configures a dedicated app pool (No Managed Code, Integrated).
    3. Sets ALL environment variables on the app pool (live in
       applicationHost.config -> survive file redeploys, never in the artifact).
    4. Creates / updates the /App sub-application under your site.
    5. Creates the logs folder and grants the pool identity write access to
       logs + wwwroot/uploads.
    6. Recycles the app pool.
#>

#Requires -RunAsAdministrator

# ======================= EDIT THESE =======================
$SiteName      = "Default Web Site"                 # <-- parent IIS site (e.g. esems.mbrhe.gov.ae)
$AppAlias      = "App"                              # keep "App" — must match PathBase /App
$PhysicalPath  = "C:\inetpub\esems\App"            # <-- folder where you extracted the zip
$AppPoolName   = "ESEMS"

$SqlServer     = "MBRHE-ERM-Prod"                  # SQL Server host\instance
$SqlDatabase   = "ESEMS"
$SqlUser       = "sa"                              # or a dedicated 'esems_app' login (recommended)
$SqlPassword   = "REPLACE_WITH_DB_PASSWORD"        # <-- real SQL password
$AdminPassword = "REPLACE_WITH_STRONG_ADMIN_PW"    # <-- first-boot 'admin' password (NOT 'Admin123')
# ==========================================================

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

Write-Host "== ESEMS IIS setup ==" -ForegroundColor Cyan

# --- guard: refuse obvious placeholder values ---
if ($SqlPassword -eq "REPLACE_WITH_DB_PASSWORD" -or $AdminPassword -eq "REPLACE_WITH_STRONG_ADMIN_PW") {
    throw "Edit the SqlPassword and AdminPassword values at the top of this script first."
}
if ($AdminPassword -eq "Admin123" -or [string]::IsNullOrWhiteSpace($AdminPassword)) {
    throw "AdminPassword must be a strong password (the app rejects blank / 'Admin123' in Production)."
}

# --- guard: the artifact must already be extracted ---
if (-not (Test-Path (Join-Path $PhysicalPath "ESEMS.Web.dll"))) {
    throw "ESEMS.Web.dll not found in $PhysicalPath. Extract ESEMS-publish.zip there first."
}

# --- 1. Hosting Bundle / ANCM present? ---
if (-not (Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue)) {
    Write-Warning "AspNetCoreModuleV2 not found. Install the ASP.NET Core 9 Hosting Bundle, do 'iisreset', then re-run."
    throw "Hosting Bundle missing."
}

# --- 2. App pool: dedicated, No Managed Code, Integrated ---
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "  + created app pool '$AppPoolName'"
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""           # No Managed Code
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode  -Value "Integrated"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode            -Value "AlwaysRunning"
Write-Host "  + app pool configured (No Managed Code, Integrated)"

# --- 3. Environment variables on the app pool ---
$connString = "Data Source=$SqlServer;Initial Catalog=$SqlDatabase;User ID=$SqlUser;Password=$SqlPassword;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;Pooling=true;Max Pool Size=100;MultipleActiveResultSets=true;"

$keysDir = Join-Path $PhysicalPath "keys"   # data-protection key ring; not in the zip, so it persists across overwrite-deploys (like logs/uploads)
$envMap = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"               = "Production"   # engages HTTPS-only cookies, HSTS, admin guard
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
Write-Host "  + set $($envMap.Count) environment variables on '$AppPoolName'"

# --- 4. Sub-application under the site ---
$existing = Get-WebApplication -Site $SiteName -Name $AppAlias -ErrorAction SilentlyContinue
if (-not $existing) {
    New-WebApplication -Site $SiteName -Name $AppAlias -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName | Out-Null
    Write-Host "  + created application '$SiteName/$AppAlias' -> $PhysicalPath"
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName\$AppAlias" -Name physicalPath    -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName\$AppAlias" -Name applicationPool -Value $AppPoolName
    Write-Host "  + updated existing application '$SiteName/$AppAlias'"
}

# --- 5. logs folder + write permissions for the pool identity ---
$logsDir = Join-Path $PhysicalPath "logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }
$poolIdentity = "IIS AppPool\$AppPoolName"
icacls $logsDir /grant "${poolIdentity}:(OI)(CI)M" | Out-Null
$uploadsDir = Join-Path $PhysicalPath "wwwroot\uploads"
if (Test-Path $uploadsDir) { icacls $uploadsDir /grant "${poolIdentity}:(OI)(CI)M" | Out-Null }
Write-Host "  + logs + uploads writable by '$poolIdentity'"

# Data-protection key ring. The overwrite-deploy never ships a 'keys' folder, so
# (like logs/ and wwwroot/uploads/) it persists across redeploys. Must be
# writable by the pool identity, or auth cookies + antiforgery tokens get
# regenerated on every recycle (users bounced to login each deploy).
if (-not (Test-Path $keysDir)) { New-Item -ItemType Directory -Path $keysDir | Out-Null }
icacls $keysDir /grant "${poolIdentity}:(OI)(CI)M" | Out-Null
Write-Host "  + data-protection key ring at '$keysDir' (writable by '$poolIdentity')"

# --- 6. Recycle ---
Restart-WebAppPool -Name $AppPoolName
Write-Host "`nDone." -ForegroundColor Green
Write-Host "Browse:  https://<your-host>/$AppAlias   (health probe: /$AppAlias/health -> 'ok')"
Write-Host "After first login: change the admin password in the UI, then set Bootstrap__RunSeeder=false:"
Write-Host "  Set-WebConfigurationProperty -pspath '$apphost' -filter `"$filter/add[@name='Bootstrap__RunSeeder']`" -name value -value false"
Write-Host "  Restart-WebAppPool -Name $AppPoolName"
