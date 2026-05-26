# =============================================================
# ejraa360 — IIS app pool + sub-application + env-var setup
#
# Run on the EC2 IIS host AS ADMINISTRATOR after extracting the
# deploy ZIP to $PhysicalPath. Mirrors the rouya360 layout:
#   parent site:     ejraa360.com  (already exists in IIS)
#   sub-application: /App          (this script creates it)
#   app pool:        ejraa360      (this script creates it)
#
# Adjust the variables below if the parent site or physical path
# differ on this box.
# =============================================================

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

# ---- adjust these if needed ------------------------------------
$ParentSite   = 'ejraa360.com'                       # IIS parent website
$AppName      = 'App'                                # IIS sub-app name → URL becomes /App
$AppPoolName  = 'ejraa360'                           # dedicated app pool
$PhysicalPath = 'C:\inetpub\ejraa360\App'            # where the ZIP was extracted

# ---- env vars (set real values via IIS Manager — see runbook) -
# These are placeholder names; the actual values are entered in
# IIS Manager → Configuration Editor for security (so they're not
# baked into a script file under source control).
$RequiredEnvVars = @(
    'ASPNETCORE_ENVIRONMENT',
    'ConnectionStrings__DefaultConnection',
    'Bootstrap__RunSeeder'
)
# ---- end of adjustables ---------------------------------------

Write-Host "==> Verifying physical path: $PhysicalPath"
if (-not (Test-Path $PhysicalPath)) {
    throw "Physical path not found. Extract the deploy ZIP to $PhysicalPath first."
}
if (-not (Test-Path (Join-Path $PhysicalPath 'ESEMS.Web.exe'))) {
    throw "ESEMS.Web.exe missing in $PhysicalPath. Did the ZIP extract correctly?"
}
if (-not (Test-Path (Join-Path $PhysicalPath 'web.config'))) {
    throw "web.config missing in $PhysicalPath. The custom WebDAV-removal web.config must be present."
}

Write-Host "==> Verifying parent site exists: $ParentSite"
if (-not (Get-Website -Name $ParentSite -ErrorAction SilentlyContinue)) {
    throw "Parent IIS site '$ParentSite' not found. Create it via IIS Manager before running this script."
}

Write-Host "==> Creating/configuring app pool: $AppPoolName"
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "    Created app pool $AppPoolName"
} else {
    Write-Host "    App pool $AppPoolName already exists"
}
# .NET CLR Version = No Managed Code (required for ASP.NET Core in-process hosting)
Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ''
# Pipeline mode = Integrated
Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode -Value 'Integrated'
# Identity = ApplicationPoolIdentity (default; safe per-pool isolation)
Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
# 32-bit = false (we publish for x64 .NET 9)
Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name enable32BitAppOnWin64 -Value $false
Write-Host "    .NET CLR=No Managed Code, Integrated, AppPoolIdentity, x64"

Write-Host "==> Creating/configuring sub-application: $ParentSite/$AppName"
$existingApp = Get-WebApplication -Site $ParentSite -Name $AppName -ErrorAction SilentlyContinue
if (-not $existingApp) {
    New-WebApplication -Site $ParentSite -Name $AppName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName | Out-Null
    Write-Host "    Created sub-app /$AppName at $PhysicalPath bound to pool $AppPoolName"
} else {
    Set-ItemProperty -Path "IIS:\Sites\$ParentSite\$AppName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty -Path "IIS:\Sites\$ParentSite\$AppName" -Name applicationPool -Value $AppPoolName
    Write-Host "    Updated existing sub-app /$AppName"
}

Write-Host "==> Granting app-pool identity read access to $PhysicalPath"
$identity = "IIS AppPool\$AppPoolName"
icacls $PhysicalPath /grant ('{0}:(OI)(CI)RX' -f $identity) | Out-Null
# Writable: logs/ + wwwroot/uploads/ (for file uploads + ASP.NET Core stdout logs)
foreach ($subdir in @('logs', 'wwwroot\uploads')) {
    $p = Join-Path $PhysicalPath $subdir
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Force -Path $p | Out-Null }
    icacls $p /grant ('{0}:(OI)(CI)M' -f $identity) | Out-Null
}
Write-Host "    Read+Execute on root, Modify on logs/ and wwwroot/uploads/"

Write-Host ''
Write-Host '=================================================================='
Write-Host ' Next: set environment variables via IIS Manager (NOT this script)'
Write-Host '=================================================================='
Write-Host '  IIS Manager  ->  ejraa360.com/App  ->  Configuration Editor'
Write-Host '  Section: system.webServer/aspNetCore  ->  environmentVariables'
Write-Host ''
foreach ($ev in $RequiredEnvVars) { Write-Host "    $ev" }
Write-Host ''
Write-Host '  Click "Apply", then recycle the app pool:'
Write-Host "    Restart-WebAppPool -Name '$AppPoolName'"
Write-Host ''
Write-Host '  Smoke test:'
Write-Host "    Invoke-WebRequest 'http://localhost/App/health' -UseBasicParsing"
Write-Host '=================================================================='
