# Delete Policy

This document is the authoritative reference for which entities in ESEMS support hard delete, soft delete, or no delete at all. It exists so that Edit/Index views, controllers, and any future automation behave consistently.

The policy was decided as part of UX-08 in the 2026-05-14 audit follow-up. The driver: list views previously showed a delete button for some entities, no affordance at all for others, and a "Retire" action for a third group — leaving users guessing whether deletion was unavailable, forbidden, or simply hidden.

## Categories

### A — Audit-locked (no delete)

Records in an audit trail or workflow chain. Deleting them would orphan workflow steps, approvals, SLA history, or evidence. These entities **must not** expose a delete affordance. Soft-archive (where it exists) or status flip is the only path.

- **Incidents** — workflow + SLA + escalation history
- **EnterpriseRisks** — Bowtie chains, treatment plans, residual scoring history
- **Problems** — root-cause chains, often linked to many incidents
- **ChangeRequests** — approval audit, downstream activation
- **CustomerFeedback** — regulatory complaint trail
- **MaintenanceRecord** — asset uptime history
- **SLA** — referenced by every workflow instance under it
- **Activities** — workflow / approval log entries

Implementation: Index rows render a **disabled `trash-2` icon** with the tooltip "Cannot delete — audit-locked" (Arabic: "لا يمكن الحذف — مقفل للتدقيق"). Users see the affordance is acknowledged but unavailable, instead of an unexplained absence.

If a Delete controller action still exists for an entity in this category (some do, for admin-data-migration scenarios), it remains in the controller but is not surfaced in the UI. A separate `Admin/DataMigration` workflow (TODO) is the only legitimate caller.

### B — Soft-delete only (Retire / Restore)

Entities where rows are referenced widely but go in and out of active use. Hard delete would break historical reports; soft retire/restore handles the lifecycle.

- **StrategicObjectives** — Retire / Restore
- **KpiLibrary** — Retire / Restore
- **ImprovementInitiative** — Status-based (Draft only is deletable; everything else is status-flipped)

Implementation: row actions show **Retire** or **Restore** icons (`archive` / `rotate-ccw`) — no trash icon, no hard delete affordance.

### C — Hard-deletable (config / lookup data)

Configuration and lookup tables. Rows have no audit trail of their own — they're referenced *by* records but deleting a config row is a configuration decision, not a record-history concern.

- **Services** — has button ✓
- **Assets** — needs button (controller action exists)
- **Categories** — has button ✓
- **ProcessGroups** — has button ✓
- **Users** — has button ✓
- **Roles** — has button (Admin role disabled) ✓
- **WorkloadAnalysis (scenarios)** — has button (permission-gated) ✓
- **OrganizationUnits** — needs button (controller action exists)
- **AssetCategories** — needs button (controller action exists)
- **RoleGroups** — needs button (controller action exists)

Implementation: row shows a working **`trash-2` button** that opens the standard SweetAlert confirm flow and POSTs to the controller's Delete action.

### D — Intentionally no delete (no controller action at all)

- **Processes** — by design; deletion would cascade through process architecture, RACI, KPI mappings. Use ProcessStatus + retire.

## Rules for new entities

When adding a new entity:

1. Decide which category it belongs to **before** writing the controller.
2. Category A → no Delete action method. Show disabled trash icon.
3. Category B → implement Retire/Restore POST actions. Show `archive` / `rotate-ccw` icons.
4. Category C → standard Delete + SweetAlert confirm + visible button.
5. Category D → no Delete action. No icon.

This file is the single source of truth — update it whenever the policy for an entity changes.
