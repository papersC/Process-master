# ESEMS.E2E — Playwright .NET smoke

Tests skip automatically if the dev server is not reachable on
`http://localhost:5297` (override with `ESEMS_E2E_BASE_URL`) or if Playwright
browsers were never installed.

## First-time setup

```powershell
dotnet build ESEMS.E2E
# Install all three browsers (or just chromium for fast turnaround):
pwsh ESEMS.E2E\bin\Debug\net9.0\playwright.ps1 install chromium firefox webkit
```

## Cross-browser

Set `ESEMS_E2E_BROWSER` to switch engines:

```powershell
$env:ESEMS_E2E_BROWSER = "firefox"; dotnet test ESEMS.E2E
$env:ESEMS_E2E_BROWSER = "webkit";  dotnet test ESEMS.E2E
$env:ESEMS_E2E_BROWSER = "chromium" # default
```

## Run

```powershell
# In another terminal
dotnet run --project ESEMS.Web

# Run the suite
dotnet test ESEMS.E2E
```

## Gaps to add later

- Real login → dashboard happy path (needs a seeded test user).
- Incident create → SLA badge update.
- BPMN import drag-and-drop with `autoCreate=true`.
- Excel export under ar-AE (numeric format, RTL header alignment).
