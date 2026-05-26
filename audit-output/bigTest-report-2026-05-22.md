# bigTest Audit Report — ESEMS.Web (Enterprise Service Excellence Management System)

**Date:** 2026-05-22
**Mode:** dynamic (live app at `http://localhost:5297`) + static read-through
**Auditor:** Claude (bigTest skill)
**Scope:** ~43 controllers, 100+ Razor views, 4 legacy roles (ADMIN/EDITOR/APPROVER/VIEWER) + 4 matrix role-groups + Guest, EN/AR bilingual (RTL), 7 background services. `ESEMS.Web/` main tree only — `.claude/worktrees/**` excluded.
**Supersedes:** `bigTest-report.md` (2026-05-20, static-only) — kept for baseline diffing.

---

## Executive summary

- **Total findings: 80** — **Critical: 7 · High: 24 · Medium: 29 · Low: 20**
- This is a **mature, well-built application** with a strong baseline: every controller has class-level `[Authorize]`, granular `Module.Action` policies on writes, ~complete anti-forgery coverage, a clean XSS surface (no unsafe `Html.Raw`), no raw SQL, brute-force login lockout, exemplary resx parity (2306/2306 keys), correct decimal/culture validation, and div-by-zero-safe math. The findings below are the gaps **against** that baseline — concentrated in three areas: **record-level (IDOR) scope enforcement**, **mass-assignment on Edit**, and **approval-chain governance**.

### Top 3 risks
1. **Record-level data isolation is incomplete (IDOR).** `Processes` and `Services` Details/Edit/Delete load by id with no org-unit scope check (SEC-001), and three unscoped read channels — global search (SEC-004), exports (FUNC-004), and `AIBpmnReadController` (SEC-003) — leak cross-unit data and hand out the ids needed to exploit it. For a Dubai-gov system with unit-scoped roles, this is the headline issue.
2. **Mass-assignment via `_context.Update(boundEntity)` (FUNC-001).** ~11 controllers overwrite the whole entity from the POST body, so a user with only *Edit* can flip `IsDeleted=true` (soft-delete through Edit, bypassing every cascade-delete guard) or forge `CreatedById`/`CreatedAt`/`Version`.
3. **Approval governance gaps (FLOW-001/002/003).** No submitter≠approver check anywhere (self-approval possible), ChangeRequest workflow instances desync from the entity and orphan in the pending queue, and a 2-level chain can be cleared by one person. Segregation-of-duties failures in a government approval system.

### Coverage
- **Static:** ~100% of controllers and the workflow/UX/data layers read by 4 specialist agents.
- **Dynamic:** 48 routes walked as admin (100% returned 200, zero errors); RBAC sweeps run as viewer, editor, procowner, analyst; Arabic/RTL verified live; logout/session verified.
- **Roles fully tested dynamically:** Admin, Viewer, Editor, Guest. **Partial:** Approver. **Blocked (DYN-001):** the 4 pure role-group personas are non-functional as seeded, so scope-based RBAC and IDOR could not be reproduced live.

### Skipped / limitations
- **Scope-based IDOR (SEC-001) not dynamically confirmed** — no working org-scoped user exists in the seed (DYN-001). Rests on static file:line evidence.
- **No load/performance testing** (use `dotnet-full-test`). FUNC-005/009 flag latent scale risks by inspection only.
- **Windows Auth path (SEC-006)** depends on IIS config on the EC2 host — not verifiable locally.
- Screenshot capture errored in this environment (`UnknownVizError`); evidence is via accessibility snapshots, network/console logs, and live HTTP probes instead.

---

## Findings — Critical

### SEC-001 — Process & Service record-level access ignores org-unit scope (IDOR read+write+delete)
- **Source:** `Controllers/ProcessesController.cs:133` (Details), `:866`/`:951` (Edit GET/POST), `:1629` (Delete); `Controllers/ServicesController.cs:104` (Details), `:371`/`:415` (Edit)
- **Role(s):** any role-group scoped to `OwningUnit`/`Process` holding `Process.*`/`Service.*` (e.g. process-owner)
- **Severity:** Critical · **Nielsen:** N/A
- **Detail:** `Index` filters via `ApplyOwningUnitScope`, but by-id actions use bare `FindAsync(id)` and never call `scope.CanAccess(...)`. `AssetsController.Edit:141-166` shows the correct pattern (re-load + scope re-check). A scoped user can read/edit/delete any other unit's process/service by changing the id.
- **Expected vs Actual:** Expected `NotFound()` for out-of-scope ids; actual full access.
- **Note:** not reproduced live (DYN-001 blocks the only scoped accounts); static evidence is unambiguous.

