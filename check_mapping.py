#!/usr/bin/env python3
"""Check mapping between Excel file data and database categories."""
import json
import pyodbc

# Load Excel analysis data
with open(r'ExcelReader\analysis_output.json', 'r', encoding='utf-8') as f:
    excel = json.load(f)

print("=" * 80)
print("EXCEL FILE DATA (from analysis_output.json)")
print("=" * 80)

print(f"\nProcess Groups (APQC Level 2): {len(excel['ProcessGroups'])}")
for i, pg in enumerate(excel['ProcessGroups']):
    print(f"  {i+1}. {pg}")

print(f"\nMain Processes (APQC Level 3): {len(excel['MainProcesses'])}")
for i, mp in enumerate(excel['MainProcesses']):
    print(f"  {i+1}. {mp['NameAr']}")

print(f"\nSub-Processes: {len(excel['SubProcesses'])}")
for i, sp in enumerate(excel['SubProcesses']):
    print(f"  {i+1}. {sp['NameAr']}")

print(f"\nProcedures (Level 5): {len(excel['ProcedureDetails'])}")

# Connect to database
conn = pyodbc.connect(r'DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=ESEMS;Trusted_Connection=yes;')
cursor = conn.cursor()

print("\n" + "=" * 80)
print("DATABASE DATA")
print("=" * 80)

# Categories
cursor.execute("SELECT Id, Code, NameEn, NameAr FROM Categories")
cats = cursor.fetchall()
print(f"\nCategories (APQC Level 1): {len(cats)}")
for c in cats:
    print(f"  [{c.Code}] {c.NameAr} | {c.NameEn}")

# Process Groups
cursor.execute("SELECT pg.Id, pg.Code, pg.NameAr, pg.NameEn, c.NameEn as CatName FROM ProcessGroups pg LEFT JOIN Categories c ON pg.CategoryId = c.Id")
pgs = cursor.fetchall()
print(f"\nProcess Groups (APQC Level 2): {len(pgs)}")
for pg in pgs:
    print(f"  [{pg.Code}] {pg.NameAr} -> Category: {pg.CatName}")

# Processes (Main + Sub)
cursor.execute("SELECT p.Id, p.Code, p.NameAr, p.NameEn, p.ClassificationType, pg.NameAr as PGName FROM Processes p LEFT JOIN ProcessGroups pg ON p.ProcessGroupId = pg.Id ORDER BY p.ClassificationType, p.Code")
procs = cursor.fetchall()
main_procs = [p for p in procs if p.ClassificationType == 0]
sub_procs = [p for p in procs if p.ClassificationType == 1]
print(f"\nMain Processes (APQC Level 3): {len(main_procs)}")
for p in main_procs:
    print(f"  [{p.Code}] {p.NameAr} -> PG: {p.PGName}")

print(f"\nSub-Processes: {len(sub_procs)}")
for p in sub_procs:
    print(f"  [{p.Code}] {p.NameAr} -> PG: {p.PGName}")

# Activities
cursor.execute("SELECT a.Id, a.Code, a.NameAr, a.NameEn, p.NameAr as ProcessName FROM Activities a LEFT JOIN Processes p ON a.ProcessId = p.Id ORDER BY a.Code")
acts = cursor.fetchall()
print(f"\nActivities (APQC Level 4): {len(acts)}")
for a in acts[:10]:
    print(f"  [{a.Code}] {a.NameAr} -> Process: {a.ProcessName}")
if len(acts) > 10:
    print(f"  ... and {len(acts)-10} more")

# ProcessTasks
cursor.execute("SELECT pt.Id, pt.Code, pt.NameAr, a.NameAr as ActivityName, pt.BpmnDiagram FROM ProcessTasks pt LEFT JOIN Activities a ON pt.ActivityId = a.Id ORDER BY pt.Code")
tasks = cursor.fetchall()
with_bpmn = sum(1 for t in tasks if t.BpmnDiagram)
print(f"\nProcess Tasks (APQC Level 5): {len(tasks)} (with BPMN: {with_bpmn})")
for t in tasks[:10]:
    print(f"  [{t.Code}] {t.NameAr} -> Activity: {t.ActivityName}")
if len(tasks) > 10:
    print(f"  ... and {len(tasks)-10} more")

# KEY ISSUES
print("\n" + "=" * 80)
print("MAPPING ISSUES")
print("=" * 80)

# Issue 1: All processes point to same ProcessGroup
cursor.execute("SELECT DISTINCT ProcessGroupId FROM Processes")
distinct_pg = cursor.fetchall()
print(f"\n1. Distinct ProcessGroupIds used by Processes: {len(distinct_pg)}")
for d in distinct_pg:
    print(f"   {d.ProcessGroupId}")

# Issue 2: Check if MainProcesses are correctly linked to ProcessGroups
print(f"\n2. Excel has {len(excel['ProcessGroups'])} ProcessGroups but DB has {len(pgs)} ProcessGroups")
print(f"   All {len(main_procs)} main processes + {len(sub_procs)} sub-processes point to SAME ProcessGroup")

# Issue 3: Check procedure-to-subprocess mapping
cursor.execute("""
    SELECT a.NameAr as ActivityName, COUNT(pt.Id) as TaskCount 
    FROM Activities a 
    LEFT JOIN ProcessTasks pt ON pt.ActivityId = a.Id 
    GROUP BY a.NameAr 
    ORDER BY TaskCount DESC
""")
act_counts = cursor.fetchall()
print(f"\n3. Activity -> ProcessTask distribution:")
for ac in act_counts[:15]:
    print(f"   {ac.ActivityName}: {ac.TaskCount} tasks")
if len(act_counts) > 15:
    print(f"   ... and {len(act_counts)-15} more activities")

# Issue 4: Check Excel ProcedureDetails for ProcessGroup mapping
print(f"\n4. Excel ProcedureDetails ProcessGroup distribution:")
pg_dist = {}
for pd in excel['ProcedureDetails']:
    pg = pd.get('ProcessGroup', 'NONE')
    pg_dist[pg] = pg_dist.get(pg, 0) + 1
for pg, cnt in sorted(pg_dist.items(), key=lambda x: -x[1]):
    print(f"   {pg}: {cnt} procedures")

# Issue 5: Check MainProcess distribution in ProcedureDetails
print(f"\n5. Excel ProcedureDetails MainProcess distribution:")
mp_dist = {}
for pd in excel['ProcedureDetails']:
    mp = pd.get('MainProcessAr', 'NONE')
    mp_dist[mp] = mp_dist.get(mp, 0) + 1
for mp, cnt in sorted(mp_dist.items(), key=lambda x: -x[1]):
    print(f"   {mp}: {cnt} procedures")

conn.close()

