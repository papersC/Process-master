#!/usr/bin/env python3
"""Find the 13 missing procedures that were not imported."""
import json
import pyodbc

# Load Excel analysis data
with open(r'ExcelReader\analysis_output.json', 'r', encoding='utf-8') as f:
    excel = json.load(f)

# Connect to database
conn = pyodbc.connect(r'DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=ESEMS;Trusted_Connection=yes;')
cursor = conn.cursor()

# Get all ProcessTask names from DB
cursor.execute("SELECT NameAr, NameEn FROM ProcessTasks")
db_tasks = cursor.fetchall()
db_names_ar = set(t.NameAr for t in db_tasks if t.NameAr)
db_names_en = set(t.NameEn for t in db_tasks if t.NameEn)

print(f"DB ProcessTasks: {len(db_tasks)}")
print(f"Excel ProcedureDetails: {len(excel['ProcedureDetails'])}")
print()

# Find missing procedures
missing = []
for i, pd in enumerate(excel['ProcedureDetails']):
    name_ar = pd.get('NameAr', '')
    name_en = pd.get('NameEn', '')
    
    # Check if exists in DB by either Arabic or English name
    in_db = name_ar in db_names_ar or name_en in db_names_en
    
    if not in_db:
        missing.append({
            'index': i + 1,
            'NameAr': name_ar,
            'NameEn': name_en,
            'SubProcessAr': pd.get('SubProcessAr', ''),
            'SubProcessEn': pd.get('SubProcessEn', ''),
            'MainProcessAr': pd.get('MainProcessAr', ''),
            'ProcessGroup': pd.get('ProcessGroup', ''),
            'SectionAr': pd.get('SectionAr', ''),
            'DepartmentAr': pd.get('DepartmentAr', ''),
        })

print(f"MISSING PROCEDURES: {len(missing)}")
print("=" * 100)

for m in missing:
    print(f"\n  #{m['index']}:")
    print(f"    NameAr: {m['NameAr']}")
    print(f"    NameEn: {m['NameEn']}")
    print(f"    SubProcess: {m['SubProcessAr']} | {m['SubProcessEn']}")
    print(f"    MainProcess: {m['MainProcessAr']}")
    print(f"    ProcessGroup: {m['ProcessGroup']}")
    print(f"    Section: {m['SectionAr']}")
    print(f"    Department: {m['DepartmentAr']}")

# Now check WHY they failed - check if their SubProcess exists in DB as a Process
print("\n\n" + "=" * 100)
print("ROOT CAUSE ANALYSIS")
print("=" * 100)

cursor.execute("SELECT NameAr, NameEn FROM Processes")
db_processes = cursor.fetchall()
db_proc_names_ar = set(p.NameAr for p in db_processes if p.NameAr)
db_proc_names_en = set(p.NameEn for p in db_processes if p.NameEn)

for m in missing:
    sp_ar = m['SubProcessAr']
    sp_en = m['SubProcessEn']
    sp_in_db = sp_ar in db_proc_names_ar or sp_en in db_proc_names_en
    
    reason = ""
    if not sp_ar and not sp_en:
        reason = "EMPTY SubProcess (no parent to link to)"
    elif not sp_in_db:
        reason = f"SubProcess NOT in DB: '{sp_ar}' / '{sp_en}'"
    else:
        reason = f"SubProcess EXISTS in DB but procedure still missing - check name match"
    
    print(f"\n  {m['NameAr']}")
    print(f"    SubProcess: '{sp_ar}' / '{sp_en}'")
    print(f"    Reason: {reason}")

# Also check for duplicate names in Excel
print("\n\n" + "=" * 100)
print("DUPLICATE CHECK IN EXCEL")
print("=" * 100)
name_counts = {}
for pd in excel['ProcedureDetails']:
    name = pd.get('NameEn', '')
    name_counts[name] = name_counts.get(name, 0) + 1

dupes = {k: v for k, v in name_counts.items() if v > 1}
print(f"\nDuplicate NameEn values: {len(dupes)}")
for name, count in sorted(dupes.items(), key=lambda x: -x[1]):
    print(f"  '{name}' appears {count} times")

conn.close()

