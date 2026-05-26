#!/usr/bin/env python3
"""Compare database OrganizationUnits with the org chart from images."""
import pyodbc

conn = pyodbc.connect(r'DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=ESEMS;Trusted_Connection=yes;')
cursor = conn.cursor()

cursor.execute("""
    SELECT ou.Id, ou.NameAr, ou.NameEn, ou.Level, ou.Code, 
           p.NameAr as ParentNameAr, ou.ParentId
    FROM OrganizationUnits ou
    LEFT JOIN OrganizationUnits p ON ou.ParentId = p.Id
    ORDER BY ou.Level, ou.NameAr
""")
units = cursor.fetchall()

print(f"Total OrganizationUnits: {len(units)}")
print()

for level in range(5):
    level_units = [u for u in units if u.Level == level]
    level_names = {0: "Sector (Level 0)", 1: "Department (Level 1)", 2: "Section (Level 2)", 
                   3: "Function (Level 3)", 4: "SubFunction (Level 4)"}
    print(f"\n{'='*80}")
    print(f"{level_names.get(level, f'Level {level}')}: {len(level_units)} units")
    print(f"{'='*80}")
    for u in level_units:
        parent = f" -> Parent: {u.ParentNameAr}" if u.ParentNameAr else ""
        print(f"  [{u.Code}] {u.NameAr} | {u.NameEn}{parent}")

conn.close()

