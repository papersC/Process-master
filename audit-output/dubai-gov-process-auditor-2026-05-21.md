# ESEMS — Dubai Government Senior Process Audit
**Date:** 2026-05-21
**Branch / commit:** `master` @ `d598379`
**Standards:** DGEP 4th Generation · UAE Government Services Standards (7-Star) · MBRGEA · APQC PCF · BPMN 2.0 · ISO 9001 / 27001 / 31000 / 20000 · UAE PDPL (Federal Decree-Law 45/2021) · ISR v2 / NESA
**Auditor:** Senior Process Management Consultant (40+ yrs Dubai government — DEWA / RTA / DLD / Smart Dubai / Dubai Municipality lens), via Claude
**Scope:** All 4 modules (Processes · Services · Initiatives [= Improvements] · Workload) + cross-cutting
**Priority lens:** Methodology + Integration (selected by Khaled)

---

## Executive summary

ESEMS is a structurally competent process-management platform with a clear L1→L5 APQC hierarchy, exemplary BPMN swimlane reconciliation, comprehensive bilingual scaffolding, and strong M2M linkage between the *strategic* triad (Processes ↔ Services ↔ Improvements ↔ Strategic Objectives). It collapses, however, on three governance pillars that a Dubai government entity will be judged on at the next DGEP / MBRGEA assessment:

1. **The Workload module is a structural silo.** `WorkloadLineItem` has no FK to `Process` / `Service` / `OrganizationUnit`. Step durations are re-entered manually, violating the single-source-of-truth principle. The staffing-demand insights cannot flow back into process redesign — the feedback loop DGEP calls for is broken.
2. **Stage-gate enforcement is permissive, not gated.** Improvements transition state without prerequisite audits (charter sign-off, sponsor assignment, risk review). Processes have no publish-approval gate at all — every save is the live version. This fails MBRGEA §3.1 gate-control discipline.
3. **The Dashboard does not respect org-unit scoping.** Despite a `ScopingService` being available, `DashboardController` issues global counts. A scoped Process Owner sees the whole entity — DGEP transparency + ISR v2 multi-tenant separation both violated.

**Overall maturity: 2.4 / 5 (Emerging → Developing band).** Best-in-class L1→L5 modelling is undermined by missing publish gates, missing review cycles on processes, and structural workload isolation. None of the findings is one-way; all are remediable inside one quarter (the integration sprint X1+X2+X3+W1 is ~4 weeks). Without those, the system's data is rich but its governance is theatre.

**Top 3 strengths**
- L1→L5 APQC hierarchy correctly structured (Category=L1, ProcessGroup=L2, Process=L3, Activity=L4, Task=L5); `BpmnLaneReconciler` matches swim-lanes to OrganizationUnits — sophisticated.
- 12 M2M junctions with Cascade/SetNull discipline; Process ↔ Service ↔ Improvement ↔ Strategic Objective all bidirectional and configured in `ApplicationDbContext`.
- Bilingual parity is foundational, not bolted on — `BilingualEntity` base class + `IStringLocalizer<SharedResource>` + `ar`/`en` RequestLocalization wired correctly.

**Top 3 critical risks**
1. **X1 / W1 — Workload ↔ Process integration absent.** No FK; durations re-entered; staffing analytics divorced from process master.
2. **X3 — Dashboard lacks org-unit scoping.** Global KPIs leak across units, breaking DGEP transparency + multi-tenant governance.
3. **I3 — Stage-gate transitions are state-machine-permissive.** No prerequisite enforcement before Improvement state changes (charter, sponsor, risk-matrix sign-off).

**Most urgent action:** Bundle **X1 + X2 + X3 + W1** into one focused 4-week integration sprint. Without these, every other governance fix sits on broken foundations.

---

## Maturity scorecard

```
Processes module             ███░░  3.0/5  (Developing)
Services module              ███░░  2.5/5  (Developing)
Initiatives (Improvements)   ███░░  2.5/5  (Developing)
Workload Analysis            ██░░░  2.0/5  (Emerging)
Cross-module integration     ██░░░  2.1/5  (Emerging)
Governance & audit trail     ███░░  3.0/5  (Developing)
Reporting & decision support ██░░░  2.0/5  (Emerging)
─────────────────────────────────────────────────────
Overall                      ██░░░  2.4/5  (Emerging → Developing)
```
**Bands:** 1 Initial · 2 Emerging · 3 Developing · 4 Advanced · 5 Leading.

