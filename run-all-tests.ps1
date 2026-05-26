# ESEMS — full test suite runner.
#
# Runs unit + integration + security in sequence (cheap, hermetic).
# E2E gracefully skips if the dev server is not up at $BaseUrl.
# Load tests run on-demand only — pass -IncludeLoad to enable.
#
# Usage:
#   pwsh run-all-tests.ps1                     # default: cheap suites
#   pwsh run-all-tests.ps1 -IncludeLoad        # also run NBomber
#   pwsh run-all-tests.ps1 -Coverage           # also generate coverage HTML

param(
    [switch]$IncludeLoad,
    [switch]$Coverage,
    [string]$BaseUrl = "http://localhost:5297"
)

$ErrorActionPreference = "Stop"

# Use the 64-bit dotnet, not the (x86) host that ships on Windows.
if (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
    $env:Path = "C:\Program Files\dotnet;" + $env:Path
}

Write-Host "==> Build" -ForegroundColor Cyan
dotnet build "ESEMS.sln" --nologo -v:q
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

Write-Host "==> Unit + Integration (ESEMS.Tests)" -ForegroundColor Cyan
$coverageArg = if ($Coverage) { '--collect:"XPlat Code Coverage" --results-directory TestResults' } else { '' }
$testCmd = "dotnet test ESEMS.Tests\ESEMS.Tests.csproj --no-build --nologo $coverageArg"
Invoke-Expression $testCmd
if ($LASTEXITCODE -ne 0) { throw "Unit/Integration tests failed" }

Write-Host "==> Security (vulnerable + deprecated package scan)" -ForegroundColor Cyan
dotnet test "ESEMS.SecurityTests\ESEMS.SecurityTests.csproj" --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw "Security tests failed" }

Write-Host "==> E2E (Playwright)" -ForegroundColor Cyan
$env:ESEMS_E2E_BASE_URL = $BaseUrl
dotnet test "ESEMS.E2E\ESEMS.E2E.csproj" --no-build --nologo

if ($IncludeLoad) {
    Write-Host "==> Load (NBomber)" -ForegroundColor Cyan
    $env:BASE_URL = $BaseUrl
    dotnet run --project "ESEMS.LoadTests\ESEMS.LoadTests.csproj" -c Release
}

if ($Coverage) {
    Write-Host "==> Coverage report" -ForegroundColor Cyan
    if (-not (dotnet tool list -g | Select-String "reportgenerator")) {
        Write-Host "Installing dotnet-reportgenerator-globaltool ..."
        dotnet tool install --global dotnet-reportgenerator-globaltool
    }
    $latest = Get-ChildItem TestResults -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        reportgenerator -reports:$($latest.FullName) -targetdir:"TestResults\html" -reporttypes:Html
        Write-Host "Coverage HTML: TestResults\html\index.html" -ForegroundColor Green
    }
}

Write-Host "`nAll requested suites passed." -ForegroundColor Green
