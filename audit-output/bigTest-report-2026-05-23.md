# bigTest Audit Report — ESEMS.Web (ejraa360)
**Date:** 2026-05-23
**Mode:** static
**Auditor:** Claude (bigTest skill)
**Scope:** ~120+ Razor views · 43 controllers (incl. 6 API) · RBAC = permission-claim policies (legacy ADMIN/EDITOR/APPROVER/VIEWER + ~70 granular `Module.Action` + matrix RoleGroups) · cultures en + ar (RTL) · ~10 end-to-end journeys · run immediately after the org-unit GUID→int merge.

## Executive summary
- **Total findings: 29** — Critical: 2, High: 4, Medium: 13, Low: 10.
- The system is **unusually well-hardened**: global `AutoValidateAntiforgeryToken`, global authenticated `FallbackPolicy`, per-record IDOR guards + load-then-patch on the core entities, fail-closed scoping, self-approval blocks, all `Html.Raw` JSON/JS-encoded, no raw SQL. The org-unit merge is **clean** — no leftover GUID/string-id assumptions, RevertImport transaction-safe, import parent-FK split correct.
- **Top 3 risks:**
  1. `Improvements/Transition` lets an **Editor (no Approve permission) approve/reject** an initiative via a crafted POST — bypasses the approval engine, self-approval guard, and desyncs the workflow row (F-001).
  2. **Arabic PDF exports render as blank boxes** — QuestPDF has no Arabic font registered, so every Arabic PDF column is empty across 4 controllers; the Arabic half of a core reporting feature is non-functional (F-002).
  3. **WorkloadAnalysis by-id actions have no record-scope check** — a unit-scoped user can read/edit/delete/export any unit's scenarios (IDOR), the one place the app's own scoping pattern wasn't applied (F-003).
- **Coverage:** 100% controllers read, ~100% views inventoried (static read-through), 8 roles modelled. **Not validated at runtime** (static mode): real auth/session flow, actual rendered PDFs, JS console, layout under data load.
- **Skipped (with reason):** dynamic click-through, screenshots, real export byte-output, live RBAC 403/200 confirmation — all require dynamic mode (a running server + a seeded user per role).

---

## Findings — Critical

### F-001: `Improvements/Transition` lets an Edit-only user approve/reject (approval-engine bypass)
- **Page:** POST `/Improvements/Transition`
- **Role(s) affected:** Editor (has `Improvement.Edit`, explicitly NOT `Improvement.Approve`)
- **Workflow:** Improvement approval / lifecycle
- **Severity:** Critical · **Nielsen severity:** N/A · **Standard:** RBAC / DGEP governance
- **Steps to reproduce:**
  1. Log in as an Editor-role user.
  2. Find an initiative in `UnderReview`.
  3. POST `/Improvements/Transition` with `target=Approved` (or `Rejected`) + valid anti-forgery token.
- **Expected:** Approval must run through `Approve`/`Reject` (which enforce `Improvement.Approve`, the assigned-approver check, the self-approval guard, and advance the `WorkflowInstance`). The Kanban `UpdateStatus` action already blocks these targets (FLOW-011).
- **Actual:** `Transition` is gated only on `Improvement.Edit` and defers to the FSM, which permits `UnderReview → {Approved, Rejected}`. Status flips with no approver authorization, no self-approval block, and the linked `WorkflowInstance` is left orphaned at `Submitted`/`UnderReview`.
- **Source:** `Controllers/ImprovementsController.cs:1301` (action), `:1303` (policy); `Services/Improvements/ImprovementStatusMachine.cs:44` (FSM allows the transition); contrast `Controllers/ImprovementsController.cs:464` (`UpdateStatus`, which guards it).

### F-002: Arabic PDF exports render as blank boxes (no Arabic font in QuestPDF)
- **Page:** PDF export on Processes / Services / Risks / Incidents / Dashboard Top-Categories / Workload
- **Role(s) affected:** all roles, **Arabic UI users specifically**
- **Workflow:** Reporting / export
- **Severity:** Critical · **Nielsen severity:** catastrophic · **Standard:** DGEP bilingual-parity pillar / TDRA; H1
- **Steps to reproduce:**
  1. Switch UI to Arabic.
  2. Export any of the above to PDF.
  3. Open the PDF — the Arabic ("Name (AR)") columns are empty rectangles (tofu).