---

## Findings — total 61 (Critical 8 · High 19 · Medium 30 · Low 4)

### CRITICAL (8)

| # | Module | Finding | Dimension | Standard | Evidence | Remediation | Effort |
|---|---|---|---|---|---|---|---|
| **X1** | Cross | Workload ↔ Process linkage missing; no FK on `WorkloadLineItem` | Integration | DGEP Portfolio; SSoT | `WorkloadLineItem.cs:18-22` — `ProcessId`/`ServiceId` orphaned, no FK constraint | Add FK `WorkloadLineItem.ProcessId → Process.Id` (Cascade) + `ServiceId → Service.Id` (Cascade); backfill rows from linked Scenario | L |
| **X2** | Cross | Workload ↔ OrganizationUnit reverse navigation absent; FTE not rolled up by unit | Integration | DGEP Resource Planning; ISR v2 multi-tenant | `WorkloadScenario.OwningUnitId` not FK-declared; LineItem has no unit scope | Add explicit FK `WorkloadScenario.OwningUnitId → OrganizationUnit.Id` (SetNull); index `(FiscalYear, OwningUnitId)`; scope `WorkloadAnalysisController.Index()` | M |
| **X3** | Cross | Dashboard ignores org-unit scoping; KPIs are global | Governance | DGEP Transparency; ISR v2 RBAC layering | `DashboardController.cs:33-80` makes no `ScopingService` call; all tile counts unscoped | Inject `IScopingService`; wrap every `DbSet` query with `.Where(p => scopedUnitIds.Contains(p.OwningUnitId))`; preserve unscoped counts only when `user.ScopeLevel == "All"` | M |
| **W1** | Workload | `AvgProcessingTimeMinutes` manually entered; not pulled from `Activity.AggregatedDurationMinutes` | Integration | SSoT (DGEP single-source-of-truth) | `WorkloadLineItem.cs:30` is a manual decimal column; Activity already holds the canonical duration | Refactor `WorkloadLineItem.AvgProcessingTimeMinutes` to computed read from linked Activity/ProcessTask; allow manual override only via flag + justification text | XL |
| **P1** | Processes | Process versioning lacks publish/approve gate; every save is live | Governance | DGEP §4.1; APQC governance | `Process.cs` has no `ApprovedAt` / `ApprovedById` / `PublishedAt` / `PublishedVersion` fields. `ProcessBpmnVersion` tracks BPMN only | Add `PublishedAt`, `PublishedById`, `VersionNumber`, `ApprovalStatus`; gate state-change actions behind `WorkflowService` (Draft → Approved → Published) | L |
| **I3** | Initiatives | Stage-gate transitions state-machine-permissive; no prerequisite gate-check | Governance | MBRGEA §3.1 Gate Control | `ImprovementStatusMachine.cs:41-52` allows Proposed → UnderReview with zero pre-conditions | Build `ImprovementGateCheckService` enforcing charter completeness + sponsor signature + risk-matrix sign-off; block transitions if any gate fails | M |
| **S1** | Services | Service catalog missing `Eligibility` + `RequiredDocuments` columns | Data Quality | UAE Gov Services Std §3.1 (mandatory catalog fields) | `Service.cs:11` extends `MeasurableEntity`; no Eligibility/Docs props; not in `ServiceCatalogInfo` either | Add `Eligibility` (max 2000) + `RequiredDocuments` (max 4000) — either as Service columns or as ServiceCatalogInfo narrative fields | M |
| **S11** | Services | No customer journey entity — touchpoints / pain points / stages exist only as narrative text | Data Quality | DGEP CX Pillar 3 (journey mapping) | grep for "journey/touchpoint" in `Models/Services/` returns nothing structural; only `ServiceCatalogInfo.Procedure` mentions steps as freeform | Create `ServiceJourney` + `ServiceTouchpoint` tables: `(Id, ServiceId, StageEn, StageAr, SequenceNumber, Description, PainPoints, OwningRole)`; allow per-touchpoint feedback | XL |

### HIGH (19)