### FLOW-001 — Self-approval possible in every approval chain (no submitter ≠ approver check)
- **Source:** `Services/Workflow/WorkflowService.cs:156-164`; `Controllers/ImprovementsController.cs:476-543`; `Controllers/ChangeRequestsController.cs:466-488`
- **Role(s):** any user holding both `*.Create`/`*.Edit` and `*.Approve` who is also the configured approver
- **Severity:** Critical · **Nielsen:** N/A · **Standard:** four-eyes / DGEP segregation of duties
- **Steps:** As the configured Level-1 approver, create + submit an initiative → it lands in your own queue → Approve → succeeds.
- **Expected vs Actual:** Expected submitter can never approve own submission; actual no layer compares `SubmittedById` to the acting approver.

### FLOW-002 — ChangeRequest WorkflowInstance is created then orphaned; approval paths desync
- **Source:** `Controllers/ChangeRequestsController.cs:195` (creates workflow, never advanced); `:466-488` (Approve flips status directly); `Controllers/WorkflowController.cs:81-102` (only syncs `EntityType=="Improvement"`)
- **Role(s):** Requester + Approver
- **Severity:** Critical · **Nielsen:** major (H1)
- **Steps:** Create a CR with an approver → approve from CR Details → CR shows Approved but the WorkflowInstance stays `Submitted` and lingers in `/Workflow/PendingApprovals` forever; or approve from the unified inbox → workflow flips but CR stays `Submitted`.
- **Expected vs Actual:** one atomic action should move both; actual two paths that never reconcile → phantom queue entries + contradictory status.

### FUNC-001 — Mass-assignment via `_context.Update(boundEntity)` flips `IsDeleted` + forges audit fields
- **Source:** `OrganizationUnitsController.cs:227`, `ProcessGroupsController.cs:179`, `AssetsController.cs:174`, `EnterpriseRisksController.cs:247`, `IncidentsController.cs:187`, `ProblemsController.cs:160`, `CustomerFeedbackController.cs:134`, `SLAController.cs:116`, `ActivitiesController.cs:132`, `TasksController.cs:190`, `MaintenanceController.cs:142`. Base: `Models/Common/BilingualEntity.cs:72` (`IsDeleted`/`CreatedById`/`CreatedAt`/`Version`/`DeletedAt` all public-settable)
- **Role(s):** any holder of the relevant `*.Edit` (Delete permission not required)
- **Severity:** Critical · **Nielsen:** N/A
- **Detail:** Edit POST binds the full entity and writes every column. Crafted POST `IsDeleted=true` soft-deletes via Edit — bypassing every cascade-delete guard. ChangeRequests/Improvements/StrategicObjectives/KpiLibrary patch named fields correctly; these 11 don't.
- **Expected vs Actual:** load tracked entity + copy only user-editable fields; actual whole-entity overwrite from untrusted post.

### FUNC-002 — `OrganizationUnitsController.Delete` has no cascade/in-use guard (orphans the org subtree)
- **Source:** `Controllers/OrganizationUnitsController.cs:244-256`
- **Role(s):** OrganizationUnit.Delete holder
- **Severity:** Critical · **Nielsen:** N/A
- **Detail:** Unconditional soft-delete of the scoping spine. Children units + owned Processes/Services/Assets/Activities/Tasks/Responsibilities/RACI keep pointing at a now-invisible FK; scope filters break. Sibling category controllers all have in-use COUNT guards; this one was missed.

### FUNC-003 — `AssetsController.Delete` has no cascade/in-use guard
- **Source:** `Controllers/AssetsController.cs:226-242`
- **Role(s):** Asset.Delete holder
- **Severity:** Critical · **Nielsen:** N/A
- **Detail:** Soft-deletes without checking ServiceAssets / AssetRisks / ChangeRequestAssets / Maintenance schedules+records / child real-estate assets (`ParentProjectId`) / Incidents+Problems. Services got this guard (`ServicesController.cs:673`); the leaf Asset entity didn't.

### UX-001 — Server-side validation errors render in English on Arabic pages
- **Source:** `Models/**/*.cs` (71 hardcoded `ErrorMessage` literals, 18 files) e.g. `Models/Common/BilingualEntity.cs:14,20`, `Models/RiskManagement/EnterpriseRisk.cs:48,55,63`, `Models/Improvement/Improvement.cs` (16)
- **Page:** every Create/Edit form · **Role(s):** all (Arabic users)
- **Severity:** Critical · **Nielsen:** major · **Standard:** H9 / bilingual parity (UAE 7-Star, DGEP)
- **Detail:** `Program.cs:263-277` localizes only attributes with no explicit `ErrorMessage`. These 71 hardcode English sentences that don't exist as AR resx keys, so Arabic users tripping server validation get English errors inside an otherwise-Arabic form — exactly what the localization infra was built to prevent.

---

## Findings — High

### SEC-002 — Process/Service Edit POST allows owner over-posting (scope escape via `OwningUnitId`)
- **Source:** `ProcessesController.cs:951-983`, `ServicesController.cs:415` · **Severity:** High
- Binds whole entity incl. `OwningUnitId` and `_context.Update(...)` with no re-check against the persisted owner (contrast `AssetsController.cs:157-166`). A scoped user can re-assign a record to another unit. (Pairs with SEC-001 + FUNC-001.)

