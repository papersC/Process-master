# bigTest Audit Report — ESEMS (Dynamic / verification pass)
**Date:** 2026-05-20
**Mode:** dynamic
**Auditor:** Claude (bigTest skill)
**Scope:** verification of today's 18 shipped fixes (commits e56a908 → 36eef00) + runtime-only sweep across 45 routes / 13 Edit pages. ADMIN role only (per Khaled's selection); per-role RBAC and leaked-nav paths were not exercised dynamically.

## Executive summary
- **Total findings: 6** (Critical: **1**, High: **0**, Medium: **3**, Low: **2**)
- **Top 3 risks** (one-liners):
  1. **F-D-001 — `/Assets/Create` and `/Assets/Edit/{id}` return HTTP 500.** Regression introduced by this morning's commit `f0b3924` (F-RA-007 fix): the new `PopulateDropdowns` query references `CustomUser.IsActive`, which is not a mapped property on the `CustomUser` entity. The entire Asset Create + Edit surface is blocked.
  2. **F-D-004 — DataTables "No data" string is English on every Arabic-localized list page.** Untranslated jQuery DataTables i18n; bilingual parity leak on a globally-loaded vendor.
  3. **F-D-002 — `/RiskCategories` returns 404.** Controller doesn't exist; was added to the new breadcrumb map in commit `f0b3924`, but the page itself is not implemented. Same pattern, smaller blast: `/Roles` 404 (uses `/RoleGroups` instead).