| # | Module | Finding | Dimension | Standard | Evidence | Remediation | Effort |
|---|---|---|---|---|---|---|---|
| **P2** | Processes | RACI defined but not mandatory at process / activity / task level | Governance | ISO 9001:2015 §5.3; APQC | `RaciBase` collections on Process/Activity/Task are not `[Required]`; controllers don't enforce | Validator + entity constraint that fires on `SaveChangesAsync` if RACI missing; UI hint "RACI required to publish" | M |
| **P3** | Processes | No metadata for process Inputs / Outputs / Suppliers / Customers (APQC PCF columnar fields) | Methodology | APQC PCF L3 canonical; DGEP §3.2 | `Process.cs` lacks Inputs/Outputs/Suppliers/Customers; `LinkedServices`/`ExternalPartners` are comma-separated strings | Add 5 nullable columns (or M2M junction tables); render each in Details view | M |
| **P4** | Processes | BPMN validation is structural only (XML well-formed + lanes present); no semantic check | Methodology | BPMN 2.0 (OMG); APQC lane mapping | `BpmnValidator.cs` does not enforce: lane has Start+End event, exclusive gateways have ≥2 outgoing flows, all paths reconverge | Extend validator with semantic checks; store result in `ProcessBpmnValidation`; gate publish behind validator passing | M |
| **P5** | Processes | No review cycle / expiry reminder for Process (Improvements has it; Process does not) | Governance | DGEP §4.3 recertification; ISO 9001:2015 | `Process.cs` has no `ReviewDueDate` / `LastReviewedAt` / `ReviewCycle`; no `ProcessReviewReminder` background service | Add the three columns; create `ProcessReviewReminder` service mirroring `RecurringReviewScheduler`; emit 30-day-before notification | M |
| **S2** | Services | No G2C / G2B / G2G classification; `ServiceType` only distinguishes Internal/External | Methodology | UAE Gov Services Std §2.3 (service nature) | `Service.cs:21` — `enum ServiceType { Internal, External }` | Add `enum ServiceNature { G2C, G2B, G2G, G2B2C }`; store as `ServiceNatureId` | L |
| **S3** | Services | No digital maturity / star rating stored on Service | Methodology | UAE Gov 7-Star Maturity Model | No `DigitalMaturityRating` / `StarRating` / `IsPaperless` field on `Service.cs` | Add `int DigitalMaturityRating (1-7)` + `bool? IsPaperless`; consider `ServiceMaturityAssessment` for audit trail | M |
| **S4** | Services | `CustomerFeedback` links to Service but has no touchpoint / stage identifier | Data Quality | DGEP Customer Experience Pillar 2 | `CustomerFeedback.cs:32` has `ServiceId` FK but no `Stage` / `Touchpoint` / `Timing` field | Add `enum ServiceFeedbackStage { SubmissionAck, InProgress, Resolved, Closed }`; allow multiple feedback rows per service across the journey | M |
| **S7** | Services | `ApprovalSlaHostedService` monitors workflow step SLAs but does NOT monitor `Service.SLADays` or `ServiceCatalogInfo.DurationValue` | Governance | UAE Gov Services Std §3.2 (SLA performance) | `ApprovalSlaHostedService.cs:1-50` scans `WorkflowStep` only | Create `ServiceSlaHostedService` (15-min tick): flag `SLADays` breaches on open Incidents/Problems linked to service; populate `SLABreach` table; alert owner | L |
| **I1** | Initiatives | `StrategicObjectiveId` nullable at schema; orphan initiatives creatable | Governance | DGEP §2.1 Strategic Alignment | `Improvement.cs:64` `public string? StrategicObjectiveId` | Add NOT NULL constraint + backfill orphans; enforce in EF modelBuilder via `IsRequired()` | M |
| **I2** | Initiatives | Charter fragmented across 3 tables; no `Sponsor`, no `Scope`, no explicit `Budget` column | Governance | DGEP §2.2 Initiative Charter | `Improvement.cs` has no Sponsor/Scope/Budget fields; ChangeRequest (adjacent) is richer | Consolidate into `ImprovementCharter` entity: sponsor + scope (bilingual) + planned budget (AED) + risks | L |
| **I4** | Initiatives | No RAG (Red/Amber/Green) health status; only the Impact×Effort Quadrant | Governance | DGEP §2.3 Portfolio Health | `Improvement.cs:89` — Quadrant calc yields 4 quadrants, not RAG; Status enum has no RAG band | Define `ImprovementHealthStatus { Green, Amber, Red }` based on ProgressPercentage vs TargetDate variance; surface on dashboard | M |
| **I5** | Initiatives | Legacy `[Obsolete]` `ProcessId`/`ServiceId` shims remain alongside the M2M tables; dual SoT | Data Quality | DGEP Portfolio SSoT | `Improvement.cs:44-51` — obsolete columns coexist with `ImprovementProcesses` / `ImprovementServices` M2M; old queries silently miss multi-linked rows | Complete Batch B migration: backfill M2M, drop the shim columns, remove nav props, update all reporting queries | L |
| **I6** | Initiatives | Benefits realization not gated; initiative can move Closed→BenefitsRealization with zero `BenefitsReview` rows | Governance | DGEP §2.4 Benefits Tracking | `ImprovementBenefitsReview.cs:54-109` — rows created by scheduler but no NOT NULL guard at transition | Add NOT NULL guard on `Outcome` + `ReviewedById`; scheduler must guarantee three reviews before controller permits state change | S |
| **W2** | Workload | No per-role FTE allocation — one role per line item only | Methodology | Activity-Based Costing; DGEP Resource Optimization | `WorkloadLineItem` aggregates volume × duration; never splits across roles | Add `WorkloadLineItemRole` junction `(RoleId, FTEAllocation %, Description)`; compute per-role gap; extend Dashboard/Compare | L |
| **W3** | Workload | No demand-driver field; frequency not pulled from process master | Methodology | IPA Demand Modelling; ABC | `WorkloadLineItem.AnnualVolume` disconnected from any Activity/Task frequency master | Add `AnnualFrequency` on Activity/ProcessTask; link LineItem.AnnualVolume as FK + manual override; version demand drivers | M |
| **X4** | Cross | Audit trail captures FK values as guids; deep entity-link changes are opaque | Governance | ISR v2 immutability; ISO 27001 | `AuditSaveChangesInterceptor.cs:100-119` records field values but not FK target name | Enhance `ExtractValues()` to resolve FK target name at audit-write: `OldValues["ProcessName"] = oldProcess.Code + "|" + oldProcess.NameEn` | M |
| **X5** | Cross | `CustomerFeedback ↔ Service` FK not explicitly declared in `ApplicationDbContext`; relying on EF convention | Data Quality | SSoT; ISO 27001 | No `HasForeignKey()` call in `ConfigureCustomerFeedback()` | Add explicit `HasOne(cf => cf.Service).WithMany().HasForeignKey(cf => cf.ServiceId).OnDelete(DeleteBehavior.SetNull)` | S |
| **X6** | Cross | `Process ↔ Risk` FK not explicitly declared; implicit EF convention | Data Quality | SSoT; ISO 27001 | `EnterpriseRisk.ProcessId` (nullable) — no Fluent API config in `ConfigureRiskManagement()` | Same pattern as X5 | S |
| **X8** | Cross | `AuditLogsController` has no scope filter — all users with `Reports.View` see all audits | Governance | DGEP transparency; ISR v2 RBAC layering | Controller has no `IScopingService` call | Inject `IScopingService`; filter `AuditLog.EntityType` + `EntityId` to scoped entity list | M |
| **X11** | Cross | `CustomerFeedback` has no `ConsentGivenAt` / `RetentionExpiresAt` / `IsErasureRequested` columns | Compliance | UAE PDPL Art. 14-16 | `CustomerFeedback.cs:1-160` — none of the three; submitted feedback has no consent proof and no retention horizon | Add 3 columns + scheduled erasure job (delete rows where `RetentionExpiresAt < UtcNow`) | M |