### SEC-003 — `AIBpmnReadController` exposes process/BPMN data to any authenticated user, unscoped
- **Source:** `Controllers/AIBpmnReadController.cs:19` (bare `[Authorize]`), actions `:40-181` · **Severity:** High
- No `Process.View`, no scoping. Returns full process lists + BPMN XML for any id to any logged-in user (incl. Viewer). Clean cross-unit read channel.

### SEC-004 — Global search returns cross-scope records (names, ids, working detail URLs)
- **Source:** `Controllers/Api/SearchController.cs:24-141` · **Severity:** High
- No `IScopingService`. Returns names/descriptions + `/X/Details/{id}` for all units — leaks existence/metadata even where Details enforces scope, and hands out the ids for SEC-001.

### SEC-005 — AI write actions reachable by read-only roles (Viewer can mutate process BPMN)
- **Source:** `Controllers/AIController.cs:19` (bare `[Authorize]`); `SaveBPMNToProcess:1075`, `ImportBpmnFromFiles:1206` · **Severity:** High
- No granular policy; a Viewer can persist/overwrite BPMN onto a Process. (Anti-forgery is present — the gap is authorization granularity; writes should require `Process.Edit`.)

### SEC-006 — `WindowsLogin` authenticates by username with no credential/issuer verification
- **Source:** `Controllers/AccountController.cs:251-300` · **Severity:** High (conditional on IIS config)
- `[AllowAnonymous]` reads ambient `User.Identity`, strips domain, looks up by `Username`, signs in with full roles — safe only if IIS Windows Auth is provably enforced for this path. No assertion it's a Negotiate `WindowsIdentity`. **Verify IIS auth on the EC2 box**; if Windows Auth is enforced, downgrade to Low.

### SEC-007 — No global `AutoValidateAntiforgeryToken`; CSRF safety is per-action discipline
- **Source:** `Program.cs:254-258` · **Severity:** High (systemic/latent)
- Coverage is currently good (175 tokens ≈ 174 unsafe actions) but every new POST must remember the attribute. `Api/MySpaceController.cs:19-24` already recognized this locally — make it global.

### FLOW-003 — Two-level approval can be cleared by one person (no Level1 ≠ Level2 check)
- **Source:** `Services/Workflow/WorkflowService.cs:102-139`, `:181-205` · **Severity:** High
- No validation that Level1 and Level2 approvers differ; one person can satisfy a dual-control chain.

### FLOW-004 — Incident Resolve/Close have no state guard (re-entrant + illegal transitions)
- **Source:** `Controllers/IncidentsController.cs:238-253` (Resolve), `:261-274` (Close) · **Severity:** High
- `FindAsync` then overwrite status with no current-state check; a Closed incident can be "Resolved" backwards, clobbering timestamps/notes. ChangeRequests got `IsActionable` guards; Incidents never did.

### FLOW-005 — Improvement Details renders Approve/Reject buttons to non-approvers (incl. submitter)
- **Source:** `Views/Improvements/Details.cshtml:1948-1980` · **Severity:** High · **Nielsen:** major (H1)
- Buttons come solely from `ImprovementStatusMachine.ActionsFor(status)` — not gated by policy/ownership. Misleading affordance; reinforces FLOW-001.

### FLOW-006 — `WorkflowController.Details` leaks any workflow to any viewer (no scope/approver check)
- **Source:** `Controllers/WorkflowController.cs:140-145` · **Severity:** High
- `GetByIdAsync(id)` + `View`, guarded only by `Workflow.View`. Enumerate a GUID → read submitter/notes/approver chain/comments for any entity outside scope.

### FUNC-004 — ExportController ignores org scope (cross-unit data leak, incl. PII)
- **Source:** `Controllers/Api/ExportController.cs:15,34,121,205,289,342` · **Severity:** High
- Excel/PDF exports query whole tables with no scope filter; a unit-restricted user pulls every unit's risks/services/processes/incidents via `/api/export/...`. Risk export includes `Owner?.Email`.

### FUNC-005 — Exports buffer entire tables to memory and ignore the active filter
- **Source:** `ExportController.cs:36-86,207-255`; `WorkloadAnalysisController.ExportScenario` · **Severity:** High
- `.ToListAsync()` over the whole entity → workbook + `byte[]` double-buffer, no streaming/row cap (OOM risk at scale). Exports take no filter params, so "Export" after filtering still dumps everything.

### FUNC-006 — `UsersController` hard-deletes users (inconsistent + no in-use guard)
- **Source:** `Controllers/UsersController.cs:293-321` (`Remove(user)` :309); also `:217` mass-assignment · **Severity:** High
- Whole app soft-deletes; Users physically deletes a row referenced by Manager self-FK, CustomUserRole, ImprovementTeamMember, Asset DataOwner/Custodian, etc. → FK failure (500) or silent orphans.

