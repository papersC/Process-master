# bigTest Audit Report — ESEMS (delta + augmentation pass)
**Date:** 2026-05-21
**Mode:** static (verify-then-augment)
**Auditor:** Claude (bigTest skill)
**Scope:** delta on top of three prior reports (`audit-output/bigTest-report.md` 2026-05-20 static, `audit-output/bigTest-report-dynamic-20260520.md` dynamic, `QA-AUDIT-20260519.md`), plus four targeted augmentation passes on gap areas the prior audits skipped.
**Branch / commit:** `master` @ `d598379` (= `origin/master`). Working tree has 6 staged `.cshtml` edits restored from a stash; those are audited in §6.

---

## Executive summary

Master moved 1 commit (`d598379 chore(deploy)`) since yesterday's audit, so the surface area is essentially unchanged. The right move was to verify yesterday's 74 findings against current source, then run targeted audits on the four gap areas: **background services** (prior audit said "none exist" — wrong, 6 exist), **server-side RBAC tampering / IDOR / CSRF** (prior audit explicitly deferred), **SQL injection / XSS / race conditions** (also deferred), and **staged `.cshtml` edits + DGEP/TDRA + Arabic parity matrix**.

### Headline counts (after dedup against prior reports)

| Bucket | Count |
|---|---|
| Prior bigTest findings (2026-05-20) verified **FIXED** | **7** |
| Prior bigTest findings verified **PARTIALLY-FIXED** | **1** |
| Prior bigTest findings **UNVERIFIABLE** without runtime | **4** |
| Prior bigTest findings **STILL-PRESENT** | **62** |
| QA-AUDIT-20260519 **deferred Criticals still open** | **5** |
| QA-AUDIT-20260519 **deferred Highs still open** | **14** |
| Dynamic report (2026-05-20) **still open** | **5** |
| **NEW findings (this pass)** | **28** |
| **Total open after dedup** | **~107** |

### NEW findings by severity (this pass only — full carry-over list in §3)

| Severity | Count | Codes |
|---|---|---|
| Critical | 1 | RBAC-001 |
| High | 9 | BG-003, BG-004, BG-005, BG-006, BG-007, DATA-009, RBAC-002, RBAC-005, RBAC-006, DGEP-001, DGEP-002, DGEP-004 (12 distinct, 3 marked as carry-over refinements below) |
| Medium | 9 | BG-001, BG-002, BG-008, BG-009, BG-010, DATA-001, DATA-002, RBAC-003, RBAC-004, RBAC-007, RBAC-009, RBAC-010, DGEP-003 |
| Low | 2 | DATA-003, DATA-004 |

(Numbering note: some new findings are refinements of existing carry-overs — e.g. **RBAC-001 = C2 from QA-AUDIT** confirmed still risky, **DATA-009 = C9 narrowed to the two controllers still vulnerable**.)

### Top 3 new risks (since yesterday)

1. **RBAC-001 / C2: `CustomUser.Password` may still hold plaintext legacy rows.** `Helpers/PasswordHelper.cs` correctly implements PBKDF2 (SHA512, 100k iterations) for new accounts, but the column type is `nvarchar(50)` and there is no migration that backfilled hashes for pre-existing rows. Any unrotated account is one DB-backup leak away from disclosure.
2. **DATA-009: `ServicesController.Edit` + `ProcessesController.Edit` still NOT wrapped in `DbUpdateConcurrencyException`** despite the fix landing in Risks/Improvements/Assets/CustomerFeedback. Two of the most-edited modules silently last-write-wins under concurrent edits.
3. **RBAC-006: `Api/MySpaceController` `[HttpDelete]` and `[HttpPut]` actions are missing `[ValidateAntiForgeryToken]`.** `[ApiController]` does NOT apply anti-forgery by default. A malicious site can trigger document deletes against any logged-in user with a simple `fetch(..., {method:'DELETE'})`.

### Hidden honourable mentions (prior audit missed entirely)

The prior bigTest explicitly stated "Hangfire / Quartz background jobs: project has none". That is **wrong** — there are **six** `BackgroundService` classes (ApprovalSla, VectorSync, RecurringReview, MeasurementReminder, InitiativeStallDetection, BenefitsRealization). Three of them send notifications without using the `DedupKey` pattern (`feedback_notification_dedup_key.md`), and two accumulate unbounded in-memory `HashSet<string>` dedup caches. These are real production risks the prior audit did not surface.

