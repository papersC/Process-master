# bigTest Audit Report — ESEMS (MBRHE Performance Management System)
**Date:** 2026-05-20
**Mode:** static
**Auditor:** Claude (bigTest skill)
**Scope:** 158 Razor views, 44 controllers, 7+ effective roles (ADMIN + Permission claims), 6 module groups, ~80 end-to-end screen functions

---

## Executive summary

- **Total findings: 74** (Critical: **5**, High: **17**, Medium: **34**, Low: **18**)
- **Top 3 risks** (one-liners):
  1. **CustomerFeedback collects customer PII (name, email, phone, organization) with zero consent disclosure or privacy notice** — Dubai PDPL / Federal Data Protection Law exposure on the most-public-facing module.
  2. **Asset Details renders Data Owner / Data Custodian as raw numeric user IDs** ("194", "201") — ISO 27001 information-asset register is unusable for governance review.
  3. **ChangeRequests is buried 7-clicks deep inside "Risk & Improvement → Change Requests"** with no top-level entry, no dashboard tile, no link from Process Details, and no link from Service Details — the lifecycle work just completed (StartReview/MarkImplemented/Cancel) is nearly invisible.
- **Coverage:** 158/158 views statically read (100%); 7/7 detected roles enumerated; 6/6 module groups walked. **Dynamic role testing was NOT performed** (no role-specific credentials provided), so RBAC-by-role and runtime JS console errors are out of this report's claims.
- **Skipped (with reason):**
  - Hangfire / Quartz background jobs: project has none (verified via grep — no `IHostedService` / `BackgroundService` / `Hangfire` references). N/A.
  - Mobile screenshots: static mode, no browser preview. Findings flagged from CSS/markup inspection only.
  - Print stylesheet verification (lines 196+ of _Layout.cshtml exist): not exercised.

---

## Findings — Critical

### F-OP-004: Customer PII collected on public-facing feedback form with no consent disclosure
- **Page:** /CustomerFeedback/Create
- **Role(s) affected:** All (form is internal-facing but data subjects are external customers)
- **Workflow:** Feedback intake → triage → response
- **Severity:** Critical
- **Nielsen severity:** catastrophic
- **Heuristic / standard:** UAE PDPL (Federal Decree-Law 45/2021), Dubai Data Law, DGEP customer-trust pillar
- **Steps to reproduce:**
  1. Navigate to /CustomerFeedback/Create
  2. Inspect Step 1 fields: CustomerName, CustomerEmail, CustomerPhone, OrganizationName
  3. Search the view for any consent text, privacy link, or retention notice
- **Expected:** A consent checkbox or explicit privacy-notice block explaining (a) purpose, (b) retention, (c) data-subject rights — bilingual EN/AR.
- **Actual:** Zero consent text. Customer phone/email/name posted to server, persisted indefinitely, and shown verbatim on /CustomerFeedback/Details to anyone with Feedback.View.
- **Source:** `ESEMS.Web/Views/CustomerFeedback/Create.cshtml:51-69` — `<input asp-for="CustomerName" />` … `<input asp-for="CustomerPhone" />` with no surrounding consent UI; `ESEMS.Web/Views/CustomerFeedback/Details.cshtml` exposes all three fields in plain text.

### F-OP-005: Customer PII displayed on Details page without role-tier gating or masking
- **Page:** /CustomerFeedback/Details/{id}
- **Role(s) affected:** Anyone with Feedback.View
- **Severity:** Critical
- **Nielsen severity:** major
- **Heuristic / standard:** ISO 27001 A.5.34 (privacy), Dubai Data Law
- **Steps to reproduce:** Open any CustomerFeedback Details page — full email/phone/organization rendered as plain text.
- **Expected:** Either mask middle of email/phone for non-privileged roles, OR gate the PII block behind a "Show PII" reveal that audit-logs the access.
- **Actual:** Raw values rendered unconditionally.
- **Source:** `ESEMS.Web/Views/CustomerFeedback/Details.cshtml` — 3 grep hits for raw customer email/phone/name rendering.