### FUNC-008 — `MaintenanceController` CRUD is incomplete and unscoped
- **Source:** `Controllers/MaintenanceController.cs` (`CreateRecord:157`, `CreateSchedule:88`, `DeleteSchedule:211`) · **Severity:** High
- MaintenanceRecord has Create only (mistyped cost/date is permanent); no `IScopingService`; CreateRecord mutates the schedule's `NextScheduledDate` without checking the record's asset matches the schedule's asset.

### FUNC-009 — No server-side pagination; two lists silently cap at 100 rows
- **Source:** all entity `Index` actions; hard caps at `MaintenanceController.cs:60` & `SLAController.cs:158` (`.Take(100)`) · **Severity:** High
- Lists load the full filtered table and rely on client DataTables (only `AuditLogsController:48` paginates). `Maintenance/Records` & `SLA/Breaches` hide rows 101+ with no indication — a silent data-completeness bug that worsens as MBRHE data grows.

### UX-002 — Native `type="date"` pickers are Gregorian-only; no Hijri option
- **Source:** 17 inputs / 11 files (`Assets/*`, `SLA/*`, `Improvements/Wizard.cshtml:237`, `Maintenance/*`, etc.) · **Severity:** High · **Nielsen:** minor · **Standard:** H2 (gov calendaring)
- No Hijri/Umm-al-Qura alternative — a notable gap for an MBRHE government context.

### UX-003 — Hardcoded English risk-matrix legend on the risk wizard
- **Source:** `Views/EnterpriseRisks/Create.cshtml:162-165` · **Severity:** High · **Nielsen:** minor · **Standard:** H4
- `Low (1-4)`…`Critical (15-25)` raw English even on Arabic pages, while `Index.cshtml:168-173` correctly uses `Shared["Label_*"]`. Keys already exist.

### UX-004 — Dynamic JS-injected content not announced to screen readers
- **Source:** `_Layout.cshtml` quick-search `#quickSearchResults`, AI chat `#spAiChatBody`, `#notifPreviewList`, toasts; only 23 `aria-live`/`role=alert` across 12 of ~120 views · **Severity:** High · **Nielsen:** major · **Standard:** WCAG 4.1.3 / TDRA
- Search results, "Thinking…", new notifications, validation toasts inserted via `innerHTML` with no live region.

### UX-005 — "Forgot password" is a dead link
- **Source:** `Views/Account/Login.cshtml:86` (`href="#"`) · **Severity:** High · **Nielsen:** major · **Standard:** H1/H9
- Prominent control on the entry screen that does nothing. Wire a reset flow or remove it.

### UX-006 — Login bypasses the global SetLanguage/PathBase logic with a fragile reload
- **Source:** `Views/Account/Login.cshtml:141-153` · **Severity:** High · **Nielsen:** minor · **Standard:** H4
- Login's local `changeLanguage` posts raw `window.location.pathname` (includes `/App` under sub-app hosting) then reloads — can double-prefix PathBase on `ejraa360.com/App`. Reuse the shared `SetLanguage` helper.

### UX-007 — English-only strings leak on the Login screen
- **Source:** `Views/Account/Login.cshtml:131` (footer tagline) · **Severity:** High · **Nielsen:** minor · **Standard:** bilingual parity
- The product tagline/footer never localizes — a half-translated entry card for Arabic users.

### DYN-001 — Pure role-group users have zero working permissions (locked out everywhere)
- **Source:** observed live; `Controllers/AccountController.cs:113-144` (role-group→permission load with LegacyRoleBridge fallback only for legacy roles); seed `Program.cs:1866-1934` + role-group Code tagging `:686-723`
- **Role(s):** process-owner, quality-officer, improvement-analyst, risk-manager (any user with a role-group but no legacy CustomRole)
- **Severity:** High (fail-closed, so safe — but a shipped feature is non-functional) · **Nielsen:** major
- **Steps (reproduced live):** Log in as `procowner`/`ProcOw123` or `analyst`/`Analys123` → lands on `/Account/AccessDenied`; `/Processes`, `/Improvements`, `/Improvements/Wizard` all blocked despite the Process Owner / Improvement Analyst groups granting those exact permissions. By contrast `editor` (legacy EDITOR) reaches all Create pages.
- **Expected vs Actual:** role-group permissions should load at login; actual the permission set is empty and, with no legacy role, the `LegacyRoleBridge` fallback yields nothing → total denial. Likely cause: the role-group `Code` is set by an UPDATE that runs before the rows are INSERTed (`Program.cs:687` vs `:701`, and the INSERT omits `Code`), so `UserRoleGroups` assignment via `WHERE Code='process-owner'` matches nothing on first boot and is never retried. **Verify `RoleGroups.Code` is populated and `UserRoleGroups` rows exist.** Blocks dynamic verification of SEC-001 and all scope-based RBAC.

