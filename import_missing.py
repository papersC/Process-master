#!/usr/bin/env python3
"""Import the 13 missing procedures into the database."""
import json
import pyodbc
import uuid

# Load Excel analysis data
with open(r'ExcelReader\analysis_output.json', 'r', encoding='utf-8') as f:
    excel = json.load(f)

# Connect to database
conn = pyodbc.connect(r'DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=ESEMS;Trusted_Connection=yes;')
cursor = conn.cursor()

# Get existing ProcessTasks (to skip duplicates)
cursor.execute("SELECT NameEn, NameAr FROM ProcessTasks")
db_tasks = cursor.fetchall()
db_names_en = set(t.NameEn for t in db_tasks if t.NameEn)
db_names_ar = set(t.NameAr for t in db_tasks if t.NameAr)

# Get existing Processes (SubProcesses) by Arabic name
cursor.execute("SELECT Id, NameAr, NameEn FROM Processes WHERE ClassificationType = 1")
sub_procs = cursor.fetchall()
sp_map_ar = {p.NameAr: p.Id for p in sub_procs if p.NameAr}

# Get existing Activities by ProcessId
cursor.execute("SELECT Id, ProcessId, NameAr FROM Activities")
activities = cursor.fetchall()
act_by_proc = {a.ProcessId: a.Id for a in activities}

# Get existing MainProcesses
cursor.execute("SELECT Id, NameAr FROM Processes WHERE ClassificationType = 0")
main_procs = cursor.fetchall()
mp_map_ar = {p.NameAr: p.Id for p in main_procs if p.NameAr}

# Get ProcessGroup IDs
cursor.execute("SELECT Id, NameAr FROM ProcessGroups")
pgs = cursor.fetchall()
pg_map = {p.NameAr: p.Id for p in pgs}

# Get existing OrganizationUnits for OwningUnit
cursor.execute("SELECT Id, NameAr FROM OrganizationUnits")
org_units = cursor.fetchall()
ou_map = {o.NameAr: o.Id for o in org_units}

# Find the 13 missing procedures
missing = []
for pd in excel['ProcedureDetails']:
    name_en = pd.get('NameEn', '')
    name_ar = pd.get('NameAr', '')
    if name_en not in db_names_en and name_ar not in db_names_ar:
        missing.append(pd)

print(f"Found {len(missing)} missing procedures to import\n")

# Get current max PROC code number
cursor.execute("SELECT MAX(CAST(REPLACE(Code, 'PROC-', '') AS INT)) FROM ProcessTasks WHERE Code LIKE 'PROC-%'")
max_code = cursor.fetchone()[0] or 150
next_code = max_code + 1

# Get current max SP code number
cursor.execute("SELECT MAX(CAST(REPLACE(Code, 'SP-', '') AS INT)) FROM Processes WHERE Code LIKE 'SP-%'")
max_sp = cursor.fetchone()[0] or 42
next_sp = max_sp + 1

