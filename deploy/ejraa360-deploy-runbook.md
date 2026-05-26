# ejraa360 Deploy Runbook — release `af364f4` (2026-05-24)

Safe deploy of the current `master` (`af364f4`) to **ejraa360.com/App**.
This release is **NOT a routine ZIP push** — it carries a pending migration
chain that includes the **destructive org-unit merge**. Follow this in order.

## What this release contains
- 27 bigTest audit fixes + F-013 (self-service password change) + F-023 (import-order)
  + FU-001…FU-005 (Workload IDOR, password rate-limit/length/confirm, session
  invalidation, notify-on-change).
- **Migrations pending vs the 2026-05-01 prod baseline** (the deploy will apply
  whichever of these prod is missing):
  - several additive ones (May 18–21),
  - `20260523072618_AddImportBatch` — additive,
  - **`20260523100616_MergeOrganizationUnitsToIntKey` — DESTRUCTIVE**: drops the
    business `OrganizationUnits` (GUID) table, re-keys 24 FKs to int, points
    everything at `organization_units`. **Org links are reset → the client's org
    structure must be re-imported afterward.**
  - `20260523194017_AddUserSecurityStamp` — additive (nullable `[user].security_stamp`).
- Tests: 333 unit + 51 E2E + 23 security, all green. Release build clean.

## Target (from prior deploys)
- EC2 Windows / IIS 10, host `ejraa360.com`, IIS site **`Ejaar`**, sub-app **`/App`**
  at `C:\inetpub\ESEMS`, app pool **`Ejaar`** (No Managed Code).
- App runs `Database.Migrate()` on startup, so it WILL auto-apply pending
  migrations when it restarts — which is exactly why the destructive one must be
  handled deliberately (below), not left to a surprise restart.

---

## STEP 0 — Decide the path (2-minute check)
On the prod DB, run:
```sql
SELECT TOP 5 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
```
- **If the top row is `20260523100616_MergeOrganizationUnitsToIntKey` or newer**
  → the destructive merge is already applied. This becomes a **routine deploy** —
  skip the org re-import (Step 5) and just let the additive `AddUserSecurityStamp`
  apply. Go Step 1 → 2 → 4 → 6.
- **If it's older** (e.g. anything ≤ `20260521…`) → the destructive merge is
  PENDING. Do the **full** runbook including the controlled migration (Step 3)
  and the org re-import (Step 5).

---

## STEP 1 — Pre-flight (local — already verified, re-confirm)
- `git -C <repo> rev-parse HEAD` == `af364f4`, `git status` clean.
- `dotnet build ESEMS.Web -c Release` → 0 errors. Tests green.
- Build the publish artifact:
  ```powershell
  dotnet publish ESEMS.Web/ESEMS.Web.csproj -c Release -o publish/ejraa360
  ```
- **Do NOT ship a dev appsettings** — the box's `appsettings.json` (real
  connection string, JWT key, AllowedHosts=`ejraa360.com`, `AppSettings:PathBase=/App`)
  must be PRESERVED, not overwritten (see Step 4).

## STEP 2 — Backup (prod — MANDATORY, especially for the destructive merge)
1. **Full DB backup**:
   ```sql
   BACKUP DATABASE [ESEMS] TO DISK = N'D:\backups\ESEMS_pre_af364f4_2026-05-24.bak' WITH INIT, COMPRESSION;
   ```
2. **App folder backup**: copy `C:\inetpub\ESEMS` → `C:\inetpub\_backup\ESEMS_pre_af364f4`.
3. Note the current `__EFMigrationsHistory` top row (your rollback target).

## STEP 3 — Apply migrations in a CONTROLLED way (destructive path only)
Do NOT rely on the app's startup auto-migrate for the destructive merge — apply
the reviewed SQL yourself, with the app **stopped**, so a mid-chain failure can't
leave a half-migrated DB serving traffic.
1. Use the generated **idempotent** script: `deploy/ejraa360-migrate-to-af364f4.sql`
   (in this repo). It guards every migration on `__EFMigrationsHistory`, so it
   applies only what's missing — safe regardless of prod's exact position.
2. **Review the org-merge section** (search the file for
   `MergeOrganizationUnitsToIntKey`) — confirm the table/FK names match prod
   (the migration uses dynamic introspection SQL for live-DB drift, but eyeball it).