### DYN-002 — Post-login landing page (Dashboard) requires `Reports.View`; users without it hit AccessDenied at login
- **Source:** `Controllers/AccountController.cs:374-381` (`RedirectToLocal`→Dashboard); `Controllers/DashboardController.cs` (`[Authorize(Policy = Reports.View)]`)
- **Role(s):** any role lacking `Reports.View` — e.g. the Process Owner role-group (its permission set has no `Reports.View`)
- **Severity:** High · **Nielsen:** major (H1/H5)
- **Steps:** Log in as a user without `Reports.View` → immediately redirected to `/Account/AccessDenied`. (Currently also masked by DYN-001; a *working* process-owner would still hit this by design.)
- **Expected vs Actual:** login should land on a page the user can see (e.g. MySpace) or the home should be permission-aware; actual every user is sent to a Reports-gated dashboard.

---

## Findings — Medium

### SEC-008 — `ProcessesController` BPMN/version/service-link helpers lack scope checks
`ProcessesController.cs:507,540` (version GETs, no per-record policy), `:305,414,577,1156,1207,1237` (Edit-gated link mutators) operate on a processId with no `scope.CanAccess`. Same root cause as SEC-001.

### SEC-009 — `ServicesController` link mutators + "available" GETs lack scope checks
`ServicesController.cs:811,867,897,974,1030,1060,1137,1188,1218,930,1093,1251` — all keyed by serviceId with no scope check; a scoped editor can rewire another unit's service relationships.

### SEC-010 — Notification mark-as-read has no ownership check
`Api/NotificationsController.cs:96-101` — `MarkAsRead(id)` with no check the notification belongs to the caller; cross-user write by GUID enumeration. (`ApproveFromNotification`/`MarkAllAsRead` are correctly scoped.)

### SEC-011 — `ProcessHierarchy`/`Help`/`MySpace` (MVC) gated by bare `[Authorize]`
`ProcessHierarchyController.cs:15` (mitigated — re-decorates actions with `OrganizationUnit.View`), `HelpController.cs:11` (low-risk static), `MySpaceController.cs:11` (API enforces ownership). Flagged for least-privilege consistency.

### SEC-012 — Org-unit scope claim resolved by display-name match (fragile, fail-open)
`AccountController.cs:192-203`, `Services/Common/ScopingService.cs:38-45` — `OrganizationUnitId` resolved by matching legacy `UnitName` to APQC `NameEn/NameAr/Code`; on no match **no claim is emitted and scope falls back to Unscoped** (sees everything). A misconfigured scoped user silently gets full visibility; name collisions mis-map units.

### SEC-013 — `Improvement.Edit` gates KPI Library + Strategic Objectives (global reference data)
`KpiLibraryController.cs:26`, `StrategicObjectivesController.cs:30` — org-wide reference data governed by a per-unit edit policy; any improvement editor can mutate global KPI/objective definitions.

### FLOW-007 — Improvement approval is non-atomic across two `SaveChanges` (partial-failure window)
`ImprovementsController.cs:530-535` (also Reject `:566-571`, Return `:599-604`) — `ProcessActionAsync` commits workflow+notification, then a second save sets entity status; a crash between leaves workflow Approved + submitter notified but initiative still UnderReview.

### FLOW-008 — Approval notification fires before commit; rollback leaves a phantom "approved" alert
`Services/Workflow/WorkflowService.cs:218-258` — `SendAsync` awaited before the final `SaveChangesAsync()`; a concurrency throw on save still leaves the submitter notified.

### FLOW-009 — `MarkImplemented`/`Cancel`/`StartReview` on a CR never resolve the lingering WorkflowInstance
`ChangeRequestsController.cs:399-420,431-458,367-388` — terminal CR states don't close the workflow row (compounds FLOW-002; orphaned rows accumulate in the approver queue).

### FLOW-010 — `Improvements/Kanban` is unscoped (cross-unit leakage)
`ImprovementsController.cs:353-363` — unlike Index/Roadmap, `Kanban()` doesn't apply `_scopingService`; a scoped user sees all units' initiatives, with counts inconsistent vs Index.

### FLOW-011 — `UpdateStatus` (Kanban drag) bypasses the approval chain to reach Approved
`ImprovementsController.cs:371-409` + `ImprovementStatusMachine.cs:44` — the FSM allows `UnderReview→Approved`, so dragging a card to Approved sets status with no workflow, no approver, no notification. Exclude approval-gated transitions from `UpdateStatus`.

### FUNC-007 — Risk Edit recomputes score but mass-assigns the rest
`EnterpriseRisksController.cs:240-248` — `_context.Update(risk)` after recompute lets `IsActive`/`IsDeleted`/`RiskNumber`/audit be overwritten from the form. Pair-fix with FUNC-001.

