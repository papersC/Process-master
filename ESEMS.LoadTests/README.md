# ESEMS.LoadTests — NBomber

One scenario, two anonymous reads: `/health` + `/Account/Login`. No seeded user required. Reports land in `./reports/`.

## Run

```powershell
# Start the app in another terminal
dotnet run --project ESEMS.Web -c Release

# Default: 50 ramped users over 2 minutes — anonymous (/health + /Account/Login)
dotnet run --project ESEMS.LoadTests -c Release

# Authenticated read-mix — Dashboard / Improvements / EnterpriseRisks / Categories
$env:AUTHED = "1"
dotnet run --project ESEMS.LoadTests -c Release

# Custom volume
$env:BASE_URL = "http://localhost:5297"
$env:RAMP_USERS = "100"
$env:DURATION_MIN = "5"
$env:AUTHED = "1"
dotnet run --project ESEMS.LoadTests -c Release
```

The authed scenario uses the seeded dev test user `viewer / Viewer123`
(Program.cs:1690). Each virtual user runs its own cookie jar — login,
harvest antiforgery token, POST creds, then read-mix.

## Gaps to add later

- Spike test — sudden 5× burst to test rate-limiter behavior.
- Sustained 8h for memory leaks.
- Write-mix (Improvement create with CSRF dance) — risks polluting dev DB.