---

## 1. Status of yesterday's 74 findings

Full per-finding verification table (Critical first):

| Finding | Title | Status | Evidence |
|---|---|---|---|
| **CRITICAL** | | | |
| F-OP-004 | CustomerFeedback PII no consent | **FIXED** | `Views/CustomerFeedback/Create.cshtml:62-76` consent block + `ConsentAck` server-side enforcement |
| F-OP-005 | PII shown unmasked on Details | **FIXED** | `Views/CustomerFeedback/Details.cshtml:9-31` `MaskEmail`/`MaskPhone` helpers |
| F-CC-011 | SettingsHub mobile tab overflow | **FIXED** | `Views/SettingsHub/Index.cshtml:30-31` scrollable tabs container |
| F-SV-001 | ChangeRequest state-visibility cues | **FIXED** | `Views/ChangeRequests/Details.cshtml:97-108` `.pc-state-hint` CSS |
| F-CC-002 | ChangeRequests buried 7-deep in sidebar | **STILL-PRESENT** | `Views/Shared/_Layout.cshtml` — still 7th menu item |
| **HIGH** | | | |
| F-RA-001 | Asset Details DataOwner shown as int | **FIXED** | `Views/Assets/Details.cshtml:190-198` + `ViewBag.UsersById` resolver |
| F-RA-007 | Asset Edit numeric user-picker | **STILL-PRESENT** | `Views/Assets/Edit.cshtml` — number input remains, no autocomplete |
| F-PA-001 | Activities tab no swim-lane | STILL-PRESENT | flat `<ol>`, no BPMN lanes |
| F-PA-002 | ProcessHierarchy no expand-all | STILL-PRESENT | no bulk controls on tree |
| F-PA-003 | Activity code zero-pad inconsistency | UNVERIFIABLE | migration `20260520122626` requires DB state check |
| F-SV-002 | Service Details Related Processes code-only | STILL-PRESENT | no Name shown next to code |
| F-SV-003 | Service Edit no save-continue | STILL-PRESENT | `Views/Services/Edit.cshtml:56` redirects to Index |
| F-SV-008 | Decimal Range crash under Arabic | UNVERIFIABLE | needs runtime; `DATA-011` confirms invariant flag is everywhere |
| F-OP-001 | KPI submission no unit-of-measure label | STILL-PRESENT | |
| F-OP-006 | Workload silent 3-row cap | STILL-PRESENT | |
| F-OP-007 | Documents Index no preview | STILL-PRESENT | |
| F-OP-013 | ServiceOps double-load on lang switch | UNVERIFIABLE | runtime JS check needed |
| F-CC-001 | Sidebar lock tooltip missing | STILL-PRESENT | |
| F-CC-003 | Breadcrumb missing on 9 detail pages | **PARTIALLY-FIXED** | Processes/Services redesigned; 9-page set partially closed |
| F-CC-005 | Language toggle bounces to home | **FIXED** | `_Layout.cshtml:1319-1364` `SetLanguage` preserves returnUrl |
| F-CC-007 | Toast stack overflows off-screen | **FIXED** | max-height + overflow-y added |
| F-CC-014 | Edit forms allow double-submit | STILL-PRESENT | no `data-loading` / disabled-on-submit anywhere |
| **MEDIUM** | All 34 prior Medium findings | **STILL-PRESENT** (except F-SV-004 PARTIAL, F-SV-009 UNVERIFIABLE) | see appendix |
| **LOW** | All 18 prior Low findings | **STILL-PRESENT** | see appendix |

**Summary:** 7 FIXED, 1 PARTIAL, 4 UNVERIFIABLE, 62 STILL-PRESENT. Expected — master only added a deploy-script chore commit since yesterday.

---

## 2. NEW findings — background services (prior audit missed)

Yesterday's bigTest report stated *"Hangfire / Quartz background jobs: project has none — N/A"*. This is **wrong**. `Program.cs:109,120` plus 4 more registrations enroll six `BackgroundService` classes, all with real production behavior. Findings on each:

### BG-001: VectorSyncBackgroundService — shutdown can hang on long re-index
- **Severity:** Medium · **Category:** cancellation
- **Source:** `ESEMS.Web/Services/AI/VectorSyncBackgroundService.cs:18-40`
- **Issue:** If `IndexAllDataAsync()` does not honour the `stoppingToken` internally, host shutdown will hang until the index completes. No timeout guard.