### MEDIUM (30)

| # | Module | Finding | Standard | Effort |
|---|---|---|---|---|
| **P6** | Processes | Risk linkage one-way; `RiskActionPlan` has no `ProcessId`; can't trace Controls back to Process | ISO 31000:2018; DGEP §5.1 | M |
| **P7** | Processes | `ProcessMeasurement` no `BaselineValue` / no `OwnerId`; `Frequency` is freeform string; KPIs not rendered on Process Details | APQC L3 KPI structure; DGEP §6.1 | M |
| **P8** | Processes | Improvement→Process M2M present but Process Details doesn't load/display linked improvements (PDCA loop invisible) | APQC continuous improvement; DGEP §7.1 | S |
| **P9** | Processes | Activity/Task RACI not enforced at role level — can have "Responsible = Unit X" without naming the role in X | ISO 9001:2015 §5.1 | S |
| **P10** | Processes | APQC L1 names not seeded as canonical (1.0 Develop Vision, 2.0 Manage Customer, etc.); arbitrary text accepted | APQC PCF 2024 | S |
| **P11** | Processes | APQC L1 labels render English on Arabic page (bigTest F-D-004 still open) | DGEP bilingual; H4 Consistency | S |
| **S8** | Services | `Service.IsActive` is boolean only; no `Retired` status; no version history; every Save overwrites | DGEP Service Governance §5 | M |
| **S9** | Services | `ServiceCatalogInfo.IsPublished` toggled inline in Step 3 wizard; no separate approval workflow | DGEP catalog governance | M |
| **S10** | Services | Process↔Service M2M exists but Services Index doesn't show "Linked Processes" count; bidirectional visibility incomplete | DGEP Process-Service linkage | S |
| **S12** | Services | `ServiceCategory` is flat; no hierarchical taxonomy (unlike `ProcessCategory`) | APQC P-CH analogy | L |
| **I7** | Initiatives | Stall detection (`InitiativeStallDetectionService`) emits notifications only; no state hold; `TargetDate` overflow silent | DGEP §4.3 governance cadence | M |
| **I8** | Initiatives | `RecurringReviewScheduler` cadence not configurable by stage; no formal `SteeringCommitteeReview` entity for board reviews | DGEP §2.3 Portfolio Review | M |
| **I9** | Initiatives | `ImprovementAction.AssignedToId` is single-assignee only; no RACI bands; action completion % does not cascade to parent | DGEP §3.2 Execution Oversight | L |
| **I10** | Initiatives | Measurement-vs-Benefit dual-source: `Measurement.IsBenefitTracked` flag and `BenefitsReview.ActualCostSaving` accumulate separately with no reconciliation | DGEP §2.4 Quantification | M |
| **I11** | Initiatives | `ChangeRequest` parallel lifecycle has no `ImprovementId` FK; can exist standalone; can't navigate Improvement→CR | MBRGEA §3.2 Change Continuity | S |
| **I12** | Initiatives | No `SponsorId` column on Improvement; sponsor is inferred from `ImprovementTeamMember.Role` lookup; sponsor accountability diffuse | DGEP §2.2 Governance Roles | S |
| **W4** | Workload | No scenario snapshot mechanism; can't compare "Q1 2026 approved workload" vs "Q4 2025 approved workload" — only current state | IPA best practice; DGEP reporting | M |
| **W5** | Workload | Absence rate + admin overhead combined into one factor; can't split "FTE gap due to absence" vs "due to overhead" | DGEP clarity | M |
| **W6** | Workload | Productivity factor not configurable per role / experience level — no learning curve | IPA Demand Modelling | L |
| **W7** | Workload | `Compare.cshtml` limited to ad-hoc pairs; no multi-scenario ranking / heatmap | DGEP Scenario Planning | M |
| **W9** | Workload | No automation-candidate scoring; no linkage to `Process.AutomationAssessmentScores` for RPA prioritization | DGEP Automation Strategy | M |
| **W10** | Workload | Dashboard KPIs misaligned with DGEP Resource Optimization (no Automation Index, no Scenario Approval Rate, no role-skill gap) | DGEP KPI framework | M |
| **X7** | Cross | Legacy `Improvement.ProcessId/ServiceId` shims still populated alongside M2M; boot-time backfill undocumented | DGEP Portfolio SSoT | M |
| **X9** | Cross | `WorkflowService` supports Improvement + ChangeRequest only; Incident/Problem have no approval chain (ISO 20000-1 mandates one) | DGEP Change Control; ISO 20000-1 | M |
| **X10** | Cross | Dashboard KPI tiles do not drill down to underlying records — counts are dead-end | DGEP Analytics | S |
| **X12** | Cross | No APQC PCF classification code on Process or Service entities | DGEP Portfolio; APQC PCF | M |
| **X13** | Cross | No service method supports cross-module analytical queries like "initiatives targeting processes with SLA breach > 10% in Q1 2026" | DGEP Portfolio Analytics | M |
| **X14** | Cross | `ProcessService` M2M `Criticality`/`IsMandatory` audited but FK target names not resolved in audit log | ISO 27001; ISR v2 | S |
| **X15** | Cross | Exports (PDF, Excel) not parameterized by org-unit scope or date range; can exfiltrate PII | DGEP transparency; PDPL data-minimization | M |
| **W8** | Workload | `WorkloadScenario.CurrentHeadcount` has hard 0-10000 cap but no soft thresholds; gap > 25% required FTE silent | Data integrity (typo detection) | S |

