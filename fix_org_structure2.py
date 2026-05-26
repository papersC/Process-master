#!/usr/bin/env python3
"""Fix remaining duplicate Level 1 units (SEC-* vs seed data codes)."""
import pyodbc

conn = pyodbc.connect(r'DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=ESEMS;Trusted_Connection=yes;')
cursor = conn.cursor()

# Get all current units by code
cursor.execute("SELECT Id, NameAr, NameEn, Level, Code, ParentId FROM OrganizationUnits")
by_code = {u.Code: u for u in cursor.fetchall()}

# Remaining duplicates: merge SEC-* into seed data codes
remaining_dups = {
    'SEC-S&D': 'STR',    # إدارة الاستراتيجية والتطوير
    'SEC-IA&': 'IAO',    # مكتب التدقيق الداخلي والمخاطر
    'SEC-LAO': 'LEG',    # مكتب الشؤون القانونية
    'SEC-CEO': 'CEO',    # مكتب المدير التنفيذي
}

deleted = 0
for dup_code, keep_code in remaining_dups.items():
    dup = by_code.get(dup_code)
    keep = by_code.get(keep_code)
    if dup and keep:
        # Reassign children OrganizationUnits
        cursor.execute("UPDATE OrganizationUnits SET ParentId = ? WHERE ParentId = ?", keep.Id, dup.Id)
        cnt = cursor.rowcount
        # Reassign ProcessTasks
        cursor.execute("UPDATE ProcessTasks SET OwningUnitId = ? WHERE OwningUnitId = ?", keep.Id, dup.Id)
        pt_cnt = cursor.rowcount
        # Delete the duplicate
        cursor.execute("DELETE FROM OrganizationUnits WHERE Id = ?", dup.Id)
        deleted += 1
        print(f"Merged [{dup_code}] -> [{keep_code}]: {cnt} children moved, {pt_cnt} tasks moved")
    elif dup:
        print(f"WARNING: [{dup_code}] exists but [{keep_code}] not found")
    elif keep:
        print(f"OK: [{dup_code}] already removed, [{keep_code}] exists")

conn.commit()
print(f"\nDeleted {deleted} remaining duplicates")

# Verify final structure
print("\n" + "=" * 80)
print("FINAL STRUCTURE")
print("=" * 80)
cursor.execute("""
    SELECT ou.NameAr, ou.NameEn, ou.Level, ou.Code, p.NameAr as ParentNameAr
    FROM OrganizationUnits ou
    LEFT JOIN OrganizationUnits p ON ou.ParentId = p.Id
    ORDER BY ou.Level, ou.NameAr
""")
for u in cursor.fetchall():
    indent = "  " * u.Level
    parent = f" -> {u.ParentNameAr}" if u.ParentNameAr else ""
    print(f"{indent}[L{u.Level}] [{u.Code}] {u.NameAr}{parent}")

cursor.execute("SELECT Level, COUNT(*) as Cnt FROM OrganizationUnits GROUP BY Level ORDER BY Level")
print("\nCounts by level:")
for r in cursor.fetchall():
    print(f"  Level {r.Level}: {r.Cnt}")

cursor.execute("SELECT COUNT(*) FROM OrganizationUnits")
print(f"\nTotal units: {cursor.fetchone()[0]}")
conn.close()