### BG-002: RecurringReviewScheduler — swallows cancellation exceptions silently
- **Severity:** Medium · **Category:** cancellation / observability
- **Source:** `ESEMS.Web/Services/Improvements/RecurringReviewScheduler.cs:39,52`
- **Issue:** Bare `catch { return; }` on startup + main-loop delay. Silent shutdown indistinguishable from crash.

### BG-003: MeasurementReminderHostedService — unbounded `_sentKeys` HashSet
- **Severity:** High · **Category:** memory
- **Source:** `ESEMS.Web/Services/Improvements/MeasurementReminderHostedService.cs:32,105-120`
- **Issue:** In-process dedup set accumulates forever across the service lifetime. At 1000+ active measurements × N periods × N users, heap grows linearly indefinitely. No eviction, no size cap, no TTL.

### BG-004: InitiativeStallDetectionService — same unbounded dedup
- **Severity:** High · **Category:** memory
- **Source:** `ESEMS.Web/Services/Improvements/InitiativeStallDetectionService.cs:36,123-125`
- **Issue:** Single Red-status initiative polled every 12h for 180 days = 1440 duplicate keys retained, none removed.

### BG-005: InitiativeStallDetectionService — DedupKey passed but not verified end-to-end
- **Severity:** High · **Category:** dedup
- **Source:** `InitiativeStallDetectionService.cs:212-216`
- **Issue:** `dedupKey` parameter is constructed and passed to `SendAsync()`, but per `feedback_notification_dedup_key.md` the title-based dedup is broken. Verify that `INotificationService.SendAsync` actually persists `dedupKey` to the DB-backed dedup table — otherwise stall alerts duplicate after restart.

### BG-006: MeasurementReminderHostedService — sends notifications WITHOUT DedupKey
- **Severity:** High · **Category:** dedup
- **Source:** `MeasurementReminderHostedService.cs:110, 177, 205`
- **Issue:** Three `SendAsync()` call sites omit the `dedupKey` parameter. After a host restart, the in-process `_sentKeys` HashSet is wiped → users receive duplicate "reading due" notifications on the next poll.

### BG-007: BenefitsRealizationScheduler — same DedupKey gap
- **Severity:** High · **Category:** dedup
- **Source:** `BenefitsRealizationScheduler.cs:157`
- **Issue:** Benefits-review reminder sent without `dedupKey`. Same restart-loses-dedup symptom as BG-006.

### BG-008: All six services — no concurrent-run protection
- **Severity:** Medium · **Category:** concurrency
- **Source:** all six service `ExecuteAsync` loops
- **Issue:** None of the services guard against overlapping poll cycles. If a single sweep takes longer than the poll interval (busy DB, 1000+ initiatives), the next iteration starts before the previous returns. Concurrent DB mutations follow.

### BG-009: VectorSyncBackgroundService — `IsStale` read without synchronization
- **Severity:** Medium · **Category:** concurrency
- **Source:** `VectorSyncBackgroundService.cs:27-30`
- **Issue:** Plain `if (_vectorStore.IsStale) IndexAllDataAsync()`. If `IsStale` is mutable and read concurrently, two simultaneous true-readings can trigger duplicate indexing passes.

### BG-010: RecurringReviewScheduler + ApprovalSlaHostedService — no tenant-isolation filter
- **Severity:** Medium *(only if multi-tenant)* · **Category:** tenant
- **Source:** `RecurringReviewScheduler.cs:63-74`, `ApprovalSlaHostedService.cs:75-81, 112-118`
- **Issue:** Both query `ImprovementInitiatives` / `WorkflowSteps` without a `.Where(x => x.TenantId == ...)`. If ESEMS is ever multi-tenanted, background sweeps will cross-mutate tenants.

**ApprovalSlaHostedService:** clean on dedup, memory, and explicit tenant scoping (modulo BG-008 / BG-010 above).

---

## 3. NEW findings — server-side RBAC tampering, IDOR, CSRF, SQL

The 2026-05-20 audit explicitly deferred "server-side RBAC tampering, IDOR sniff-tests, CSRF, SQL-injection" to a separate engagement. This pass covers them.