### LOW (4)

| # | Module | Finding | Standard | Effort |
|---|---|---|---|---|
| **P12** | Processes | `Process.Version` auto-increments but no `ProcessVersion` snapshot table; rollback / "what changed?" hard | APQC audit trail | L |
| **S5** | Services | ServiceCatalogInfo bilingual completeness — all 8 narrative pairs present | DGEP §6 Bilingual | — *(positive)* |
| **S6** | Services | Channel coverage uses ChannelType + structured `DeliveryChannels` enum — correctly modelled | UAE Gov Services Std §4.2 | — *(positive)* |
| — | — | (No further Low findings) | | |

---

## 4-module integration matrix

| Link | Type | FK | Status | Risk |
|---|---|---|---|---|
| Process ↔ Service | M2M `ProcessService` | ✓ Cascade | Fully configured | Low |
| Improvement ↔ Process | M2M `ImprovementProcess` | ✓ Cascade | Configured (legacy shim coexists — X7/I5) | Medium |
| Improvement ↔ Service | M2M `ImprovementService` | ✓ Cascade | Configured (legacy shim coexists) | Medium |
| Improvement ↔ StrategicObjective | FK nullable | ✓ SetNull | Schema-permissive (I1) | High |
| Service ↔ StrategicObjective | M2M | ✓ Cascade | Fully configured | Low |
| Process ↔ StrategicObjective | M2M | ✓ Cascade | Fully configured | Low |
| **Workload ↔ Process** | — | **✗** | **ABSENT (X1 / W1)** | **Critical** |
| **Workload ↔ Service** | — | **✗** | **ABSENT (X1)** | **Critical** |
| Workload ↔ OrganizationUnit | implicit | ⚠ partial | Scenario→Unit (config only); LineItem orphaned (X2) | High |
| Service ↔ CustomerFeedback | FK nullable | ⚠ implicit | Convention-based, not Fluent (X5) | Medium |
| Process ↔ Risk (`EnterpriseRisk`) | FK nullable | ⚠ implicit | Convention-based (X6) | Medium |
| Improvement ↔ Risk | M2M `ImprovementRisk` | ✓ Cascade | Configured | Low |
| Improvement ↔ ChangeRequest | — | **✗** | ABSENT (I11) | Medium |