- **Expected:** Arabic glyphs render; Arabic users get a usable report.
- **Actual:** QuestPDF's default font (Lato) has no Arabic glyphs and no `FontManager.RegisterFont`/`.FontFamily(...)` is set anywhere; the Arabic half of every PDF export is unreadable. (Excel/CSV are fine — ClosedXML embeds fonts, CSV uses a UTF-8 BOM.)
- **Source:** `Controllers/Api/ExportController.cs:454` (processes NameAr), `:493` (services), `:104/:188/:272` (DefaultTextStyle, no font family); `Controllers/DashboardController.cs:411` / `:297`; `Controllers/WorkloadAnalysisController.cs:790`.

---

## Findings — High

### F-003: WorkloadAnalysis by-id actions lack per-record scope checks (IDOR)
- **Page:** `/WorkloadAnalysis/{Details,Edit,Delete,Calculate,ExportScenario,ExportScenarioPdf,Compare,Clone,BulkReassignConfig,AddLineItem,UpdateLineItem,RemoveLineItem}`
- **Role(s) affected:** any unit-scoped user (e.g. "Process Owner"/"Improvement Analyst" RoleGroup → `ScopeLevel=OwningUnit`) with `Workload.View/Edit/Delete`
- **Workflow:** Workload analysis
- **Severity:** High · **Nielsen severity:** N/A · **Standard:** RBAC record-level (IDOR)
- **Steps to reproduce:** As a unit-scoped user, GET `/WorkloadAnalysis/Details/{id-of-another-unit's-scenario}` (or `Edit`/`ExportScenarioPdf`/POST `Delete`).
- **Expected:** `NotFound()` after `scope.CanAccess(scenario)` — exactly as `EnterpriseRisks.Details`/`Incidents.Details` do.
- **Actual:** List/dashboard views filter by scope, but **no by-id action re-checks** `CanAccess`; full read/edit/delete/export of any unit's scenario. (`WorkloadScenario.Id` is a GUID so not enumerable — bounded reachability — but it's the one controller that omits the app-wide pattern.)
- **Source:** `Controllers/WorkloadAnalysisController.cs` — `:113` Details, `:190/:220` Edit, `:284` Delete, `:592` Calculate, `:710/:768` Export(Pdf), `:899` Compare, `:633` Clone, `:939` BulkReassignConfig, `:383/:453/:534` line-items.

### F-004: Workflow `ProcessActionAsync` doesn't re-check terminal status (re-entrant decision overwrite)
- **Page:** Workflow inbox → POST `/Workflow/ProcessAction`
- **Role(s) affected:** assigned approver
- **Workflow:** any approval routed through the unified inbox
- **Severity:** High · **Nielsen severity:** N/A · **Standard:** state-machine integrity
- **Steps to reproduce:** After a workflow is `Rejected` (or `Approved` at final level), the same approver re-POSTs `ProcessAction` with `action=Approved`.
- **Expected:** a decided/terminal workflow refuses further actions.
- **Actual:** no terminal-status guard; `workflow.Status` is reassigned and committed, flipping a rejected item to approved and firing duplicate notifications. (Dedicated controller actions guard their own entity; only the inbox path mutates the workflow row post-decision.)
- **Source:** `Services/Workflow/WorkflowService.cs:156` (no terminal check), status reassigned `:260/:274`; inbox caller `Controllers/WorkflowController.cs:66`.

### F-004b: OrganizationUnit Create can 500 on empty/duplicate Code (no server validation)
- **Page:** POST `/OrganizationUnits/Create`
- **Role(s) affected:** Admin / `OrganizationUnit.Create`
- **Workflow:** Org-unit management (heavily used post-merge for cleanup)
- **Severity:** High · **Nielsen severity:** major · **Standard:** H9 (error prevention / quality), H5
- **Steps to reproduce:** POST a unit with blank or duplicate `Code` (client JS bypassed, or two blanks in a row).
- **Expected:** field-level validation message.
- **Actual:** `OrganizationUnit` model has no `[Required]`/`[StringLength]` on `Code`/`NameEn`/`NameAr`, but `Code` has a unique index; `ModelState.IsValid` passes and `SaveChangesAsync()` throws an unhandled `DbUpdateException` → 500.
- **Source:** `Models/APQC/OrganizationUnit.cs` (no validation attributes), unique index `Data/ApplicationDbContext.cs:816`, `Controllers/OrganizationUnitsController.cs:167` (no try/catch), client-only required `Views/OrganizationUnits/Create.cshtml:44`.

