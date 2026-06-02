/*  reset-apqc-catalog.sql
    ──────────────────────────────────────────────────────────────────────
    Surgically wipes ONLY the APQC process catalog so you can re-import the
    "APQC Process Mapping" sheet into a clean catalog and verify the codes
    (1 / 1.1 / 1.1.1, doc code in LegacyCode).

    DELETES (hard / physical):
      Categories, ProcessGroups, Processes
      + everything that hangs off a Process: Activities, ProcessTasks,
        Process/Activity/Task RACI, ProcessMeasurements, ProcessRisks,
        ProcessServices, ProcessResponsibilities, ProcessStrategicObjectives,
        ImprovementProcesses (M2M links), ProcessDocuments, ProcessBpmnVersions,
        BpmnLanes.

    PRESERVES (only their link to the deleted processes is set to NULL):
      Organization units, Services, Assets, Risks, Users, Improvements,
      Change Requests, Incidents, Problems, Customer Feedback, Workload
      scenarios/line-items, and all lookup tables.

    SAFETY:
      • Wrapped in a transaction with XACT_ABORT ON — any error rolls the
        whole thing back, so you can't end up half-wiped.
      • DRY RUN: change the final COMMIT to ROLLBACK to preview the before/
        after counts without committing anything.
      • ⚠ Take a database backup (or snapshot) first — this is destructive.

    Run against your ESEMS database (SSMS, Azure Data Studio, or:
      sqlcmd -S .\SQLEXPRESS -d ESEMS -E -i reset-apqc-catalog.sql ).
    ──────────────────────────────────────────────────────────────────────*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Before: catalog size now
SELECT 'before' AS phase,
       (SELECT COUNT(*) FROM Categories)    AS Categories,
       (SELECT COUNT(*) FROM ProcessGroups) AS ProcessGroups,
       (SELECT COUNT(*) FROM Processes)     AS Processes;

-- 1) Null the optional cross-references so deleting Processes can't trip a FK
--    (these tables are KEPT — only their ProcessId link is cleared).
UPDATE Assets                  SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE EnterpriseRisks         SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE Incidents               SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE Problems                SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE CustomerFeedbacks       SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE ChangeRequests          SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE ImprovementInitiatives  SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE ImprovementMeasurements SET AppliesToProcessId = NULL WHERE AppliesToProcessId IS NOT NULL;
UPDATE WorkloadLineItems       SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;

-- 2) Activity / Task subtree (these FKs are Restrict → delete child-first).
DELETE FROM TaskRacis;
DELETE FROM ProcessTasks;
DELETE FROM ActivityRacis;
DELETE FROM Activities;

-- 3) Process-scoped children and junction tables.
DELETE FROM ProcessRacis;
DELETE FROM ProcessMeasurements;
DELETE FROM ProcessRisks;
DELETE FROM ProcessServices;
DELETE FROM ProcessResponsibilities;
DELETE FROM ProcessStrategicObjectives;
DELETE FROM ImprovementProcesses;
DELETE FROM ProcessDocuments;
DELETE FROM ProcessBpmnVersions;
DELETE FROM BpmnLanes;

-- 4) Catalog spine, child → parent.
DELETE FROM Processes;
DELETE FROM ProcessGroups;
DELETE FROM Categories;

-- After: should be 0 / 0 / 0
SELECT 'after' AS phase,
       (SELECT COUNT(*) FROM Categories)    AS Categories,
       (SELECT COUNT(*) FROM ProcessGroups) AS ProcessGroups,
       (SELECT COUNT(*) FROM Processes)     AS Processes;

-- ▼▼▼ Change COMMIT to ROLLBACK for a dry run (preview counts, commit nothing). ▼▼▼
COMMIT;