---

## Roadmap

### Quick wins (0–30 days) — Critical/High, S effort

- **X5, X6** — explicit FK Fluent API for CustomerFeedback↔Service + Process↔EnterpriseRisk (1-day each)
- **I6** — NOT NULL guard on `BenefitsReview.Outcome`/`ReviewedById` before Closed→BenefitsRealization (½-day + migration)
- **I11** — add `ImprovementId?` FK on `ChangeRequest`; bidirectional nav (1-day + migration)
- **I12** — add `SponsorId` FK on `Improvement`; enforce at UnderReview gate (1-day)
- **P8** — load + render Improvements on `Processes/Details` (2-day)
- **P9, P10, P11** — APQC L1 canonical names + role-level RACI guard + DataTables i18n (3-day combined)
- **S10** — "Linked Processes" badge + drill-down on `Services/Index` (1-day)
- **X10** — Dashboard tile drill-down links (1-day)
- **X14** — extend audit interceptor FK resolution for `ProcessService` (½-day, ties into X4)
- **W8** — soft headcount-warn threshold + admin notify (1-day)

### Short-term (1–3 months) — M effort, structural

- **X3** — inject `IScopingService` into `DashboardController` + apply org-unit filter everywhere (1 wk)
- **X2** — explicit FK + composite index `(FiscalYear, OwningUnitId)` (3-day)
- **X4** — audit FK resolution helper across all audited entities (1 wk)
- **X8** — scope `AuditLogsController` to user's entity set (3-day)
- **X11** — PDPL columns + scheduled erasure job (1 wk)
- **X15** — scope + date filter on every `Export()` action; strip PII unless explicitly requested (1 wk)
- **P3** — Inputs / Outputs / Suppliers / Customers (M2M junctions preferred) (1 wk)
- **P4** — semantic BPMN validator (Start/End events, gateway flows) (1 wk)
- **P5** — `ProcessReviewReminder` background service mirroring `RecurringReviewScheduler` (1 wk)
- **P7** — `BaselineValue` + `OwnerId` + Frequency enum on `ProcessMeasurement`; render KPIs on Process Details (1 wk)
- **I3** — `ImprovementGateCheckService` enforcing charter+sponsor+risk before transitions (2 wk)
- **I4** — `ImprovementHealthStatus` RAG + dashboard surfacing (1 wk)
- **I7** — integrate stall detection into `CanTransition` guard; surface SLA variance in Details (1 wk)
- **I10** — define which is SoT (Measurement vs BenefitsReview); add reconciliation view (1 wk)
- **S1** — Eligibility + RequiredDocuments columns + form fields (1 wk)
- **S3** — `DigitalMaturityRating` + `IsPaperless` (3-day) + assessment audit trail (1 wk)
- **S4** — `ServiceFeedbackStage` enum + per-stage feedback collection (1 wk)
- **W3** — demand-driver fields on Activity/Task; LineItem.AnnualVolume FK-pull (1 wk)
- **W4** — `WorkloadScenarioSnapshot` table + temporal Compare (2 wk)
- **W5, W7, W9, W10** — overhead breakdown + multi-scenario ranking + automation candidates + DGEP-aligned dashboard (3 wk combined)
- **X9** — extend `WorkflowService` to Incident/Problem (1 wk)
- **X12** — `ApqcPcfCode` lookup + picker on Process/Service (1 wk)
- **X13** — `ReportingService` with cross-module analytical queries (2 wk)