### F-005: Global Quick-Search returns English-only names on Arabic pages
- **Page:** Quick-Search (Ctrl+K) → `/api/search`
- **Role(s) affected:** all Arabic UI users
- **Workflow:** Global navigation/search
- **Severity:** High · **Nielsen severity:** major · **Standard:** DGEP bilingual parity; H2
- **Steps to reproduce:** Set UI to Arabic, press Ctrl+K, search — result titles/descriptions come back in English (Latin).
- **Expected:** culture-aware `NameAr`/`DescriptionAr` with English fallback.
- **Actual:** `SearchController` always projects `NameEn`/`DescriptionEn` regardless of `CurrentUICulture`.
- **Source:** `Controllers/Api/SearchController.cs:54-55, 68-69, 84-87, 100-103, 116-119, 132-135, 148-151`.

---

## Findings — Medium

### F-006: Improvements child/relationship & helper actions skip the parent-scope check (IDOR)
- **Page:** `/Improvements/{AddMeasurement,UpdateMeasurement,DeleteMeasurement,RecordReading,SaveBatchReadings,AddTeamMember,RemoveTeamMember,LinkRisk,UnlinkRisk,UpdateImprovementRisk,GetReadings,ComparisonData,GetAvailableRisks,GetLinkedRisks}`
- **Role(s):** unit-scoped user with `Improvement.Edit/View` · **Severity:** Medium · **Nielsen:** N/A · **Standard:** IDOR
- **Steps:** POST `AddMeasurement`/`DeleteMeasurement`/`LinkRisk` with an id belonging to an out-of-scope initiative.
- **Expected:** 404 after a `CanAccess` probe (cf. `ServicesController.CanAccessServiceAsync`, which gates every link helper). **Actual:** mutate/read child rows on out-of-scope initiatives.
- **Source:** `Controllers/ImprovementsController.cs:1707,1746,1835,1166,1235,1861,1904,1994,2058,2090,1283,1679,2129,2170`.

### F-007: `AIController.SaveBPMNToProcess` overwrites a diagram without a scope check (IDOR)
- **Page:** POST `/AI/SaveBPMNToProcess` · **Role(s):** scoped `Process.Edit` user · **Severity:** Medium · **Standard:** IDOR
- **Expected:** `scope.CanAccess(process)` (as `ProcessesController.UpdateBpmnDiagram:451` / SEC-008 does). **Actual:** loads process by id and overwrites `BpmnDiagram` with no scope probe.
- **Source:** `Controllers/AIController.cs:1077`, loads `:1090`.

### F-008: AssetsController asset-risk link helpers skip the per-asset scope check (IDOR)
- **Page:** `/Assets/{LinkRisk,UnlinkRisk,UpdateAssetRisk,GetAvailableRisks,GetAvailableAssets}` · **Role(s):** scoped `Asset.Edit` · **Severity:** Medium · **Standard:** IDOR
- **Expected:** `CanAccess` probe on the asset (cf. ServicesController). **Actual:** acts on `assetId`/`riskId` join rows with no probe. Low blast radius (join table) but same class.
- **Source:** `Controllers/AssetsController.cs:385,441,471,504,542`.

### F-009: WorkflowController cross-module status sync is two-phase, not atomic
- **Page:** POST `/Workflow/ProcessAction` · **Workflow:** approvals via unified inbox · **Severity:** Medium · **Standard:** transactional integrity (FLOW-002 intent)
- **Expected:** workflow + linked entity status change together or not at all. **Actual:** `ProcessActionAsync` commits, then a second `SaveChanges` syncs the Improvement/ChangeRequest; if the second throws, the workflow is Approved while the entity stays UnderReview/Submitted.
- **Source:** `Controllers/WorkflowController.cs:66`, second save `:102` / `:144`.

