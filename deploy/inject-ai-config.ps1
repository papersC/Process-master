# =============================================================
# Inject Azure OpenAI credentials into the EC2 appsettings.json
# AFTER the redeploy completes.
#
# This script:
#   1. Reads C:\inetpub\ESEMS\appsettings.json
#   2. Sets the AzureOpenAI block to the supplied values
#   3. Writes it back atomically
#   4. Recycles the IIS app pool so the new key takes effect
#
# Run AS ADMINISTRATOR on the EC2 host.
#
# NOTE: This file is NOT in git (deploy/.gitignore says *.zip plus
# this file is ignored explicitly). Keep the secret only on the host.
# =============================================================

param(
    [string]$Endpoint  = 'https://mootori-openai.openai.azure.com',
    [string]$ApiKey    = '',  # paste the secret in -ApiKey "..." at call time
    [string]$Deployment = 'gpt-4o',
    [string]$ApiVersion = '2025-01-01-preview',
    [string]$AppRoot   = 'C:\inetpub\ESEMS',
    [string]$AppPool   = 'Ejaar'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "ApiKey is required. Call as:  .\inject-ai-config.ps1 -ApiKey 'sk-...'"
}

$settingsPath = Join-Path $AppRoot 'appsettings.json'
if (-not (Test-Path $settingsPath)) { throw "$settingsPath not found." }

Write-Host "==> Reading $settingsPath"
$json = Get-Content $settingsPath -Raw | ConvertFrom-Json

if (-not ($json.PSObject.Properties.Name -contains 'AzureOpenAI')) {
    $json | Add-Member -NotePropertyName 'AzureOpenAI' -NotePropertyValue ([pscustomobject]@{}) -Force
}

$json.AzureOpenAI = [pscustomobject]@{
    Endpoint     = $Endpoint
    ApiKey       = $ApiKey
    DeploymentId = $Deployment
    ApiVersion   = $ApiVersion
    MaxTokens    = 16000
    Temperature  = 0.7
}

Write-Host "==> Writing updated appsettings.json"
$tmp = "$settingsPath.tmp"
$json | ConvertTo-Json -Depth 12 | Set-Content -Path $tmp -Encoding UTF8
Move-Item -Force -Path $tmp -Destination $settingsPath

Write-Host "==> Recycling app pool $AppPool"
Restart-WebAppPool -Name $AppPool
Start-Sleep -Seconds 8

Write-Host ''
Write-Host '=================================================================='
Write-Host ' AI credentials injected. Smoke-test by:'
Write-Host '   - Open https://ejraa360.com/App'
Write-Host '   - Look for the floating AI bubble bottom-right (only renders'
Write-Host '     when AzureOpenAI:ApiKey is configured and not a placeholder)'
Write-Host '   - Click any "AI Analysis" button on a Process/CR/Risk page'
Write-Host '=================================================================='