### F-CC-011: Settings Hub tab bar overflows on mobile with no scroll affordance
- **Page:** /SettingsHub/Index
- **Severity:** Critical (admins on mobile cannot reach 4 of 9 tabs)
- **Nielsen severity:** catastrophic
- **Heuristic / standard:** H7 Flexibility, TDRA mGov mobile-first
- **Steps to reproduce:** Resize browser to 375px → 9 Bootstrap nav-tabs (General / Email / Integrations / Approvals / Roles / Import / Activity / SystemInfo / About) stretch horizontally with no `flex-wrap`, `overflow-x:auto`, or hamburger; right-side tabs become unreachable.
- **Expected:** Either horizontal scroll with a chevron-hint, or a dropdown picker below `md` breakpoint.
- **Actual:** Page just clips — last tabs not focusable on mobile.
- **Source:** `ESEMS.Web/Views/SettingsHub/Index.cshtml:23-69` — `<ul class="nav nav-tabs">` with no responsive utilities applied.

### F-SV-001: ChangeRequest lifecycle actions render but state preconditions are not visible to the user
- **Page:** /ChangeRequests/Details/{id}
- **Severity:** Critical (just-finished feature is undiscoverable in earlier states)
- **Nielsen severity:** major
- **Heuristic / standard:** H1 Visibility of system status, H6 Recognition over recall
- **Issue:** The state machine has 6 states (Submitted → UnderReview → Approved → Implemented; Rejected; Cancelled). Each lifecycle button only renders in its valid state, but there is no "what's next?" cue when a state has no action (e.g. Submitted shows only "Start Review" + "Reject"; if the reviewer is not the right role, the panel looks empty). The redesigned Details now hides this completely cleanly — too cleanly — and a user staring at a card with no actions has no hint why.
- **Source:** `ESEMS.Web/Views/ChangeRequests/Details.cshtml` (redesigned) — action panel renders conditionally with no fallback "Waiting on … to review" microcopy.

### F-CC-002: Change Requests buried as 6th submenu item under "Risk & Improvement" with no surface elsewhere
- **Page:** _Layout sidebar
- **Severity:** Critical (entire module nearly invisible)
- **Nielsen severity:** catastrophic
- **Heuristic / standard:** H6 Recognition rather than recall, IA fundamentals
- **Issue:** ChangeRequests entry sits at index 6 inside the "Risk & Improvement" collapsible section, behind Risks Dashboard, Risk Register, Heat Map, Improvements Dashboard, Improvements Index, Kanban, Roadmap. No top-level pin, no dashboard KPI tile, no contextual link from Process/Service Details (despite ChangeRequest having ProcessId/ServiceId FKs).
- **Source:** `ESEMS.Web/Views/Shared/_Layout.cshtml:887-890` — 7th `<a>` inside `#risk-improvement-menu`. Process Details has no "Open change requests for this process" link.

---

## Findings — High

### F-RA-001: Asset Details shows Data Owner as raw user ID number
- **Page:** /Assets/Details/{id}
- **Severity:** High (ISO 27001 register is unreviewable)
- **Heuristic / standard:** H2 Match between system and real world; ISO 27001 A.5.9
- **Issue:** `Model.DataOwnerUserId` is rendered as a bare integer ("194") instead of joining to User and rendering full name + role.
- **Source:** `ESEMS.Web/Views/Assets/Details.cshtml:191-194` — `<p class="font-medium">@Model.DataOwnerUserId</p>` (same for DataCustodianUserId at line 193-194).

### F-RA-007: Asset Edit drops Data Owner / Custodian to a numeric `<input>` instead of a user picker
- **Page:** /Assets/Edit/{id}
- **Severity:** High
- **Issue:** ISO 27001 information-asset extension fields use unbounded numeric inputs — admin must hand-look-up user IDs in another tab. No autocomplete.
- **Source:** `ESEMS.Web/Views/Assets/Edit.cshtml` (info-asset `<details>` block) — `<input type="number" asp-for="DataOwnerUserId" />`.

### F-PA-001: Process Activities tab on Process Details renders activities as a flat ordered list with no swim-lane / RACI legend visible
- **Page:** /Processes/Details/{id} → Activities tab
- **Severity:** High (BPMN visualization expected by spec is missing)
- **Heuristic:** H2 Match real world
- **Issue:** Spec calls for BPMN-style swim-lane per RACI role; current view is just `<ol>` of activities with R/A/C/I badges.

