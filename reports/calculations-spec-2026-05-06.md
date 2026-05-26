# Computed Calculations Spec

**Generated:** 2026-05-06 from `[NotMapped]` getters in `ESEMS.Web/Models/`
plus `Calculate*`/`Compute*` methods in `ESEMS.Web/Services/`.
**Coverage:** 18 formulas across 5 files.

## How to use this

Every entry is a number the database does NOT store -- it's recomputed
at read time from other fields. If a UI shows a number that disagrees
with what you expect, find the property here, read the formula, and
compare the inputs against the row in the DB.

Get-block bodies are extracted on a best-effort basis. When the
formula column reads `(see source)` the property uses a multi-statement
get-block; open the source file at the listed line.

## Key business formulas (manually curated)

These are the high-impact scoring formulas that drive UI ranking and
strategic decisions. The auto-extractor missed them because they're
plain getter properties (no `[NotMapped]` attribute) — EF treats them
as not-mapped via convention since they're read-only.

### Improvement.TotalPrioritizationScore  (Models/Improvement/Improvement.cs:215)
```
TotalPrioritizationScore = BusinessReadinessScore × 0.30
                         + BusinessValueScore     × 0.70
```
Returns `null` if either input is missing. Drives the default sort on
the approval queue and the dashboard's "Top priorities" panel.
Recomputed every read — never persisted — so legacy rows with stale
scores are auto-refreshed when the row is touched.

### Improvement.CalculateQuadrant  (Models/Improvement/Improvement.cs)
```
quadrant =
  Impact ≥ impactCutoff && Effort < effortCutoff   → QuickWins
  Impact ≥ impactCutoff && Effort ≥ effortCutoff   → MajorProjects
  Impact <  impactCutoff && Effort < effortCutoff   → FillIns
  otherwise                                         → ThanklessTasks
```
Cutoffs come from `PrioritizationConfig` (defaults 6 / 6 if no active
config row exists). Approver can override the auto-calculated value via
the new Re-rank flow (see ImprovementRerankApprovalTests).

### EnterpriseRisk.CalculateInherentRiskScore  (Models/RiskManagement/EnterpriseRisk.cs:138)
```
InherentRiskScore = Likelihood × Impact          (1..25 grid)
RiskLevel = score ≥ 15 → Critical
            score ≥ 10 → High
            score ≥ 5  → Medium
            else       → Low
```
Both inputs are `[Range(1,5)]`-clamped at the controller layer so an
attacker can't smuggle Likelihood=99 and land off the heat map.

### EnterpriseRisk.CalculateResidualRiskScore  (Models/RiskManagement/EnterpriseRisk.cs:153)
```
ResidualRiskScore = ResidualLikelihood × ResidualImpact
                    (uses the same RiskLevel cutoffs as inherent)
```
Both ResidualLikelihood + ResidualImpact are nullable — risks without
controls assessed leave ResidualRiskScore = null and don't render on
the residual heat map.



## CustomOrganizationUnit.cs

| Line | Type | Name | Formula |
|-----:|------|------|---------|
| 61 | `bool` | **IsActive** | `true` |
| 64 | `bool` | **IsDeleted** | `false` |

## CustomUser.cs

| Line | Type | Name | Formula |
|-----:|------|------|---------|
| 119 | `bool` | **IsActive** | `true` |

## WorkloadConfig.cs

| Line | Type | Name | Formula |
|-----:|------|------|---------|
| 63 | `int` | **GrossWorkingDaysPerYear** | `(WorkingDaysPerWeek * 52) - PublicHolidaysPerYear` |
| 67 | `int` | **AbsenceDays** | `AnnualLeaveDays + AverageSickDays + TrainingDaysPerYear` |
| 71 | `int` | **NetWorkingDaysPerYear** | `GrossWorkingDaysPerYear - AbsenceDays` |
| 75 | `decimal` | **GrossAnnualHours** | `GrossWorkingDaysPerYear * WorkingHoursPerDay` |
| 84 | `decimal` | **NetAvailableHoursPerFTE** | `NetWorkingDaysPerYear         * WorkingHoursPerDay         * (1m - AdminOverheadPercent / 100m)         * TargetUtilizationRate` |
| 91 | `decimal` | **AllowanceFactor** | `GrossAnnualHours > 0 ? NetAvailableHoursPerFTE / GrossAnnualHours : 0` |

## WorkloadLineItem.cs

| Line | Type | Name | Formula |
|-----:|------|------|---------|
| 72 | `decimal` | **WeightedVolume** | `if (!ComplexityEnabled) return AnnualVolume; var simple = AnnualVolume * (SimpleVolumePercent ?? 0) / 100m * (SimpleMult ?? 1.0m); var medium = AnnualVolume * (MediumVolumePercent ?? 0) / 100m * (MediumMult ?? 1.5m); var complex = AnnualVol` |
| 88 | `decimal` | **WorkloadHours** | `WeightedVolume * AvgProcessingTimeMinutes / 60m` |
| 92 | `decimal` | **RequiredFTE** | `Scenario?.Config != null && Scenario.Config.NetAvailableHoursPerFTE > 0             ? WorkloadHours / Scenario.Config.NetAvailableHoursPerFTE             : 0m` |

## WorkloadScenario.cs

| Line | Type | Name | Formula |
|-----:|------|------|---------|
| 47 | `decimal` | **TotalWorkloadHours** | `LineItems?.Sum(li => li.WorkloadHours) ?? 0m` |
| 51 | `decimal` | **TotalRequiredFTE** | `Config != null && Config.NetAvailableHoursPerFTE > 0             ? TotalWorkloadHours / Config.NetAvailableHoursPerFTE             : 0m` |
| 58 | `decimal` | **AdjustedFTE** | `if (ProjectionYears <= 0 \|\| !GrowthRatePercent.HasValue \|\| GrowthRatePercent.Value == 0) return TotalRequiredFTE; var growthFactor = (decimal)Math.Pow((double)(1m + GrowthRatePercent.Value / 100m), ProjectionYears); return TotalRequired` |
| 70 | `decimal` | **SupervisoryFTE** | `Config != null && Config.SupervisoryRatio > 0             ? AdjustedFTE / Config.SupervisoryRatio             : 0m` |
| 76 | `decimal` | **TotalFTE** | `Math.Ceiling(AdjustedFTE + SupervisoryFTE)` |
| 80 | `decimal` | **FTEGap** | `TotalFTE - (CurrentHeadcount ?? 0)` |