imported = 0
for pd in missing:
    name_en = pd.get('NameEn', '')
    name_ar = pd.get('NameAr', '')
    sp_ar = pd.get('SubProcessAr', '').strip()
    sp_en = pd.get('SubProcessEn', '').strip()
    mp_ar = pd.get('MainProcessAr', '').strip()
    section_ar = pd.get('SectionAr', '').strip()
    dept_ar = pd.get('DepartmentAr', '').strip()
    
    activity_id = None
    
    # Strategy 1: SubProcess Arabic name exists in DB
    if sp_ar and sp_ar in sp_map_ar:
        proc_id = sp_map_ar[sp_ar]
        if proc_id in act_by_proc:
            activity_id = act_by_proc[proc_id]
        else:
            # Create activity for this SubProcess
            act_id = str(uuid.uuid4())
            cursor.execute("""
                INSERT INTO Activities (Id, ProcessId, NameEn, NameAr, Code, DisplayOrder,
                    ChannelType, HasDetailedBreakdown, CreatedAt, UpdatedAt, Version, IsDeleted, IsAutomated)
                VALUES (?, ?, ?, ?, ?, ?, 0, 0, GETUTCDATE(), GETUTCDATE(), 1, 0, 0)
            """, act_id, proc_id, f"Activities for {sp_en or sp_ar}", f"أنشطة {sp_ar}",
                f"ACT-SP-{next_sp:03d}", 1
            )
            act_by_proc[proc_id] = act_id
            activity_id = act_id
            print(f"  Created Activity for existing SubProcess: {sp_ar}")
    
    # Strategy 2: No SubProcess but has MainProcess - create a default SubProcess
    elif mp_ar and mp_ar in mp_map_ar:
        mp_id = mp_map_ar[mp_ar]
        # Get the ProcessGroupId from the MainProcess
        cursor.execute("SELECT ProcessGroupId FROM Processes WHERE Id = ?", mp_id)
        pg_id = cursor.fetchone()[0]
        
        # Create or find a default SubProcess "إجراءات عامة" under this MainProcess
        default_sp_name = f"إجراءات عامة - {mp_ar}"
        if default_sp_name not in sp_map_ar:
            sp_id = str(uuid.uuid4())
            cursor.execute("""
                INSERT INTO Processes (Id, NameEn, NameAr, Code, ProcessGroupId, ClassificationType, DisplayOrder,
                    ProcessType, Status, HasDetailedBreakdown, CreatedAt, UpdatedAt, Version, IsDeleted,
                    IsAutomated, AutomationStatus, AutomabilityStatus, CurrentProposedStatus)
                VALUES (?, ?, ?, ?, ?, 1, ?, 0, 0, 0, GETUTCDATE(), GETUTCDATE(), 1, 0, 0, 0, 0, 0)
            """, sp_id, f"General Procedures - {pd.get('MainProcessEn', mp_ar)}", default_sp_name,
                f"SP-{next_sp:03d}", pg_id, next_sp
            )
            sp_map_ar[default_sp_name] = sp_id
            next_sp += 1
            
            # Create Activity
            act_id = str(uuid.uuid4())
            cursor.execute("""
                INSERT INTO Activities (Id, ProcessId, NameEn, NameAr, Code, DisplayOrder,
                    ChannelType, HasDetailedBreakdown, CreatedAt, UpdatedAt, Version, IsDeleted, IsAutomated)
                VALUES (?, ?, ?, ?, ?, ?, 0, 0, GETUTCDATE(), GETUTCDATE(), 1, 0, 0)
            """, act_id, sp_id, f"General Activities - {pd.get('MainProcessEn', mp_ar)}",
                f"أنشطة عامة - {mp_ar}", f"ACT-SP-{next_sp-1:03d}", 1
            )
            act_by_proc[sp_id] = act_id
            activity_id = act_id
            print(f"  Created default SubProcess + Activity under MainProcess: {mp_ar}")
        else:
            sp_id = sp_map_ar[default_sp_name]
            activity_id = act_by_proc.get(sp_id)
    
    # Strategy 3: No SubProcess, no MainProcess - create uncategorized
    else:
        default_sp_name = "إجراءات غير مصنفة"
        if default_sp_name not in sp_map_ar:
            # Get default ProcessGroup
            pg_id = list(pg_map.values())[0] if pg_map else None

            sp_id = str(uuid.uuid4())
            cursor.execute("""
                INSERT INTO Processes (Id, NameEn, NameAr, Code, ProcessGroupId, ClassificationType, DisplayOrder,
                    ProcessType, Status, HasDetailedBreakdown, CreatedAt, UpdatedAt, Version, IsDeleted,
                    IsAutomated, AutomationStatus, AutomabilityStatus, CurrentProposedStatus)
                VALUES (?, ?, ?, ?, ?, 1, ?, 0, 0, 0, GETUTCDATE(), GETUTCDATE(), 1, 0, 0, 0, 0, 0)
            """, sp_id, "Uncategorized Procedures", default_sp_name,
                f"SP-{next_sp:03d}", pg_id, next_sp
            )
            sp_map_ar[default_sp_name] = sp_id
            next_sp += 1
            
            act_id = str(uuid.uuid4())
            cursor.execute("""
                INSERT INTO Activities (Id, ProcessId, NameEn, NameAr, Code, DisplayOrder,
                    ChannelType, HasDetailedBreakdown, CreatedAt, UpdatedAt, Version, IsDeleted, IsAutomated)
                VALUES (?, ?, ?, ?, ?, ?, 0, 0, GETUTCDATE(), GETUTCDATE(), 1, 0, 0)
            """, act_id, sp_id, "Uncategorized Activities", "أنشطة غير مصنفة",
                f"ACT-SP-{next_sp-1:03d}", 1
            )
            act_by_proc[sp_id] = act_id
            activity_id = act_id
            print(f"  Created 'Uncategorized' SubProcess + Activity")
        else:
            sp_id = sp_map_ar[default_sp_name]
            activity_id = act_by_proc.get(sp_id)
    
    if not activity_id:
        print(f"  SKIP (no activity): {name_ar}")
        continue
    
    # Resolve owning unit
    owning_unit_id = ou_map.get(section_ar) or ou_map.get(dept_ar) or None
    
    # Map automation status
    auto_status = 0  # NotAutomated default
    auto_str = pd.get('AutomationStatus', '')
    if 'Full' in auto_str: auto_status = 2
    elif 'Partial' in auto_str or 'Semi' in auto_str: auto_status = 1
    
    proc_status = 0
    ps = pd.get('ProcedureStatus', '')
    if 'Documented' in ps: proc_status = 1
    elif 'Reviewed' in ps: proc_status = 2
    
    task_id = str(uuid.uuid4())
    code = f"PROC-{next_code:03d}"
    
    cursor.execute("""
        INSERT INTO ProcessTasks (Id, NameEn, NameAr, DescriptionEn, DescriptionAr, Code, ActivityId,
            OwningUnitId, DisplayOrder, AutomationStatus, ProcedureStatus,
            DigitalSystemName, AutomationAssessmentScores, LinkedServices,
            DocumentReference, DocumentLanguage,
            ChannelType, CreatedAt, UpdatedAt, Version, IsDeleted, IsAutomated,
            AutomabilityStatus, CurrentProposedStatus)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, GETUTCDATE(), GETUTCDATE(), 1, 0, 0, 0, 0)
    """, task_id, name_en, name_ar, pd.get('DescriptionEn', ''), '', code, activity_id,
        owning_unit_id, next_code, auto_status, proc_status,
        pd.get('DigitalSystem', ''), pd.get('AutomationScores', ''),
        pd.get('LinkedServices', ''), pd.get('DocumentReference', ''),
        pd.get('DocumentLanguage', ''))
    
    print(f"  [{code}] {name_ar} ({name_en}) -> Activity: {activity_id[:8]}...")
    imported += 1
    next_code += 1

conn.commit()
conn.close()

print(f"\n{'='*60}")
print(f"DONE: Imported {imported} of {len(missing)} missing procedures")
print(f"Total ProcessTasks should now be: {150 + imported}")