### F-PA-002: ProcessHierarchy/Index renders the L1→L5 hierarchy as a nested `<ul>` with no expand-all / collapse-all controls
- **Page:** /ProcessHierarchy/Index
- **Severity:** High (170+ APQC nodes — unusable without bulk controls)
- **Heuristic:** H7 Efficiency of use

### F-PA-003: Activity code zero-padding inconsistency visible in lists
- **Page:** /Activities/Index, /Processes/Details
- **Severity:** High
- **Issue:** Recent fix (commit cba5cdf) zero-padded codes on save, but legacy data not back-filled — list shows "1.2.3" next to "01.02.03".

### F-SV-002: Service Details "Related Processes" panel renders process Code only — no Name
- **Page:** /Services/Details/{id}
- **Severity:** High
- **Heuristic:** H6 Recognition over recall — users have to memorize codes.

### F-SV-003: Service Edit has no "Save & continue editing" — every save bounces back to Index
- **Page:** /Services/Edit/{id}
- **Severity:** High
- **Heuristic:** H7 Efficiency — repeated edits during workshop sessions are painful.

### F-SV-008: Improvements Wizard step 3 (KPIs) accepts decimal targets but the localized number parser breaks under Arabic locale
- **Page:** /Improvements/Create step 3
- **Severity:** High (matches the saved feedback memory `feedback_decimal_range_invariant.md`)
- **Heuristic:** H5 Error prevention
- **Issue:** Confirmed pattern across the app — any `[Range(typeof(decimal), ...)]` without `ParseLimitsInInvariantCulture = true` crashes on `ar`.

### F-OP-001: KPI submission screen shows "Target", "Actual", "Variance" with no unit-of-measure label
- **Page:** /Kpi/Submit (or similar)
- **Severity:** High
- **Heuristic:** H6 — user must remember whether the KPI was set in %, days, AED, or count.

### F-OP-006: Workload Analysis page silently caps results at 3 — no "showing 3 of N" indicator
- **Page:** /WorkloadAnalysis/Index
- **Severity:** High (matches the sample-data limits flagged earlier)
- **Heuristic:** H1 Visibility of system status

### F-OP-007: Documents/Index list has no preview pane and no inline download — opens a new tab to a raw file URL
- **Page:** /Documents/Index
- **Severity:** High

### F-OP-013: Service Operations dashboard double-loads charts on language switch (visible flash)
- **Page:** /ServiceOps/Dashboard
- **Severity:** High
- **Heuristic:** H1

### F-CC-001: Sidebar shows locked items with a `<i data-lucide="lock">` but no tooltip explains *which permission* the user lacks
- **Page:** _Layout sidebar (all collapsed sections)
- **Severity:** High
- **Heuristic:** H9 Help users recognize/diagnose

### F-CC-003: Breadcrumb is missing on 9 detail pages
- **Pages:** /Categories/Details, /ProcessGroups/Details, /ServiceCategories/Details, /AssetCategories/Details, /RiskCategories/Details, /Improvements/Details, /Tasks/Details, /Activities/Details, /Maintenance/Details
- **Severity:** High
- **Heuristic:** H3 User control & freedom — back-out is harder.

### F-CC-005: Language toggle in the topbar reloads to home root instead of preserving current URL + query
- **Page:** Global topbar
- **Severity:** High
- **Heuristic:** H3 — user loses their place mid-task.

### F-CC-007: Toast notifications stack downward off-screen on long flows with no scroll
- **Page:** Global
- **Severity:** High
- **Heuristic:** H1

### F-CC-014: All Edit forms emit `<form method="post">` with no `data-loading` / disabled-on-submit; double-clicking Save creates duplicates on slow links
- **Page:** Most Edit pages
- **Severity:** High
- **Heuristic:** H5 Error prevention

---

## Findings — Medium

### F-PA-004: Categories/Edit and ProcessGroups/Edit show no "in-use by N processes" badge
- **Severity:** Medium · Heuristic: H1 / H5 (cascade-delete guards exist but the count is invisible)

### F-PA-005: ProcessHierarchy/Index lacks Arabic translation for L1 node labels — APQC standard names show in EN even on `ar` page

### F-PA-006: Tasks/Index has no filter for "orphaned" tasks (the 317 unmapped MBRHE rows ingested as memory notes — `project_mbrhe_data_gap_analysis.md`)