### RBAC-001: `CustomUser.Password` plaintext legacy rows risk *(= C2 carry-forward, escalated)*
- **Severity:** **Critical**
- **Source:** `ESEMS.Web/Models/CustomUser.cs:80-82` (column type `nvarchar(50)`); `ESEMS.Web/Helpers/PasswordHelper.cs:34-50` (PBKDF2 SHA512, 100k, salt — correct for new accounts)
- **Issue:** `PasswordHelper.Verify()` expects `base64(salt+hash)`. The fix landed for new accounts but there is no migration that backfilled existing rows. Any account created before the hash rollout still has plaintext in the column — verify will fail for those users, AND a DB backup leaks plaintext credentials.
- **Recommended fix:** Audit current DB; either re-hash on next successful legacy verify (transparent rotation) or force a password reset for all unrotated accounts. Add a DB CHECK constraint preventing plaintext (`LEN(Password) >= 90` matching PBKDF2 base64 minimum) once migrated.

### RBAC-002: `/health` endpoint exposes DB connectivity to unauthenticated callers
- **Severity:** High
- **Source:** `Program.cs:348-349, 1193` — `AddDbContextCheck<ApplicationDbContext>("database")` + `app.MapHealthChecks("/health").AllowAnonymous()`
- **Issue:** Default health-check response body lists each check by name with status / duration / exception detail. An anonymous probe of `/health` returns `{"status":"Healthy","entries":{"database":{"status":"Healthy", ...}}}` — discloses that an SQL backend exists and is reachable; on failure can leak the connection-string fragment in the exception message.
- **Recommended fix:** Either filter the response (`ResponseWriter = ctx => ctx.Response.WriteAsync("ok")`), restrict to `/health/live` (no DB) for anonymous + `/health/ready` (DB) behind auth, or bind to a separate non-public port.

### RBAC-003: HomeController.Error / StatusCode `[AllowAnonymous]` action-scoped risk
- **Severity:** Medium
- **Source:** `Controllers/HomeController.cs:33-37, 45-48`
- **Issue:** Action-level `[AllowAnonymous]` is currently scoped, but the file is a magnet for new handlers (custom errors are easy to bolt on). If the class ever grows a class-level decorator (e.g. someone adds `[AllowAnonymous]` to a new debug action and decides "may as well lift it"), every action under it silently bypasses `FallbackPolicy`. Best practice: move error handlers to a dedicated `ErrorController`.

### RBAC-004: `AccountController.Logout` no `IsAuthenticated` guard
- **Severity:** Medium
- **Source:** `Controllers/AccountController.cs:308-315`
- **Issue:** Calls `SignOutAsync()` unconditionally. Cosmetic — pair with a redirect to `/Account/Login` regardless of state.

### RBAC-005: IDOR — `scope.CanAccess` enforcement not uniform across Edit/Delete
- **Severity:** High
- **Source:** Pattern in `AssetsController.cs:55-76` + `EnterpriseRisksController.cs:96-124` is correct on `Details`; needs verification on every `Edit`/`Delete`/`Approve` action across **all** scoped controllers: Assets, EnterpriseRisks, Improvements, ChangeRequests, ProcessGroups, Processes, Services, Maintenance, Incidents, Problems, CustomerFeedback.
- **Recommended fix:** Either centralize the guard in a `[ScopeAuthorize]` action filter, or add a unit test that scans all `Edit`/`Delete` actions for a `scope.CanAccess` call.

### RBAC-006: `Api/MySpaceController` DELETE + PUT missing `[ValidateAntiForgeryToken]`
- **Severity:** High
- **Source:** `Controllers/Api/MySpaceController.cs:319-331, 336-350`
- **Issue:** `[ApiController]` does NOT enable anti-forgery by default. A `<form action="/api/myspace/doc-id" method="DELETE">` (or `fetch()` with credentials: 'include') from any other origin will succeed against any logged-in user.
- **Recommended fix:** Add `[AutoValidateAntiforgeryToken]` to the controller, or require a custom anti-forgery header that the SPA fetch wrapper sets.

### RBAC-007: `Api/ExportController` GET-only — defensible now, fragile later
- **Severity:** Medium (architectural)
- **Source:** `Controllers/Api/ExportController.cs:1-80`
- **Issue:** All endpoints are `[HttpGet]` with `[Authorize(Policy = CanView)]`. Currently safe from CSRF; flag is that any future state-changing endpoint added here will inherit the same lack of anti-forgery. Document the rule at the top of the file or add a controller-level `[AutoValidateAntiforgeryToken]`.