### F-010: OrganizationUnits.Delete uses `CanAdmin` instead of the granular `OrganizationUnit.Delete` policy
- **Page:** POST `/OrganizationUnits/Delete` · **Role(s):** holder of `OrganizationUnit.Delete` (not admin) — blocked · **Severity:** Medium · **Nielsen:** minor · **Standard:** H4 consistency / Plan-X granular-policy regression
- **Expected:** `[Authorize(Policy = AppPolicies.Module.OrganizationUnit.Delete)]` (every other action on this controller uses the granular policy). **Actual:** gated on `CanAdmin`; fail-safe (more restrictive) but inconsistent.
- **Source:** `Controllers/OrganizationUnitsController.cs:264`; policy exists `Security/AppPolicies.cs:156`.

### F-011: OrganizationUnit has no Delete affordance in the UI (CRUD-incomplete)
- **Page:** `/OrganizationUnits/{Index,Details,Edit}` · **Severity:** Medium · **Nielsen:** major · **Standard:** H7 efficiency / H4 consistency
- **Expected:** a unit-level delete button (controller soft-delete + in-use guard exists). **Actual:** views only wire `DeleteResponsibility`; units can be created/edited but never removed via UI — most needed right now to clean mis-imported rows.
- **Source:** controller `Controllers/OrganizationUnitsController.cs:266` (delete exists); no delete in `Views/OrganizationUnits/Index.cshtml` / `Details.cshtml` / `Edit.cshtml`.

### F-012: Import upload has no server-side file-type/content validation
- **Page:** POST `/SettingsHub/ImportUpload` · **Role(s):** Admin · **Severity:** Medium · **Nielsen:** minor · **Standard:** H9 / robustness
- **Expected:** extension/signature allow-list + friendly message before streaming to the importer. **Actual:** only `file.Length == 0` is checked; `.xlsx,.xls` is client-side only; a non-spreadsheet surfaces a raw `"Import failed: <exception>"`. (Size cap `[RequestSizeLimit(50_000_000)]` is present — good.)
- **Source:** `Controllers/SettingsHubController.cs:453-456`, raw error `:482`; client accept `Views/SettingsHub/_DataImportPartial.cshtml:38`.

### F-013: User Profile is largely non-functional + shows fabricated security state
- **Page:** `/Account/Profile` · **Severity:** Medium · **Nielsen:** major · **Standard:** H1 / TDRA self-service
- **Actual:** Edit, Change-password, Sign-out-others are hard-`disabled` ("Coming soon"); the Security tab shows static "Last changed: Not available", "Last sign-in: This session / Secure", "Active sessions: 1 session", "Two-factor: Not enabled". For a government system, no self-service password change is a gap, and the fabricated "Secure / 1 session" chips mislead users about real security state.
- **Source:** `Views/Account/Profile.cshtml:99-106, 183-212`.

### F-014: Profile tab pattern has incomplete ARIA
- **Page:** `/Account/Profile` · **Severity:** Medium · **Nielsen:** major · **Standard:** TDRA accessibility / WCAG (H-N/A)
- **Actual:** `role="tablist"/"tab"` without `aria-selected`/`aria-controls`; panels lack `role="tabpanel"`/`aria-labelledby`/`tabindex`. Keyboard + screen-reader users can't perceive selection or arrow between tabs. (Skip-link + nav landmarks elsewhere are present — good.)
- **Source:** `Views/Account/Profile.cshtml:110-124, 127/179/217/226`.

### F-015: Live PDF export buttons silently produce Arabic-broken PDFs (compounds F-002)
- **Page:** every "Export PDF" button · **Severity:** Medium · **Nielsen:** major · **Standard:** H1 visibility
- **Actual:** users download a PDF that looks successful but has empty Arabic cells, with no warning. Until F-002 is fixed, these need a caveat or the Arabic column omitted.
- **Source:** e.g. `Views/WorkloadAnalysis/Index.cshtml:222`; SettingsHub export section.

### F-016: Dynamically injected links (search results) are not PathBase-rewritten under sub-app hosting
- **Page:** Quick-Search results · **Severity:** Medium (ejraa360 `/App` only) · **Standard:** deployment correctness
- **Actual:** `site.js` PathBase sweep covers static markup only, not innerHTML-injected content; SearchController returns root-absolute `/Processes/Details/…` URLs rendered via innerHTML → 404 under `/App`. (fetch/XHR are wrapped and fine.)
- **Source:** `wwwroot/js/site.js:14-18`; `Controllers/Api/SearchController.cs:56`.