### FUNC-010 — `AssetCategoriesController` inconsistent TempData keys (one guard's error never shows)
`AssetCategoriesController.cs:163` (`TempData["ErrorMessage"]`) vs `:177` (`TempData["Error"]`); layout reads `Error`, so deleting a category-with-assets bounces to Details with no visible reason. Standardize on `Error`/`Success`.

### FUNC-011 — `Api/ImportController` reads hardcoded local filesystem paths (dead in prod)
`Api/ImportController.cs:333` (`C:\Users\kalmi\...analysis_output.json`), `:779`, `:1267` — `ImportAll`/`ImportVisioDiagrams`/`ImportConvertedBpmn` depend on dev-box paths; on `C:\inetpub\ESEMS` they BadRequest "file not found".

### FUNC-012 — `ImportSingleVisioDiagram` opens an uploaded file as ZIP with no validation
`Api/ImportController.cs:908-958` — `IFormFile` → `new ZipArchive` with no extension allowlist/size limit/magic-byte check (contrast `MySpaceController`). Admin-gated, but the one unhardened upload path.

### FUNC-013 — Bilingual importers write English placeholders into Arabic fields
`Api/ImportController.cs:633,669,710-711,751-752` — Services/Systems/Partners/Projects inserted with the same string in `NameEn` and `NameAr`; records look bilingual but show English in the Arabic UI.

### FUNC-014 — Improvements generic `Edit` POST mass-assigns status/owner
`ImprovementsController.cs:323` — `_context.Update(improvement)` writes posted `Status`/`OwningUnitId`/`IsDeleted`, bypassing the state machine that the dedicated endpoints enforce.

### FUNC-015 — Duplicate-submission protection relies only on generated codes, not idempotency
Create POSTs in `IncidentsController.cs:118`, `ProblemsController.cs:98`, `CustomerFeedbackController.cs:78`, `AssetsController.cs:111`, `OrganizationUnitsController.cs:166` — double-click/retry creates two records (each gets its own number). Consider a PRG nonce or natural-key check.

### FUNC-016 — Incident/Feedback workflow actions lack scope checks
`IncidentsController.cs:238,261`; `CustomerFeedbackController.cs:172,191` — `Resolve`/`Close`/`Respond` just `FindAsync(id)` and mutate, bypassing the Details/Edit scope guard in the same controller.

### FUNC-017 — `ServiceCategories`/`AssetCategories` Create mishandles the system Code
`ServiceCategoriesController.cs:62` lets the client override the auto-code (no uniqueness check on the posted value); `AssetCategoriesController.cs:84,89` binds then overwrites it (pre-filled code silently discarded). Code should be a read-only server-managed badge.

### UX-008 — Color-only severity in risk heat-map cells
`EnterpriseRisks/Index.cshtml:152-167`, `Create.cshtml:147-160` — severity by background color only; colorblind users can't distinguish High vs Critical. **Standard:** WCAG 1.4.1. Add a text/pattern/`title` cue.

### UX-009 — `outline:none` with a weak (border-only) focus replacement on some inputs
`ChangeRequests/Create.cshtml:38` + ~38 other `:focus` rules — most pair a box-shadow ring, a handful only change border-color (low-contrast). **Standard:** WCAG 2.4.7. Standardize the ring.

### UX-010 — ~1,091 inline `@(isRtl ? "EN" : "AR")` ternaries bypass the resx pipeline
60+ views (heaviest `AI/Diagrams.cshtml` 75, `WorkloadAnalysis/Index.cshtml` 48). Not a parity bug but translations live in markup, drift, and can't be reviewed by translators. **Standard:** H4 / maintainability.

### UX-011 — No estimated completion time on multi-step wizards
`Improvements/Wizard.cshtml`, `EnterpriseRisks/Create.cshtml` — "Step N of 3" but no "~X min". **Standard:** DGEP 4th-Gen / TDRA mGov.

### UX-012 — No in-app per-page "How can we improve?" feedback channel
Global `_Layout.cshtml` — the CustomerFeedback module logs *external* feedback; no lightweight internal "rate this page" widget. **Standard:** DGEP/7-Star continuous-improvement, H10.

### UX-013 — Quadrant/status colors not legended on the Improvements list
`Improvements/Index.cshtml:56-65` — pills carry localized text (good) but the four quadrant colors are never legended. **Standard:** H6.

### UX-014 — Quick-search/AI error strings hardcoded English
`_Layout.cshtml:1472` ("Search failed"), `:2014/2019` ("Error","Connection error") — English literals even in Arabic sessions. **Standard:** H9 / bilingual parity.

### UX-015 — Action controls implemented as `href="#"` links
`AI/ProcessAnalyzer.cshtml:206`, `ProcessHierarchy/Index.cshtml:227,232` — JS-wired anchors give "link" semantics + spurious `#` history. Use `<button>`. **Standard:** H4 / a11y.