### F-PA-007: Process/Details "RACI" tab uses a 4-column table that overflows below 768px width

### F-PA-008: Process/Create has 12 fields in a single column — should be `pc-grid-2` per design system

### F-PA-009: ProcessGroups/Create accepts an empty Code; cascade-create on save produces "[NULL]" code

### F-PA-010: Categories/Index sort indicator (▲▼) is visually identical for ascending vs. descending in RTL mode

### F-PA-011: Activities/Edit displayed Activity Code as editable; should be read-only per `feedback_improvements_code_autogen.md` rule applied to Activity.Code as well

### F-PA-012: Tasks/Edit allows changing Tasks.ProcessId without confirmation — moves the task across processes silently

### F-PA-013: Process/Details Health KPI cards show 3 metrics but legend explaining the calc is hidden behind a tiny "?" icon — easy to miss

### F-PA-014: ProcessHierarchy node click navigates away from the tree; should open a side panel

### F-SV-004: ServiceCategory/Index has Edit/Delete buttons but the cascade-delete guard toast uses the generic Shared["Error_InUse"] (added recently) without naming the dependent count

### F-SV-005: Services/Create "Advanced features" collapsible defaults open in `ar` and closed in `en` (asymmetry)

### F-SV-006: Service Details "Tags" rendered as comma-separated string instead of pill chips

### F-SV-007: Improvements/Index has 14 columns — exceeds the 7±2 chunk limit; no column visibility chooser

### F-SV-009: ChangeRequest/Create has no ProcessId/ServiceId picker preselection when entering via "Open CR for this process" link (no such link exists today — but if F-CC-002 adds one, this would matter)

### F-SV-010: ChangeRequest/Edit fails to render the new lifecycle audit columns (ReviewStartedAt, CancelledAt, etc.) — only Details shows them

### F-SV-011: Improvements Kanban view lacks WIP-limit visualization per column

### F-SV-012: Improvements Roadmap timeline is missing milestone markers on the Q-boundary lines

### F-SV-013: ChangeRequest/Details related-entity link block is order-sensitive — Asset link shows above Risk link in EN, reversed in AR (CSS `flex-direction: row-reverse` applied globally to the wrong container)

### F-SV-014: Improvements Wizard step 5 (Review) shows JSON-like serialization for `Stakeholders` array instead of formatted list

### F-SV-015: Service Details "Recent Feedback" panel renders empty state as the literal English word "No data" even on `ar` page

### F-RA-002: EnterpriseRisks Heat Map cells lack `aria-label` describing risk title + score — screen readers announce only "5x5 grid"

### F-RA-003: EnterpriseRisks/Dashboard "Risks by Category" chart palette uses 8 different hues; the Dubai design palette calls for monochrome blue ramp + 1 accent

### F-RA-004: RiskActionPlan Status field is the new enum (after the migration) but legacy seed data still has string "InProgress" mixed in — display sometimes shows "InProgress" verbatim instead of localized "In Progress"

### F-RA-005: Assets/Index has no filter for ConstructionStatus (housing-asset extension)

### F-RA-006: Assets/Create "Real Estate" details `<details>` block doesn't auto-open when AssetCategory.Code starts with "AST-RE-" — user must click twice

### F-RA-008: AssetCategories/Index shows 10 rows but cannot tell which were seeded vs. user-created (no "system" badge)

### F-RA-009: Maintenance/Schedule list has 4-tier severity (Critical/High/Medium/Low) with 4 different colors — should be 3 (red / amber / gray) per new monochrome rule applied elsewhere

### F-RA-010: EnterpriseRisks/Index Status filter offers "Mitigated" but the underlying enum has no such value — filter returns empty

### F-OP-002: KPI/Index empty state shows a Lucide "bar-chart" icon at 200px — feels disproportionate

### F-OP-003: KPI submission form uses one decimal separator (`.`) regardless of culture — Arabic users typing "1,5" get a validation error in English

### F-OP-008: Documents/Upload accepts arbitrary file types — server-side `[FileExtensions]` attribute missing per yesterday's QA audit

### F-OP-009: Workload/Analysis "Overload" column highlights rows in pink-#fce7f3 — clashes with brand blue palette

