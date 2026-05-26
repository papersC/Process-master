# =============================================================
#  ejraa360 deploy — release af364f4  (RUN ON THE EC2 IIS HOST, AS ADMIN)
# -------------------------------------------------------------
#  This release carries a DESTRUCTIVE migration (org-unit merge), so unlike the
#  plain ejaar-redeploy.ps1 this script ALSO:
#    1. backs up the database BEFORE anything,
#    2. applies the reviewed idempotent migration SQL with the app stopped
#       (so a half-migration can't serve traffic),
#  then does the normal folder swap (preserving tenant config) + smoke test.
#
#  PREREQS — RDP these two files onto the box first (e.g. into C:\Temp\):
#    - ejraa360-App.zip                       (the published app)
#    - ejraa360-migrate-to-af364f4.sql        (the idempotent migration script)
#
#  Fill in the ---- SQL connection ---- block for your prod DB, then run:
#    powershell -ExecutionPolicy Bypass -File .\ejraa360-deploy-af364f4.ps1
# =============================================================

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

# ---- adjustables (host paths) ---------------------------------
$ZipPath    = 'C:\Temp\ejraa360-App.zip'
$MigrateSql = 'C:\Temp\ejraa360-migrate-to-af364f4.sql'
$AppPool    = 'Ejaar'
$AppRoot    = 'C:\inetpub\ESEMS'
$HealthUrl  = 'https://ejraa360.com/App/health'

# ---- SQL connection (EDIT THESE) ------------------------------
$SqlInstance = 'localhost'        # or '.\SQLEXPRESS' / 'localhost,1433' / the prod instance
$SqlDb       = 'ESEMS'
$SqlUser     = ''                 # leave '' for Windows/trusted auth (-E); else a db_ddladmin login
$SqlPwd      = ''
$BackupPath  = "D:\backups\ESEMS_pre_af364f4_$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
# ---- end adjustables ------------------------------------------

$PreserveFiles = @('appsettings.json', 'appsettings.Production.json')
$PreserveDirs  = @('logs', 'wwwroot\uploads')
$BackupRoot    = "C:\Temp\ejraa360-redeploy-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

# sqlcmd auth args: -E (trusted) when no SQL user given, else -U/-P
$AuthArgs = if ([string]::IsNullOrEmpty($SqlUser)) { @('-E') } else { @('-U', $SqlUser, '-P', $SqlPwd) }
function Invoke-Sql([string]$query)   { & sqlcmd -S $SqlInstance -d $SqlDb @AuthArgs -b -h -1 -W -Q $query }
function Invoke-SqlMaster([string]$q) { & sqlcmd -S $SqlInstance -d master @AuthArgs -b -Q $q }
function Invoke-SqlFile([string]$file){ & sqlcmd -S $SqlInstance -d $SqlDb @AuthArgs -b -i $file }

# ---- 0. prereqs ----------------------------------------------
if (-not (Test-Path $ZipPath))    { throw "ZIP not found: $ZipPath — RDP it into C:\Temp\ first." }
if (-not (Test-Path $MigrateSql)) { throw "Migration SQL not found: $MigrateSql — RDP it into C:\Temp\ first." }
if (-not (Test-Path $AppRoot))    { throw "App root missing: $AppRoot" }
$bkDir = Split-Path $BackupPath -Parent
if (-not (Test-Path $bkDir)) { New-Item -ItemType Directory -Force -Path $bkDir | Out-Null }

Write-Host "==> Current DB migration state:"
Invoke-Sql "SET NOCOUNT ON; SELECT TOP 5 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;"

# ---- 1. stop the app (no traffic during backup + migration) ---
Write-Host "==> Stopping app pool $AppPool"
Stop-WebAppPool -Name $AppPool
Start-Sleep -Seconds 5

# ---- 2. BACK UP THE DATABASE (mandatory) ----------------------
Write-Host "==> Backing up [$SqlDb] -> $BackupPath"
Invoke-SqlMaster "BACKUP DATABASE [$SqlDb] TO DISK = N'$BackupPath' WITH INIT, COMPRESSION, STATS = 10;"
if (-not (Test-Path $BackupPath)) { throw "Backup file was not created — ABORTING before any change." }
Write-Host "    backup OK ($([math]::Round((Get-Item $BackupPath).Length/1MB)) MB)"

# ---- 3. apply migrations (idempotent — only what's missing) ---
#  Includes the destructive org-unit merge if prod doesn't have it yet.
Write-Host "==> Applying migration script $MigrateSql"
Invoke-SqlFile $MigrateSql
Write-Host "==> Post-migration state:"
Invoke-Sql "SET NOCOUNT ON; SELECT TOP 3 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;"

# ---- 4. swap the app folder (preserve tenant config) ----------
Write-Host "==> Preserving tenant config -> $BackupRoot"
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
foreach ($f in $PreserveFiles) {
    $src = Join-Path $AppRoot $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $BackupRoot $f) -Force; Write-Host "    preserved $f" }
}
foreach ($d in $PreserveDirs) {
    $src = Join-Path $AppRoot $d
    if (Test-Path $src) {
        $dst = Join-Path $BackupRoot $d; New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item "$src\*" $dst -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "    preserved $d\"
    }
}
Write-Host "==> Clearing $AppRoot and extracting ZIP"
Get-ChildItem -Path $AppRoot -Force | Remove-Item -Recurse -Force
Expand-Archive -Path $ZipPath -DestinationPath $AppRoot -Force
Write-Host "==> Restoring tenant config"
foreach ($f in $PreserveFiles) {
    $src = Join-Path $BackupRoot $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $AppRoot $f) -Force; Write-Host "    restored $f" }
}
foreach ($d in $PreserveDirs) {
    $src = Join-Path $BackupRoot $d
    if (Test-Path $src) {
        $dst = Join-Path $AppRoot $d; New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item "$src\*" $dst -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "    restored $d\"
    }
}

# ---- 5. start + smoke ----------------------------------------
Write-Host "==> Starting app pool $AppPool"
Start-WebAppPool -Name $AppPool
Start-Sleep -Seconds 8
Write-Host "==> Smoke test $HealthUrl"
$r = Invoke-WebRequest $HealthUrl -UseBasicParsing -TimeoutSec 30
Write-Host "    Status: $($r.StatusCode)  Body: $($r.Content)"
if ($r.StatusCode -ne 200) { throw "Health returned $($r.StatusCode)" }

Write-Host ''
Write-Host '=================================================================='
Write-Host ' Deploy OK. DB backup:  ' $BackupPath
Write-Host ' App config backup:     ' $BackupRoot
Write-Host ''
Write-Host ' NEXT (only if the org-unit merge just ran for the first time):'
Write-Host '   Log in as Admin -> Settings Hub -> Data Import -> Legacy imports'
Write-Host '   -> import "Org Structure" FIRST, then Assets, so org links repopulate.'
Write-Host ''
Write-Host ' ROLLBACK (if the app is broken):'
Write-Host "   Stop-WebAppPool '$AppPool'"
Write-Host "   sqlcmd -S $SqlInstance -d master $($AuthArgs -join ' ') -Q ""ALTER DATABASE [$SqlDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$SqlDb] FROM DISK=N'$BackupPath' WITH REPLACE; ALTER DATABASE [$SqlDb] SET MULTI_USER;"""
Write-Host "   Get-ChildItem '$AppRoot' -Force | Remove-Item -Recurse -Force"
Write-Host "   Copy-Item '$BackupRoot\*' '$AppRoot\' -Recurse -Force"
Write-Host "   Start-WebAppPool '$AppPool'"
Write-Host '=================================================================='