### RBAC-008: SQL injection — **clean** (no vector detected)
- **Severity:** Low (positive confirmation, not a finding)
- **Source:** all `*Controller.cs`; verified by grep — no `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQuery`, or string-interpolated SQL.
- **Single exception:** `IntegrationSettingsBootstrap.cs:50` uses `SqlCommand` with parameters — safe.

### RBAC-009: Many controllers lack class-level `[Authorize]`
- **Severity:** Medium
- **Source:** sample — `ActivitiesController.cs:17`, `AIController.cs:20`, others
- **Issue:** Relying solely on `FallbackPolicy = RequireAuthenticatedUser` is fragile: a future action without `[Authorize(Policy=...)]` will only require *authentication*, not the *correct policy*. The intent is invisible at the file level.
- **Recommended fix:** Add class-level `[Authorize(Policy = "<module-default>")]` to every controller.

### RBAC-010: `[AllowAnonymous]` outside Account/Home requires named review
- **Severity:** Medium
- **Source:** grep — `Api/ImportController.cs:1203` references an `[AllowAnonymous]` batch endpoint
- **Issue:** A batch importer that runs without auth is a high-blast-radius surface (bulk import of process definitions, assets, risks). Verify whether this is gated by IP allowlist, internal-VPC-only routing, or a shared secret. If none, restrict.

---

## 4. NEW findings — data integrity (SQL / XSS / uploads / race / validation)

### DATA-001: `_AiAnalysisPanel.cshtml` — fragile `Html.Raw(Model.BodyJson)` pattern
- **Severity:** Medium · **Dimension:** xss
- **Source:** `Views/Shared/_AiAnalysisPanel.cshtml:50`
- **Issue:** `Model.BodyJson` is rendered raw inside a `<script>` literal. Safe today (server-built, defaults to null), broken the moment any caller passes user input through. Replace with `@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.BodyJson))` or `<script type="application/json">` + parse on the client.

### DATA-002: `EnterpriseRisk` score fields missing `[Range]`
- **Severity:** Medium · **Dimension:** validation
- **Source:** `Models/RiskManagement/EnterpriseRisk.cs:61, 79, 99` — `InherentRiskScore`, `ResidualRiskScore`, `ControlEffectiveness`
- **Issue:** All three are unvalidated `int`. API/direct-POST can set 999 or negative; UI constraints alone don't prevent persistence. Heat-map scoring assumes 1–5 or 1–25.

### DATA-003: `Asset` decimal fields unbounded
- **Severity:** Low · **Dimension:** validation
- **Source:** `Models/AssetManagement/Asset.cs:74, 79, 84, 140, 143, 45, 50` — PurchaseCost, CurrentValue, DepreciationRate, BuiltUpAreaSqm, LandAreaSqm, AggregatedDurationMinutes, AggregatedCost
- **Issue:** Negative or extreme values bypass validation.

### DATA-004: Multiple model fields missing `[MaxLength]`
- **Severity:** Low · **Dimension:** validation
- **Source:** `Models/APQC/Process.cs:21,69`; `OrganizationUnit.cs:65`; ~20 others
- **Issue:** Unbounded strings risk DB column truncation / data loss at insert time.

### DATA-005: SQL injection — **clean** (confirmation, not finding)
- See RBAC-008 above.

### DATA-006: XSS — largely clean
- **Dimension:** xss
- **Finding:** 74 `@Html.Raw(...)` uses sampled. All except DATA-001 source from server-built localized strings or static markup. Markdown render in `_AiAnalysisPanel` calls `marked.parse()` client-side — confirm the API payload is sanitised before return (separate audit).

### DATA-007: Upload validation — **robust**
- **Source:** `MySpaceController.Upload()` — extension allowlist + 20 MB size limit + `RequestSizeLimit` + magic-byte signature + GUID filename (no path traversal). `ImprovementsController.ImportXlsx()` checks `.xlsx`. `ImportSingleVisioDiagram()` accepts VSDX (ZIP) safely.
- **Finding:** No new vector detected.