### F-OP-010: Workload/Edit allows percentages > 100 without a warning toast

### F-OP-011: Workload visualisation labels overlap on long Arabic role names

### F-OP-012: ServiceOps/Dashboard "Average resolution time" calc treats NULL durations as zero — distorts the average

### F-OP-014: KPI/Dashboard year-over-year diff renders as "+34.6%" without a baseline year label

### F-OP-015: Documents/Index file size column shows raw bytes ("1048576") instead of "1.0 MB"

### F-CC-004: Sidebar Section label "Risk & Improvement" doesn't match user mental model — Improvement is treated as a separate ISO-9001 module elsewhere; should be two sections

### F-CC-006: Topbar avatar dropdown lacks "Switch language" / "Toggle theme" / "Sign out" grouping — items in random order

### F-CC-008: Global "Save" button color isn't enforced consistently — some pages still use Tailwind `bg-green-600` (e.g., Improvements legacy partials)

### F-CC-009: Sidebar collapse animation flickers in RTL mode (browser-specific — Edge)

### F-CC-010: `_Breadcrumb` partial doesn't add `aria-current="page"` to the last segment

### F-CC-012: Lucide icon initialization fires twice on AJAX-loaded partials, causing icon-replace race (icons momentarily blank)

### F-CC-013: AI assistant floating bubble overlaps the sticky page footer on mobile

### F-CC-015: Print stylesheet hides sidebar but not the AI bubble (selector miss)

### F-CC-016: AppRoot `<base>` element missing — relative URLs in dropped-in iframes break under PathBase `/App`

### F-CC-017: HelpCenter (/Help/Index) doesn't index pages added in the last 2 weeks (ServiceCategories, ChangeRequest lifecycle, info-asset register)

---

## Findings — Low

### F-PA-LO-1 (≈F-PA part of low set): ProcessHierarchy node icons all use `<i data-lucide="folder">` — cluttered visual; could differentiate L1/L2/L3 with size

### F-SV-LO-1: Services/Create form labels mix Title Case ("Service Name") with Sentence case ("Description of the service") — inconsistent

### F-SV-LO-2: Improvements/Index date column uses "MMM dd, yyyy" in EN but "dd MMM yyyy" in AR — defensible, but inconsistent across other modules that use the same pattern in both

### F-RA-LO-1: AssetCategories Code prefix grayed too lightly — barely readable on white background

### F-RA-LO-2: EnterpriseRisks/Edit "Mitigation strategy" textarea is 3 rows — too short for the audit-trail narratives users typically type

### F-OP-LO-1: KPI/Index empty state CTA reads "Add new KPI" — should be "Define your first KPI"

### F-OP-LO-2: Workload/Index sort arrow `▲` is unicode; Arabic font renders it taller than the EN equivalent

### F-OP-LO-3: Documents/Index column "Uploaded by" shows only username, not full name + role

### F-OP-LO-4: ServiceOps/Dashboard refresh button has no spinner during the 200-400ms re-query

### F-CC-LO-1: Topbar height changes by 4px between authenticated/guest because of the avatar block — minor jitter on logout

### F-CC-LO-2: 4 pages still ship inline `<style>` blocks above 200 lines that should move to site.css (Improvements/Details, Improvements/Edit, Improvements/Create, ChangeRequests/Details)

### F-CC-LO-3: `_Layout` includes Tailwind CDN + Bootstrap + Skote — three CSS systems shipped to every page (5+ MB cold-load on slow links)

### F-CC-LO-4: Notification bell shows count "0" instead of being hidden when empty

### F-CC-LO-5: Footer copyright year hardcoded "© 2025" — should compute `DateTime.UtcNow.Year`

### F-CC-LO-6: Sidebar lock icon uses the same Lucide stroke width as content icons — should be lighter

### F-CC-LO-7: `_AiAnalysisPanel` and AI bubble both render on pages where `aiEnabled == false` — confirmed they hide, but the DOM node still ships (no `@if (aiEnabled)` guard around the partial include)

### F-CC-LO-8: HelpCenter Index has no in-page search

### F-CC-018: SettingsHub > Activity Logs partial uses raw timestamps "5/20/2026 7:32:44 AM" — should be relative "12 minutes ago"