### F-017: CSV export is vulnerable to formula injection
- **Page:** CSV export · **Role(s):** any Create-capable user (stored), anyone opening the CSV · **Severity:** Medium · **Standard:** security (CSV/formula injection)
- **Actual:** values are quoted but leading `=`,`+`,`-`,`@` are not neutralized; a name like `=HYPERLINK(...)` executes in Excel on open. Prefix risky cells with `'`.
- **Source:** `Services/Export/ExportService.cs:392-417`.

### F-018: `Account/Lockout.cshtml` is an orphan view
- **Page:** `/Views/Account/Lockout.cshtml` · **Severity:** Medium (hygiene) · **Standard:** coverage
- **Actual:** no `Lockout` action and never returned via `View("Lockout")`; lockout is handled inline in `Login`. Dead file — delete or wire up.
- **Source:** `Views/Account/Lockout.cshtml`; `Controllers/AccountController.cs:81, 261-267`.

---

## Findings — Low

### F-019: AI analysis POSTs can echo out-of-scope entity content to scoped users
- `/AI/{AnalyzeProcess,AnalyzeEnterpriseRisk,AnalyzeServicePerformance,AnalyzeIncident,AnalyzeImprovement}` load by id and return an AI summary with no `CanAccess` probe. `Ai.View`-gated, read-only. **Source:** `Controllers/AIController.cs:264,462,559,587,656`.

### F-020: `Tasks` (ProcessTask) by-id actions have no scope check
- `Details`/`Edit`/`Delete` load `ProcessTask` (carries `OwningUnitId`) with no probe — inconsistent with `AIBpmnReadController` (which scopes tasks via parent process, SEC-003). By-design low risk (process-structure editing). **Source:** `Controllers/TasksController.cs`.

### F-021: Users.Create does not reset privileged fields on the bound entity
- `Create` binds `CustomUser` and adds it directly, letting an admin set `Points`/`InnovatorLevel` via crafted fields; `Edit` has the load-then-patch hardening (FUNC-006) but Create doesn't. CanAdmin-only, new row. **Source:** `Controllers/UsersController.cs:104` vs `:217`.

### F-022: OrganizationUnits.Edit accepts an invalid `Level`/`ParentId` (cycle / wrong depth)
- Edit copies `Level` + `ParentId` from the form with no check that `Level == parent.Level+1` or that `ParentId` isn't a descendant. A cycle is tolerated by the depth-capped walks but skews the hierarchy. Defense-in-depth. **Source:** `Controllers/OrganizationUnitsController.cs:234-248`.

### F-023: Import order (Org → Assets) is a soft warning, not enforced
- Asset import warns + imports unassigned when an owner dept isn't found; recoverable via per-run undo, but a user who ignores the warning silently gets unlinked assets. **Source:** `Services/Import/ExcelImportService.cs:494-496`.

### F-024: Standard template imports (Process/Service/Asset/Risk) are not undoable
- Only the MBRHE legacy importers populate `result.Created`; the four template importers `AddRange`+`SaveChanges` with no manifest, so `RevertImport` can't roll them back. Known design gap, not an org-merge regression. **Source:** `Services/Import/ExcelImportService.cs:129-134,223-228,305-310,384-389`.

### F-025: Dead PDF code path `GeneratePdf` will throw if ever wired up
- `ExportService.GeneratePdf` builds a QuestPDF table with no `ColumnsDefinition(...)` → throws at compose. Latent (uncalled). **Source:** `Services/Export/ExportService.cs:440-474`.

### F-026: RTL detection inconsistency on ProcessHierarchy
- Uses `CurrentCulture.TextInfo.IsRightToLeft` while the rest of the app uses `CurrentUICulture.Name.StartsWith("ar")`; diverges if formatting/UI cultures differ. **Source:** `Views/ProcessHierarchy/Index.cshtml:8`.

### F-027: RACI pills under-escape on innerHTML injection (defense-in-depth)
- `pillHtml` escapes only `<` for unit/role names (not `&`,`>`,`"`,`'`); server-controlled today but inconsistent with `_Layout`'s full escaping. **Source:** `Views/Processes/Raci.cshtml:357-358`.

### F-028: Import error/warning details only via transient toast
- `upload()` toasts up to 3 row errors then they vanish; a 283-row import with many warnings has no persistent error log. **Source:** `Views/SettingsHub/_DataImportPartial.cshtml:266-280`.

