# ESEMS.SecurityTests

Two layers:

1. **Supply-chain (xUnit)** — `dotnet list package --vulnerable --include-transitive` and `--deprecated`. Fails CI on High/Critical CVEs or abandoned packages. Run on every push.

2. **ZAP baseline (PowerShell + Docker)** — passive web crawl, finds missing security headers, weak cookies, default error pages. Run nightly against staging.

## Run

```powershell
dotnet test ESEMS.SecurityTests                      # supply-chain
pwsh ESEMS.SecurityTests\run-zap.ps1                  # ZAP (needs Docker + running app)
```