### UX-016 — Notification timestamps format in browser locale, not app culture
`_Layout.cshtml:1819` (`toLocaleString()` no locale arg) — Arabic user on an EN-locale browser sees Latin dates; `Improvements/Index.cshtml:354` correctly passes `ar-AE`. **Standard:** H2.

### UX-017 — "Clear Cache" wipes storage + hard-reloads with no confirm
`_Layout.cshtml:658` — inconsistent with the localized SweetAlert confirms used elsewhere. **Standard:** H5.

---

## Findings — Low

- **SEC-015** — Login doesn't rotate the session cookie; no `Session.Clear()` on logout (`AccountController.cs:208-211,313`; `Program.cs:227-232`). Low — authorization rides the auth cookie, not session.
- **SEC-016** — `ChangeLanguage` is `[AllowAnonymous][IgnoreAntiforgeryToken]` on `BaseController.cs:30-52` (intentional; no open-redirect — `Login` uses `Url.IsLocalUrl`). Noted for inventory.
- **SEC-017** — `[AllowAnonymous]` inventory verified intentional (Login/Logout/AccessDenied/error/status/ChangeLanguage); only `WindowsLogin` is a concern (SEC-006).
- **SEC-018** — Stale comment in `Api/ImportController.cs:1201-1205` claims an `[AllowAnonymous]` batch endpoint that doesn't exist (admin-only). Hygiene.
- **FLOW-012** — Wizard "save-and-resume" drafts are an unvalidated client-side JSON blob (`ImprovementsController.cs:1202-1232,1289-1299`); stale/renamed fields silently dropped on hydrate. In-wizard back-nav does NOT lose data (no mid-flow data-loss).
- **FLOW-013** — Dead `canReports` var (`_Layout.cshtml:111`) — no `ReportsController`, no consumer; `adminControllers:708` still lists removed "Roles".
- **FLOW-014** — Terminal-state Details + standalone Workflow Details are navigational dead-ends (`Improvements/Details.cshtml:1981-1984`); breadcrumb is the only escape.
- **FUNC-018** — `AggregatedCost`/`AggregatedDurationMinutes` persisted but never written (`Process.cs:101,106` etc.) — rollup KPIs always NULL; views conditionally render them.
- **FUNC-019** — `RiskCategory`/`FeedbackCategory` have no management controller (seed/import-only) — CRUD-completeness gap (orphaning risk is moot — no delete path).
- **FUNC-020** — `MaintenanceSchedule` has no Details; `MaintenanceRecord` no Edit/Delete (append-only, no correction path).
- **FUNC-021** — `StrategicObjectives.Retire`/`KpiLibrary` retire without an "still in use by N" heads-up (links stay valid — cosmetic).
- **FUNC-022** — `Process.Create` wizard derives Activity codes `{ProcessCode}.{order}` with no global-uniqueness retry (`ProcessesController.cs:815-829`).
- **UX-018** — Logo/flag `alt` text is generic ("Logo 1"/"Logo 2") (`_Layout.cshtml:506-524`). WCAG 1.1.1.
- **UX-019** — Two parallel double-submit guards may conflict (`_Layout.cshtml:205-228` + `:1649-1660`).
- **UX-020** — New code still ships inline `on*=` handlers relying on the CSP polyfill (`_Layout.cshtml:618,658,676`; `Wizard.cshtml`) — against the team's own rule; silently die if the polyfill is removed.
- **UX-021** — Skip-link + main + nav present, but no `banner`/`contentinfo`/`search` landmarks (`_Layout.cshtml:492,701`). WCAG 1.3.1.
- **UX-022** — RTL field pairing via CSS `order:-1` instead of logical source order (`EnterpriseRisks/Create.cshtml:98,110`; `Wizard.cshtml:181,203`) — verify keyboard order.
- **UX-023** — Footer/copyright tagline English-only across layouts.
- **DYN-003** — Permission-model mismatch: `LegacyRoleBridge` grants Viewer `Users.View`/`Settings.View`, but `/Users` & `/SettingsHub` require `CanAdmin` → Viewer blocked (verified live). Fail-secure, but the bridge advertises access the controllers don't honor.

---

## Coverage matrix (dynamic, representative)

| Page | Admin | Editor | Viewer | process-owner / analyst | Guest |
|---|---|---|---|---|---|
| /Account/Login | ok | ok | ok | ok | ok |
| / (Executive Dashboard) | ok | ok | ok | **AccessDenied (DYN-002)** | →login |
| /Improvements (list) | ok | ok | ok | **blocked (DYN-001)** | →login |
| /Improvements/Wizard (create) | ok | ok | 403-as-expected | **blocked (DYN-001)** | →login |
| /Processes (list) | ok | ok | ok | **blocked (DYN-001)** | →login |
| /Processes/Create | ok | ok | 403-as-expected | blocked | →login |
| /Processes/Details/{id} | ok | ok | ok | blocked* (DYN-001 masks SEC-001) | →login |
| /EnterpriseRisks/Create | ok | ok | 403-as-expected | blocked | →login |
| /Users | ok | (n/a) | 403-as-expected | blocked | →login |
| /AuditLogs | ok | 403-as-expected | 403-as-expected | blocked | →login |
| /BpmnImport, /Import | ok | 403-as-expected | 403-as-expected | blocked | →login |
| /SettingsHub | ok | 403-as-expected | 403-as-expected | blocked | →login |
| /AI/ProcessAnalyzer | ok | ok | ok (Ai.View) | blocked | →login |