### F-029: (reserved — consolidated into F-002/F-015)

---

## Coverage matrix (representative)
Roles: **Adm**=Admin · **Edt**=Editor · **App**=Approver · **Vwr**=Viewer · **Scd**=unit-scoped RoleGroup (Process Owner / Improvement Analyst) · **Gst**=guest.

| Page | Adm | Edt | App | Vwr | Scd | Gst |
|---|---|---|---|---|---|---|
| /Home/Index | ok | ok | ok | ok | ok | redirect-to-login |
| /Improvements (list) | ok | ok | ok | ok | ok (scoped) | 403-as-expected |
| /Improvements/Transition | ok | **leak (F-001)** | ok | n/a | leak (F-001) | 403 |
| /Improvements/AddMeasurement | ok | ok | n/a | n/a | **leak (F-006)** | 403 |
| /Workflow/ProcessAction | ok | n/a | re-entrant (F-004) | n/a | n/a | 403 |
| /WorkloadAnalysis/Details/{id} | ok | ok | n/a | ok | **leak (F-003)** | 403 |
| /OrganizationUnits (CRUD) | ok | ok (no Delete UI F-011) | n/a | view | n/a | 403 |
| /OrganizationUnits/Create | 500-on-bad-Code (F-004b) | — | — | — | — | 403 |
| /AI/SaveBPMNToProcess | ok | ok | n/a | n/a | leak (F-007) | 403 |
| /Assets/LinkRisk | ok | ok | n/a | n/a | leak (F-008) | 403 |
| /api/search (Arabic) | EN-only (F-005) | EN-only | EN-only | EN-only | EN-only | 403 |
| PDF export (Arabic) | tofu (F-002) | tofu | tofu | tofu | tofu | 403 |
| /Account/Profile | partial (F-013) | partial | partial | partial | partial | redirect-to-login |

Legend: ok / leak / 500 / tofu / 403-as-expected / redirect-to-login / n/a.

## Arabic ↔ English parity
- **PDF exports** — Arabic columns blank across Processes/Services/Risks/Incidents/Dashboard/Workload (F-002).
- **Quick-Search** — result names/descriptions English-only on Arabic UI (F-005).
- **Done right:** localized DataAnnotations validation messages (`Program.cs:278-292`), CSV UTF-8 BOM for Excel-Arabic, ~2689-entry `SharedResource.ar.resx`, culture-aware list/detail labels.

## DGEP / TDRA deviations
- **Bilingual parity** broken for PDF exports (F-002) and global search (F-005) — DGEP customer-experience + bilingual pillars.
- **Accessibility** — Profile tabs lack full ARIA (F-014); broader pages have skip-link + landmarks (good). TDRA accessibility — partial.
- **Self-service** — no working password change / session management on Profile (F-013) — TDRA mGov self-service.
- **Not assessed (static):** service SLA visibility, post-submission status tracking, per-service "how can we improve" feedback channel — recommend confirming in a dynamic pass.

## Methodology
- **Mode:** static read-through (no app run).
- **Files read:** 43 controllers, Program.cs, AppPolicies/LegacyRoleBridge, ScopingService + ScopeContextExtensions + QueryableScopeExtensions, ExcelImportService, ExportService, WorkflowService/ImprovementStatusMachine, every `Html.Raw`/raw-SQL call site, ~120 views inventoried + the org-merge-affected views read in full.
- **Roles modelled:** Admin, Editor, Approver, Viewer, scoped RoleGroups (Process Owner / Improvement Analyst / Risk Manager), Guest.
- **Tools:** Glob/Grep/Read; three parallel read-only sub-audits (security/RBAC+data, coverage/paths/processes, functions/UX/Nielsen/DGEP) synthesized here.
- **Limitations:** static mode cannot confirm runtime 403-vs-200 on each route, actual rendered PDF bytes, JS console/network errors, layout under real data load, session-timeout behavior, or true concurrency handling. IDOR findings are reachable only when scoped RoleGroups are assigned (the local `organization_units` table is empty in dev; the seeded Process-Owner/Improvement-Analyst groups carry OwningUnit scope, so they are reachable in a real tenant). Recommend a dynamic pass (one seeded user per role) to confirm the RBAC matrix and capture screenshot evidence, focused on F-001/F-003/F-006/F-007/F-008.