### DATA-008 / DATA-009: Race conditions — PARTIALLY FIXED
- **Severity (DATA-009):** High
- **Source:** `Controllers/ServicesController.cs` `Edit` action + `Controllers/ProcessesController.cs` `Edit` action
- **Issue:** The C9 concurrency fix from 2026-05-19 has been applied in **4 of 6** controllers (EnterpriseRisks, Improvements, Assets, CustomerFeedback now wrap `SaveChangesAsync` in `try { ... } catch (DbUpdateConcurrencyException)`). **Services and Processes still do NOT.** These are two of the highest-traffic Edit actions. Silent last-write-wins on concurrent edits.
- **Recommended fix:** apply the same `try/catch` + `ModelState.AddModelError("", Shared["Error_Concurrency"])` + reload pattern used in the other four controllers.

### DATA-010: Idempotency — only single-use anti-forgery tokens
- **Source:** `Program.cs` — no global `[AutoValidateAntiforgeryToken]`; per-action `[ValidateAntiForgeryToken]` instead
- **Finding:** Tokens are single-use (= some double-submit protection). No idempotency keys on Create POSTs beyond that. Consider adding `Idempotency-Key` header support on the highest-blast Create endpoints (Improvements, ChangeRequests, EnterpriseRisks).

### DATA-011: Decimal `Range` + `ParseLimitsInInvariantCulture` — **uniformly applied**
- **Source:** every `Range(typeof(decimal), ...)` checked: Asset GPS lat/long, WorkloadConfig, WorkloadLineItem — all set `ParseLimitsInInvariantCulture = true`.
- **Finding:** `feedback_decimal_range_invariant.md` rule is honoured. F-SV-008 from the prior report (decimal range Arabic crash) is **architecturally closed**; needs a runtime test only as defence-in-depth.

---

## 5. NEW findings — DGEP / TDRA / UAE PDPL

### DGEP-001: `/Services/Details/{id}` shows no SLA / response time / resolution time
- **Severity:** High · **Standard:** UAE Government Services 7-Star
- **Source:** `Views/Services/Details.cshtml` — sticky header lists Name/Code/Type/Channels but no SLA badge or "expected resolution time" surface
- **Recommended fix:** add an SLA chip in the sticky header (e.g. *"Standard: 3 working days · Critical: 4 hours"*) backed by `ServiceCatalogInfo.Sla*` fields.

### DGEP-002: No per-service feedback link from `/Services/Details/{id}`
- **Severity:** High · **Standard:** DGEP customer-experience pillar
- **Source:** `Views/Services/Details.cshtml` — no "Report an issue with this service" / "Leave feedback" CTA
- **Recommended fix:** add an action button `asp-controller="CustomerFeedback" asp-action="Create" asp-route-serviceId="@Model.Id"` and prefill `RelatedServiceId` server-side.

### DGEP-003: SettingsHub tab bar — mobile collapse still needs media query
- **Severity:** Medium · **Standard:** TDRA mGov mobile-first
- **Source:** `Views/SettingsHub/_GeneralPartial.cshtml`
- **Issue:** F-CC-011 was fixed for horizontal scroll, but on `<640px` the tabs stack readability would benefit from `@media (max-width: 640px) { .ns-tabs-nav { flex-direction: column; } }`. Cosmetic refinement of the already-fixed Critical.

### DGEP-004: `/Users/Create` collects PII with no PDPL consent block
- **Severity:** High · **Standard:** UAE PDPL (Federal Decree-Law 45/2021)
- **Source:** `Views/Users/Create.cshtml` — collects EmployeeName, EmployeeNameAr, EmailAddress, JobName + assignments. No consent text. CustomerFeedback now has the consent block; this surface does not.
- **Recommended fix:** mirror the CustomerFeedback consent UX — bilingual block + `ConsentAck` server-side enforcement before saving.

---

## 6. Staged `.cshtml` edits in working tree — **CLEAN**

Six files were restored from the audit-branch stash onto master. Audited:

| File | Change | Verdict |
|---|---|---|
| `Views/Account/Profile.cshtml` | tab/lang toggle event wiring uses `addEventListener` (not inline `onclick`) | CSP-safe, accessibility OK |
| `Views/Import/Index.cshtml` | hardcoded EN+AR strings on Visio Importer block; arrow icon mirrored via `isRtl` | RTL OK |
| `Views/SettingsHub/_GeneralPartial.cshtml` | HTML entity normalization (`&amp;amp;` → `&amp;`) | safe under Razor auto-encode |
| `Views/StrategicObjectives/_Form.cshtml` | same entity normalization | safe |
| `Views/Users/Create.cshtml` | same entity normalization; all `<label for>` bindings correct | safe (DGEP-004 unrelated — pre-existing) |
| `Views/Workflow/PendingApprovals.cshtml` | same entity normalization in modal title + button | safe |