3. Stop the app pool: `Stop-WebAppPool -Name Ejaar`.
4. Run the script against prod with a login that has `db_ddladmin` (DDL rights):
   ```powershell
   sqlcmd -S <prod-sql-instance> -d ESEMS -U <ddl_login> -P <pwd> -i deploy\ejraa360-migrate-to-af364f4.sql -b
   ```
   `-b` aborts on the first error. If it errors → STOP, restore the .bak, investigate.
5. Verify: `SELECT TOP 3 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;`
   → top row is now `20260523194017_AddUserSecurityStamp`.

*(Routine path: skip Step 3 — let the app auto-apply the single additive
`AddUserSecurityStamp` on restart in Step 4, OR run the script anyway; both safe.)*

## STEP 4 — Deploy the app (ZIP/folder swap)
1. App pool stopped (from Step 3, or `Stop-WebAppPool -Name Ejaar`).
2. Deploy the `publish/ejraa360` contents over `C:\inetpub\ESEMS` (this is what
   `ejaar-redeploy.ps1` automates) — **but PRESERVE the box's `appsettings.json`**
   (and `appsettings.Production.json`, `web.config` if it carries the WebDAV
   removes). Restore them after the copy if the deploy overwrites them.
3. Confirm `web.config` still has the WebDAV `<remove name="WebDAVModule"/>` /
   handler removes (the host has WebDAV; without it, any DELETE/PUT 405s).
4. `Start-WebAppPool -Name Ejaar`.
5. Watch the app's stdout/log on first hit: it runs `Database.Migrate()` →
   should log "No migrations were applied" (you applied them in Step 3) or apply
   the additive one cleanly.

## STEP 5 — Re-import org structure (destructive path only)
The merge reset org-unit links, so org-scoped data shows unassigned until the
client's structure is reloaded.
1. Log in as Admin → **Settings Hub → Data Import → Legacy client imports**.
2. Import **Org Structure FIRST** (`mbrhe-org`) — recreates `organization_units`
   + responsibilities (the importer fails fast if you do Assets first — F-023).
3. Then re-link the rest as needed (Assets `mbrhe-assets`, etc.). Use the
   per-run **"Delete this import"** undo if anything lands wrong, then re-import.
4. Spot-check: a Process/Service/Improvement shows its owning unit again.

## STEP 6 — Smoke test (against the BOUND host, not localhost)
Hit `https://ejraa360.com/App/...` (PathBase matters):
- `/App/Account/Login` loads; log in.
- `/App/Dashboard` (or `/App/MySpace`) renders; nav links resolve under `/App`.
- **Account → Profile → change password** works (FU-002: confirm you stay logged
  in; an old session in another browser gets bounced to login within ~30 min).
- A scoped (non-admin) user sees only their unit's data on a list page.
- Export a PDF in **Arabic** UI → Arabic columns render (not tofu) — F-002.
- Quick-Search (Ctrl+K) in Arabic returns Arabic names with `/App`-prefixed links.
- Org Units Index → a unit has a working **Delete** button.

## STEP 7 — Rollback (if any step fails)
1. `Stop-WebAppPool -Name Ejaar`.
2. Restore the DB: `RESTORE DATABASE [ESEMS] FROM DISK = N'...ESEMS_pre_af364f4_2026-05-24.bak' WITH REPLACE;`
3. Restore the app folder from `C:\inetpub\_backup\ESEMS_pre_af364f4`.
4. `Start-WebAppPool -Name Ejaar`; smoke `/App/Account/Login`.

## STEP 8 — Post-deploy housekeeping
- Confirm `Bootstrap__RunSeeder=false` on the box (env var) so restarts don't
  re-seed bootstrap users.
- Confirm `AllowedHosts` = `ejraa360.com` (per-tenant) in the box's appsettings.
- Re-run any pending **seed SQL scripts** noted from earlier sweeps if their data
  isn't present (strategic objectives, etc.).
- Record the new `__EFMigrationsHistory` top row + the deployed commit `af364f4`.

---
### Artifacts in this repo
- `deploy/ejraa360-migrate-to-af364f4.sql` — idempotent migration script (Step 3).
- This runbook.