`ok` = 200 with content · `403-as-expected` = correctly redirected to AccessDenied · `blocked` = denied (DYN-001, not by design) · `*` SEC-001 could not be exercised because no working scoped user exists.

**Admin full sweep:** 48/48 routes returned 200 with no server errors, no console errors, no failed requests. Legitimate redirects: `/Tasks`→`/Processes`, `/Activities`→`/Processes`, `/Workflow`→`/Workflow/PendingApprovals`, `/Maintenance`→`/Maintenance/Schedules`, `/RoleGroups`→`/SettingsHub?tab=roles`.

---

## Arabic ↔ English parity

**Strong overall** — resx key parity is exemplary (2306 EN = 2306 AR keys, 0 asymmetry; only 6 AR values are legitimately language-neutral). RTL verified live: `<html lang="ar" dir="rtl">`, logical CSS (`padding-inline-*`, `inset-inline`), mirrored chevrons, full DataTables AR pack. Gaps:

- Server validation messages render **English on Arabic pages** — 71 hardcoded `ErrorMessage` literals (UX-001, **Critical**).
- Risk-matrix legend hardcoded English on the wizard (UX-003).
- Login footer/tagline English-only (UX-007); JS error strings English-only (UX-014).
- `type="date"` is Gregorian-only — no Hijri (UX-002).
- Notification timestamps use browser locale, not app culture (UX-016).
- ~1,091 inline EN/AR ternaries bypass resx — maintainability/drift risk (UX-010).

---

## DGEP / TDRA / UAE 7-Star deviations

- **Bilingual parity break** on server validation (UX-001) — first-class Arabic is mandatory.
- **No Hijri calendar** option on dated forms (UX-002).
- **No estimated completion time** on multi-step services (UX-011).
- **No in-app page-level improvement feedback** channel (UX-012) — DGEP customer-experience pillar.
- **Accessibility:** dynamic content not announced (UX-004, WCAG 4.1.3), color-only risk severity (UX-008, 1.4.1), weak focus indicators (UX-009, 2.4.7), thin landmarks (UX-021). SLA *is* captured on service forms (verified) — good.
- **Status tracking** after submission exists for Improvements (state machine) but the CR workflow desync (FLOW-002) breaks the transparency promise for change requests.

---

## Methodology

- **Mode:** dynamic + static. App run via preview tooling (`dotnet run`, `http` profile, Development) at `http://localhost:5297` against the local dev SQL DB (production is the remote EC2 host — not touched).
- **Static:** 4 specialist sub-agents read the full controller/view/model/service/migration set — (1) RBAC & injection/XSS, (2) coverage/paths/workflow state-machines, (3) functions & data-integrity, (4) UX/usability/i18n. `.claude/worktrees/**` excluded.
- **Dynamic:** logged in as admin (48-route health sweep), viewer (read-only RBAC sweep, 26 routes), editor (legacy-role confirm), procowner + analyst (role-group personas — found non-functional), Guest (unauthenticated bounce). Verified logout/session clearing, anti-forgery on login/logout, Arabic/RTL rendering. Evidence via HTTP probes through the authenticated session, accessibility snapshots, console + network logs.
- **Roles tested:** Admin ✓, Editor ✓, Viewer ✓, Guest ✓, Approver (partial).
- **Roles skipped:** process-owner / quality-officer / improvement-analyst / risk-manager — **blocked by DYN-001** (no working permissions as seeded). Recommend fixing the role-group seeding, then re-running the dynamic RBAC + IDOR (SEC-001) pass against a working scoped account.
- **Tools:** Glob/Grep/Read (static); preview MCP tools (dynamic); manual reasoning for Nielsen/DGEP scoring.
- **Limitations:** SEC-001 not reproduced live (DYN-001); SEC-006 depends on IIS config; no load/perf testing; screenshots unavailable (`UnknownVizError`) — verification done via DOM/network/HTTP evidence.

---

### Severity legend
- **Critical** — data loss, security/RBAC bypass, blocks a core task, or exposes data to the wrong audience.
- **High** — sub-task blocker, important-data leak/export break, RTL/a11y break on a key page, latent systemic risk.
- **Medium** — confusing/time-costing UX, minor calc/consistency issues, missing scope on secondary actions.
- **Low** — cosmetic, copy, hygiene, maintainability.