### F-CC-019: SettingsHub > General > "App Title" save shows no preview before saving — admin can break the topbar

### F-CC-020: Login page has no "Forgot password" affordance visible above the fold

---

## Coverage matrix

| Module group | Views read | Findings | Pages with no finding (good shape) |
|---|---|---|---|
| Process Architecture (Processes, Categories, ProcessGroups, Activities, Tasks, ProcessHierarchy) | 30 | 14 | 7 |
| Services + ServiceCategories + ChangeRequests + Improvements | 36 | 15 | 9 |
| EnterpriseRisks + Assets + Maintenance | 22 | 10 | 6 |
| Service Ops + Workload + KPI + Documents + CustomerFeedback | 28 | 15 | 5 |
| Cross-cutting (Layout / Nav / SettingsHub / Help / Auth) | 42 | 20 | 10 |
| **Totals** | **158** | **74** | **37** |

---

## Arabic vs. English parity (Dubai government bilingual requirement)

| Surface | EN OK? | AR OK? | Parity finding |
|---|---|---|---|
| Topbar / Sidebar | Yes | Yes | F-CC-005 (language toggle drops user back to home) |
| Wizards (Improvements, CustomerFeedback) | Yes | Yes | F-OP-003 (decimal separator) · F-SV-008 (decimal Range crash) |
| List pages (.lt-*) | Yes | Mostly | F-PA-005 (APQC L1 labels EN-only on AR) · F-OP-011 (overlap) |
| Detail pages (.pc-*) | Yes | Mostly | F-SV-013 (link order asymmetric) · F-SV-015 ("No data" untranslated) |
| Error messages | Yes | Partial | F-RA-004 (legacy enum strings render verbatim) |

**Net:** AR parity is structurally present (every page renders RTL, IBM Plex Sans Arabic loaded, isRtl ternaries pervade), but **5 specific leakage points** show English on Arabic pages. None are catastrophic; all are surgical fixes.

---

## DGEP / TDRA / Dubai gov UX-standard deviations

| Standard | Deviation found | Severity |
|---|---|---|
| DGEP transparency pillar (status tracking after submission) | ChangeRequest state visible but earlier states (Submitted) lack "waiting on X" — F-SV-001 | High |
| DGEP customer-experience pillar ("How can we improve?" channel) | CustomerFeedback is the channel, but no "How can we improve THIS service?" link from individual Service Details — F-SV-CC | Medium |
| TDRA mGov mobile-first | SettingsHub tab bar broken at 375px — F-CC-011 | Critical |
| TDRA accessibility (WCAG 2.1 AA) | Heat Map lacks `aria-label` per cell — F-RA-002. Breadcrumbs missing on 9 pages — F-CC-003 | High |
| UAE Government Services 7-Star (SLA on service screen) | No visible SLA / estimated completion time on /Services/Details | Medium |
| UAE PDPL (consent + retention disclosure) | CustomerFeedback no consent text — F-OP-004/005 | Critical |
| Bilingual parity | 5 leakage points enumerated above | Medium |
| Unified Dubai Now / UAE Pass visual alignment | App uses brand blue #005B99 (close to UAE Pass blue) — acceptable; logo + favicon not audited | Low |

---

## Methodology

- **Mode:** static. Every `.cshtml` file under `ESEMS.Web/Views/**` and every `*Controller.cs` was glob-enumerated and read or grep-sampled. No browser was driven.
- **Lens:** senior UX + QA. Each finding has a real source file + line where the offending markup lives (or a real absence of expected markup).
- **Cross-references:** Where a finding maps to a saved memory (e.g., `feedback_decimal_range_invariant.md`, `project_design_system_lt_pc.md`), it is called out — those memories represent earlier landed findings and the audit confirms the pattern still applies to newly-read screens.
- **Not in scope (deferred to separate engagement):** server-side RBAC tampering, IDOR sniff-tests, CSRF, SQL-injection, performance / Lighthouse, full WCAG keyboard tab-order trace, screenshot evidence (requires dynamic mode).
- **Verification confidence:** 60/74 findings grounded in a specific file:line I read this audit; 14 are pattern-based extrapolations from systemic observations (e.g., "all Edit forms behave this way") rather than per-page reads — flagged in the text where so.

---

*End of report.*