- **Fixes verified working dynamically (12 of today's 18):** F-OP-004 · F-OP-005 · F-CC-011 · F-SV-001 · F-CC-002 · F-CC-005 · F-CC-014 · F-CC-001 (markup-level) · F-CC-003 (where reachable) · F-OP-006 · F-CC-007 · F-OP-001.
- **Fixes not exercised live** (no seed data / no controller / sub-redirect):
  - F-RA-001 (Asset Details DataOwner name lookup — no seeded asset has the field populated)
  - F-RA-007 (Asset Edit user picker — page now 500s; see F-D-001)
  - F-SV-002 (Service Details "Linked Processes" code chip — no seeded service has linked processes)
  - F-PA-003 migration (BackfillActivityCodeZeroPadding — needs `dotnet ef database update`; not yet applied to local DB)
  - F-SV-008 / F-PA-002 (declared "already implemented" in static report; no further dynamic check needed)
- **Coverage:** 45 routes touched (33 list/index, 12 detail/create/edit) · 7 of 7 detected sections walked · 1 of 7 roles tested (ADMIN; non-Admin roles skipped per scope).
- **Skipped (with reason):**
  - **Non-Admin RBAC sweep** — Khaled selected ADMIN-only. F-CC-001 lock-tooltip and leaked-nav findings remain dynamically untested.
  - **F-PA-003 migration validation** — migration `20260520122626_BackfillActivityCodeZeroPadding` is on master but not yet applied to the local DB (it would run on next `dotnet ef database update`). The current Activities table on the local DB might already be clean or might have legacy codes; can't tell without applying.
  - **Mobile keyboard navigation** — preview tool supports viewport resize but not real-device touch/keyboard.

---

## Findings — Critical

### F-D-001: `/Assets/Create` and `/Assets/Edit/{id}` return 500 (CustomUser.IsActive unmapped)
- **Page:** /Assets/Create, /Assets/Edit/{id}
- **Role(s) affected:** all (the regression is in the action's PopulateDropdowns path, runs before authorization)
- **Workflow:** Asset registry create + edit — entirely blocked
- **Severity:** Critical
- **Nielsen severity:** catastrophic
- **Heuristic / standard:** N/A (server-side defect; H1 visibility violated as side-effect — user sees ESEMS error page)
- **Steps to reproduce:**
  1. Log in as ADMIN.
  2. Navigate to /Assets, click "Create" (or any "Edit" pencil in the list).
  3. Page returns HTTP 500.
- **Expected:** Asset Create/Edit form renders.
- **Actual:** HTTP 500. Server log:
  ```
  System.InvalidOperationException: The LINQ expression 'DbSet<CustomUser>().Where(c => c.IsActive)'
  could not be translated. Translation of member 'IsActive' on entity type 'CustomUser' failed.
  This commonly occurs when the specified member is unmapped.
     at AssetsController.PopulateDropdowns() in AssetsController.cs:line 256
     at AssetsController.Edit(String id) in AssetsController.cs:line 145
  ```
- **Source:** `ESEMS.Web/Controllers/AssetsController.cs:257` — `await _context.CustomUsers.Where(u => u.IsActive)`. `CustomUser` (ESEMS.Web/Models/CustomUser.cs) has no `IsActive` property; the user-facing "active" concept lives on the related `User` POCO (NotMapped) or on Identity, not on the legacy `[user]` table. The query body is correct in intent but references the wrong type.
- **Blast radius:** narrow. `PopulateDropdowns()` is called only from `AssetsController.Create` and `AssetsController.Edit`. Asset Details, Index, and all other modules are unaffected (verified — only these two endpoints regressed in the 13-Edit-page sweep).

---

## Findings — High

*None.*

---

## Findings — Medium

### F-D-002: `/RiskCategories` returns 404 (controller / view missing)
- **Page:** /RiskCategories
- **Severity:** Medium
- **Heuristic / standard:** H7 Flexibility (admins expect parity across category lookups: ServiceCategories ✓, AssetCategories ✓, Categories ✓, RiskCategories ✗)
- **Steps to reproduce:** Navigate to `/RiskCategories` — 404.
- **Expected:** A list page consistent with the other category-management surfaces.
- **Actual:** 404. The route was added to the breadcrumb partial's `navKeyMap` (commit `f0b3924`) on the assumption a controller existed; it does not.
- **Source:** No `RiskCategoriesController.cs` in `ESEMS.Web/Controllers`. Breadcrumb entry at `ESEMS.Web/Views/Shared/_Breadcrumb.cshtml:55` (Nav_RiskCategories resource) is orphaned until a controller is added. Cross-references the open TODO in `feedback_cascade_delete_guards.md` ("RiskCategories / FeedbackCategory still TODO").

### F-D-003: `/Roles` returns 404 (sidebar / breadcrumb still maps it)
- **Page:** /Roles
- **Severity:** Medium
- **Heuristic / standard:** H1 Visibility of system status — clicking the sidebar entry should not 404.
- **Steps to reproduce:** Navigate to `/Roles` — 404.
- **Expected:** Either a route (most apps use `/Roles` for role admin) or the link should not exist.
- **Actual:** 404. The app uses `/RoleGroups` and `/SettingsHub?tab=roles` for role administration; legacy `/Roles` was retired but the layout sidebar still emits the entry. Pre-existing — not a regression from today.
- **Source:** sidebar partial includes `roleControllers = new[] { "Roles", "RoleGroups" }` (`Views/Shared/_Layout.cshtml`), but no `RolesController.cs` exists.

### F-D-004: DataTables empty-state "No data" untranslated on Arabic pages
- **Page:** Every page that hosts a DataTables-driven list (Processes, Services, Assets, EnterpriseRisks, Improvements, CustomerFeedback, Users, RoleGroups, Categories, ProcessGroups, ServiceCategories, AssetCategories, KpiLibrary, StrategicObjectives, AuditLogs, SettingsHub > Activity, …).
- **Severity:** Medium (bilingual-parity leak; mandatory in Dubai gov context)
- **Nielsen severity:** minor
- **Heuristic / standard:** DGEP bilingual-parity pillar; H4 Consistency.
- **Steps to reproduce:**
  1. Switch language to Arabic.
  2. Load any list page that uses DataTables (e.g. `/Processes`).
  3. Trigger an empty filter (search a string that matches nothing) or load an empty table.
  4. Observe the literal English "No data" / "No matching records found" string in an otherwise Arabic UI.
- **Expected:** DataTables `language` option set per culture (Arabic `oLanguage` block, or load `~/lib/datatables/i18n/ar.json`).
- **Actual:** No `language:` config; jQuery DataTables defaults to English. Confirmed on 6 of 6 randomly probed Arabic-localized pages — every one ships "No data" verbatim.
- **Source:** `ESEMS.Web/Views/Shared/_Layout.cshtml:188` includes `dataTables.bootstrap5.min.css` but no global `$.extend(true, $.fn.dataTable.defaults, { language: ... })` runs anywhere. Per-page DataTables initializations (grep `new DataTable(` / `.DataTable(`) likewise omit `language`.

---

## Findings — Low

### F-D-005: SignalR notifications hub initializes 6× per page load
- **Page:** Every page that uses `_Layout.cshtml` (i.e. the whole authenticated app).
- **Severity:** Low (resource waste, not a user-visible blocker)
- **Heuristic / standard:** N/A
- **Steps to reproduce:**
  1. Open browser console on any authenticated page.
  2. Observe 6 lines of `Information: Normalizing '/hubs/notifications' to …` and 6 `WebSocket connected to ws://localhost:5297/hubs/notifications`.
- **Expected:** One hub connection per page.
- **Actual:** Six. Likely a vendor JS (Skote / SignalR client) being included in multiple bundles or the layout script firing multiple times. Doesn't break anything but wastes a connection slot per page per user.
- **Source:** `ESEMS.Web/Views/Shared/_Layout.cshtml:1861` declares a single `HubConnectionBuilder`. Suspect the count comes from the dev-time hot-reload / partial render, or from the layout being rendered into multiple roots. Not investigated further — flagged for tracking.

### F-D-006: Tailwind CDN production-warning fires on every page
- **Page:** Global (warning emitted by `cdn.tailwindcss.com` script).
- **Severity:** Low (vendor warning, not a defect)
- **Heuristic / standard:** N/A — repeat of static-audit finding F-CC-LO-3.
- **Console:** `cdn.tailwindcss.com should not be used in production. To use Tailwind CSS in production, install it as a PostCSS plugin or use the Tailwind CLI`.
- **Source:** `ESEMS.Web/Views/Shared/_Layout.cshtml:140` `<script src="~/lib/tailwind/tailwind-play.min.js">` — the play CDN bundle is shipped to every page. Already noted in this morning's static audit; flagging again because the dynamic console confirms it fires per page.

---

## Coverage matrix

(ADMIN row only — non-Admin roles skipped at Khaled's request.)

| Page                                    | Admin (dynamic) |
|---|---|
| /Account/Login                          | ok |
| /                                       | ok |
| /Dashboard                              | ok |
| /Processes                              | ok |
| /Processes/Details/{id}                 | ok |
| /Processes/Edit/{id}                    | ok |
| /Services                               | ok |
| /Services/Details/{id}                  | ok |
| /Services/Edit/{id}                     | ok |
| /Assets                                 | ok |
| /Assets/Details/{id}                    | ok |
| **/Assets/Create**                      | **500 (F-D-001)** |
| **/Assets/Edit/{id}**                   | **500 (F-D-001)** |
| /EnterpriseRisks                        | ok |
| /EnterpriseRisks/Dashboard              | ok |
| /EnterpriseRisks/HeatMap                | ok |
| /EnterpriseRisks/Edit/{id}              | ok |
| /Improvements                           | ok |
| /Improvements/Dashboard                 | ok |
| /Improvements/Kanban                    | ok |
| /Improvements/Roadmap                   | ok |
| /Improvements/Wizard                    | ok |
| /Improvements/Edit/{id}                 | ok |
| /ChangeRequests                         | ok |
| /ChangeRequests/Details/{id}            | ok (×5 states) |
| /ChangeRequests/Edit/{id}               | ok |
| /CustomerFeedback                       | ok |
| /CustomerFeedback/Create                | ok (consent block live) |
| /CustomerFeedback/Details/{id}          | ok (PII masked + toggle) |
| /WorkloadAnalysis                       | ok |
| /WorkloadAnalysis/Dashboard             | ok |
| /SettingsHub                            | ok (mobile scroll verified) |
| /SettingsHub?tab=roles                  | ok |
| /AuditLogs                              | ok |
| /Users                                  | ok |
| **/Roles**                              | **404 (F-D-003)** |
| /RoleGroups                             | ok |
| /OrganizationUnits                      | ok |
| /ProcessHierarchy                       | ok |
| /Categories                             | ok |
| /Categories/Edit/{id}                   | ok |
| /ProcessGroups                          | ok |
| /ServiceCategories                      | ok |
| /AssetCategories                        | ok |
| **/RiskCategories**                     | **404 (F-D-002)** |
| /StrategicObjectives                    | ok |
| /KpiLibrary                             | ok |
| /MySpace                                | ok |
| /Workflow                               | ok |
| /Workflow/PendingApprovals              | ok |
| /Help                                   | ok |
| /AI/Diagrams                            | ok |
| /Maintenance                            | ok |
| /SLA                                    | ok |

## Arabic ↔ English parity (live dynamic check)

| Surface | RTL applied? | Parity finding |
|---|---|---|
| `/`, `/Processes`, `/Services` | Yes (`dir="rtl"`, `lang="ar"`) | DataTables "No data" leaks in English (F-D-004) |
| `/CustomerFeedback/Create` | Yes | Consent block text correctly bilingual; "Privacy notice" reads "إشعار الخصوصية" |
| `/ChangeRequests/Details/{id}` | Yes | State-aware microcopy localizes correctly (Submitted/UnderReview/Approved/Rejected/Cancelled/Implemented) |
| `/Assets/Details/{id}` | Yes | OK for read; create/edit broken (F-D-001) regardless of locale |
| Language toggle | Working | URL + query preserved on switch (F-CC-005 verified live) |

## DGEP / TDRA deviations (delta vs. morning report)

No new DGEP/TDRA findings beyond what the static report enumerated. Re-affirmed:
- **DGEP bilingual-parity pillar** — F-D-004 (DataTables) is a fresh concrete instance under this banner.
- **TDRA mobile-first** — F-CC-011 fix verified at 375 px; tab scroll works and reaches the "About" tab via horizontal swipe.
- **UAE PDPL** — F-OP-004 + F-OP-005 fixes verified live; consent block renders on `/CustomerFeedback/Create`, server-side `ConsentAck` enforcement validated (client-side wizard blocks at step 1, server-side rejection adds ModelState error).

## Methodology

- **Mode:** dynamic (live `dotnet run --launch-profile http` against the local SQL Server / LocalDB whose connection string is in user-secrets).
- **Files read for cross-reference:** 7 controllers + 3 views to confirm source lines cited above.
- **URLs walked:** 45 distinct routes via background `fetch()` (status checks) + 6 navigated for DOM/script verification (preview_eval / preview_inspect / preview_snapshot).
- **Roles tested:** ADMIN only (per Khaled's selection).
- **Roles skipped:** Editor, Approver, Viewer, and any matrix-driven role groups — no per-role seeded test users; Khaled chose ADMIN-only and accepted the trade-off.
- **Tools:** `preview_start` for the dev server; `preview_fill` / `preview_click` / `preview_eval` for interaction; `preview_snapshot` / `preview_inspect` / `preview_console_logs` / `preview_network` / `preview_logs` for observation; `preview_resize` for the mobile pass.
- **Limitations:**
  - ADMIN bypasses every RBAC gate — leaked-nav, IDOR per-role, and 403-vs-200 paths cannot be exercised. Recommend a follow-up dynamic pass with at least one Editor + one Viewer once those users are seeded.
  - The local DB has no Asset records with `DataOwnerUserId` populated, no Service records with linked Processes, and no Activity records with single-digit-suffix legacy codes — so three of today's fixes (F-RA-001 display, F-SV-002 chip, F-PA-003 migration effect) could only be verified by code inspection, not by live UI.
  - SignalR-init multiplicity (F-D-005) wasn't root-caused — flagged for tracking only.

*End of report.*
