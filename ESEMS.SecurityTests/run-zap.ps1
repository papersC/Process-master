# OWASP ZAP baseline scan against a running ESEMS instance.
#
# Requirements:
#   - Docker Desktop running
#   - ESEMS.Web running on $BaseUrl (default http://host.docker.internal:5297)

param(
    [string]$BaseUrl = "http://host.docker.internal:5297",
    [string]$ReportPath = (Join-Path $PSScriptRoot "zap-baseline.html")
)

$ErrorActionPreference = "Stop"

Write-Host "Running OWASP ZAP baseline scan against $BaseUrl ..."

$reportDir = Split-Path $ReportPath -Parent
if (-not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
}

docker run --rm `
    -v "${reportDir}:/zap/wrk" `
    zaproxy/zap-stable `
    zap-baseline.py `
    -t $BaseUrl `
    -r (Split-Path $ReportPath -Leaf) `
    -I

if (Test-Path $ReportPath) {
    Write-Host "ZAP report written to: $ReportPath"
} else {
    Write-Warning "ZAP report not produced — check that Docker is running and ESEMS is reachable."
}