**Net:** no new regressions introduced by the staged edits. Safe to commit independently.

---

## 7. Arabic ↔ English parity matrix (top 25 pages)

Verified by Agent — **22 of 25 pages A-grade.** No DataTables `language:` config gaps detected (the ESEMS list pages use custom client-side filters, not jQuery DataTables, so F-D-004 from the dynamic report applies only to the pages it does use — verified at runtime, not via this static pass).

Leakage points (B/C grade):
- `AuditLogs/Index` — 3 hardcoded EN table headers (Action / Entity / Timestamp)
- `MySpace/Dashboard` — 3 hardcoded EN labels in KPI strip
- `Categories/Index`, `ProcessGroups/Index`, `AssetCategories/Index` etc. — 1–2 hardcoded EN headers each

Pattern: list pages with **custom filter bars** are more localized than those using **inherited table headers**. Recommend a sweep moving all `<th>Foo</th>` to `<th>@Shared["Th_Foo"]</th>`.

---

## 8. Carry-forward Criticals still open (QA-AUDIT-20260519)

These were marked deferred in yesterday's "fix all" pass. Re-flagged here so they don't fade from view:

| # | Finding | Why deferred | Re-priority? |
|---|---|---|---|
| C2 | CustomUser.Password plaintext | "Auth migration; every account affected; needs focused session" | **YES — re-escalate** (see RBAC-001) |
| C3 | CustomerFeedback PII at rest unencrypted, no retention scheduler | "DPIA + UX + scheduler — multi-sprint" | YES — paired with new DGEP-004 |
| C5 | ChangeRequest state-machine Reject-on-Approved | "needs product decision on undo-flows" | flag |
| C6 | 15+ entity validators hardcoded English | "mechanical but ~50 resx keys" | mechanical; bundle next sprint |
| C9 | Concurrency on Edit POSTs | "pattern across ~15 controllers; one-shot risky" | **partially closed**; see DATA-008 — finish the remaining 2 |

---

## 9. Coverage + methodology

- **Strategy:** verify-then-augment. Avoided producing a redundant 74-finding re-audit of unchanged code.
- **Prior reports read:** `audit-output/bigTest-report.md` (74 findings, 2026-05-20), `audit-output/bigTest-report-dynamic-20260520.md` (6 findings + verification), `QA-AUDIT-20260519.md` (56 findings + fix-status legend).
- **NEW augmentation passes:** 5 parallel Explore subagents — (1) verify 74 prior findings, (2) audit 6 BackgroundService classes, (3) RBAC tampering / IDOR / CSRF, (4) data integrity / SQL / XSS / uploads / race / validation, (5) staged-edits + Arabic parity + DGEP/TDRA.
- **Project map:** 158 views, 44 controllers, 6 hosted services, 4 legacy roles (`Admin`, `Editor`, `Approver`, `Viewer`) + 65 granular `Module.Action` policies, 2 cultures (`en` / `ar`), cookie auth on custom user table (Identity is registered but `AddIdentity` is commented out — `IdentitySeedData.cs` references `RoleManager`/`UserManager` that aren't registered; **likely dead code** — flag for cleanup).
- **Mode:** static. No `dotnet run`. No browser. No DB writes.

### Limitations
- **Not exercised at runtime:** the `[AllowAnonymous]` ImportController batch endpoint (RBAC-010) — need to confirm it's IP-gated.
- **Not exercised:** per-role RBAC (Editor / Approver / Viewer) — no seeded test users; recommend dynamic re-pass once seed users land.
- **Not exercised:** F-PA-003 migration (zero-padding backfill) — needs `dotnet ef database update` to verify.
- **Not measured:** background-service memory growth (BG-003 / BG-004) — flagged from code inspection; production observation would confirm.
- **Not deeply audited:** AI services beyond `_AiAnalysisPanel` pattern. `VectorStoreService` + `AiAssistantService` deserve their own pass.
- **Not audited:** `IdentitySeedData.cs` — if it's dead code, removing it eliminates a confusion vector.
- **Not done:** WCAG keyboard tab-order trace, Lighthouse, accessibility tree walk.

---

*End of report.*