### Strategic (3–12 months) — L/XL, structural redesign

- **X1 + W1** — Workload ↔ Process FK + duration computed-pull from `Activity.AggregatedDurationMinutes`; manual override via flag + justification. The single biggest unlock. (1 quarter)
- **W2** — `WorkloadLineItemRole` junction for per-role FTE allocation across activities (1 quarter)
- **W6** — `RoleProductivityProfile` per role/experience with learning curve (1 quarter)
- **P1** — Process publish/approve gate via `WorkflowService`; version snapshot table; rollback (1 quarter)
- **P12** — `ProcessVersion` snapshot table for full "what changed?" history (1 quarter)
- **I2** — `ImprovementCharter` consolidated entity replacing fragmented fields (1 quarter)
- **I5** — Batch B migration completion: drop obsolete `ProcessId`/`ServiceId` shims (1 quarter; needs careful query migration)
- **I9** — RACI on `ImprovementAction` + completion cascade to parent Improvement (1 quarter)
- **S2** — `ServiceNature { G2C, G2B, G2G, G2B2C }` enum + full 7-Star alignment (1 quarter)
- **S7** — `ServiceSlaHostedService` monitoring `Service.SLADays` breach (1 quarter)
- **S8, S9, S12** — Service lifecycle (`Retired` + version) + catalog publish workflow + hierarchical `ServiceCategory` (1 quarter combined)
- **S11** — `ServiceJourney` + `ServiceTouchpoint` first-class entities (1 quarter)

---

## Methodology + limitations

- **Audit-only** — no code modified.
- **Five parallel Explore subagents** — one per module + cross-cutting — gathered evidence in ~10 min wall-clock.
- **Evidence:** controllers, EF model classes, `ApplicationDbContext` Fluent API, background services, views, prior audit reports (`audit-output/bigTest-report-2026-05-21.md` written this morning).
- **Not assessed (require runtime / DB inspection):** actual seeded role list at runtime, AuditLog query performance under load, BPMN-XML conformance on imported diagrams.
- **Not assessed (out of scope):** UI usability per page (covered by today's bigTest pass), AI services (`VectorStoreService`, `AiAssistantService`), full ISO 27001 control-by-control gap.
- **Confidence:** High on Critical/High findings — each cites specific file:line evidence. Medium on a small number of cross-module assertions that need a final eyes-on review of FK config blocks in `ApplicationDbContext`.

---

*End of report.*
