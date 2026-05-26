#!/usr/bin/env python3
"""
Fix the OrganizationUnit structure based on the org chart images.

CORRECT STRUCTURE (from images):
=================================
Top Level (no sector): المدير التنفيذي (CEO) - not stored as unit, but direct reports are:
  - مكتب التدقيق الداخلي والمخاطر (Office - reports to CEO)
  - مكتب المدير التنفيذي (Office - reports to CEO)
  - مكتب الشؤون القانونية (Office - reports to CEO)
  - إدارة الاستراتيجية والتطوير (Department - reports to CEO)
    Sections: الاستراتيجية والأداء, التميز والريادة المؤسسية, السياسات الإسكانية, الدراسات المجتمعية

Sector: قطاع الإسكان (Housing Sector)
  - إدارة المشاريع الهندسية
    Sections: التخطيط والتصميم, الرقابة الهندسية, الصيانة, الأصول
  - إدارة إسعاد المتعاملين
    Sections: ريادة الخدمات, عناية المتعاملين, معالجة الطلبات
  - إدارة الاستثمار
    Sections: استدامة الأعمال, الشراكات الإسكانية
  - إدارة الفروع الخارجية
    Sub-units: مركز إسكان دبي المتكامل, مراكز التعهيد

Sector: قطاع الدعم المؤسسي (Corporate Support Sector)
  - إدارة الخدمات المساندة
    Sections: التخطيط والموازنة, الإيرادات والتحصيل, العقود والمشتريات, الموارد البشرية, الشؤون الإدارية
  - إدارة التحول الرقمي
    Sections: تطوير النظم والخدمات الذكية, خدمات الدعم التقني
  - إدارة الاتصال والتسويق
    Sections: الاتصال, التسويق

ISSUES TO FIX:
1. Duplicate units: Many departments/sections exist twice (once from Excel import, once from seed data)
2. إدارة الاستراتيجية والتطوير is Level 0 (Sector) but should be Level 1 (Department) under CEO
3. مكتب التدقيق الداخلي والمخاطر is Level 0 but should be Level 1 (Department/Office) under CEO
4. مكتب الشؤون القانونية is Level 0 but should be Level 1 under CEO
5. مكتب المدير التنفيذي is Level 0 but should be Level 1 under CEO
6. MBRHE root unit is unnecessary - the CEO offices/depts should be under a CEO-level unit
7. Duplicate sections with slightly different names/codes need to be merged
"""
import pyodbc
import uuid

conn = pyodbc.connect(r'DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=ESEMS;Trusted_Connection=yes;')
cursor = conn.cursor()

# Step 1: Get all current units
cursor.execute("SELECT Id, NameAr, NameEn, Level, Code, ParentId FROM OrganizationUnits")
all_units = {u.Id: u for u in cursor.fetchall()}
by_code = {u.Code: u for u in all_units.values()}
by_name_ar = {}
for u in all_units.values():
    if u.NameAr not in by_name_ar:
        by_name_ar[u.NameAr] = []
    by_name_ar[u.NameAr].append(u)

print(f"Current units: {len(all_units)}")

# Step 2: Identify the CORRECT structure IDs
# We'll keep the well-structured units (DEP-*, SCT-*, SEC-HS, SEC-CSS) and remove duplicates

# The two real sectors
housing_sector = by_code.get('SEC-HS')  # قطاع الإسكان
support_sector = by_code.get('SEC-CSS')  # قطاع الدعم المؤسسي

# Create a CEO-level unit to hold the direct reports
ceo_id = str(uuid.uuid4())
cursor.execute("""
    INSERT INTO OrganizationUnits (Id, NameEn, NameAr, Code, Level, ParentId, DisplayOrder,
        IsActive, CreatedAt, UpdatedAt, Version, IsDeleted)
    VALUES (?, 'CEO Office & Direct Reports', 'المدير التنفيذي', 'SEC-EXEC', 0, NULL, 0,
        1, GETUTCDATE(), GETUTCDATE(), 1, 0)
""", ceo_id)
print(f"Created CEO unit: {ceo_id}")

# Step 3: Fix the 4 CEO direct-report offices/departments
# These should be Level 1 under the CEO unit

# مكتب التدقيق الداخلي والمخاطر - currently Level 0 (SEC-IA&)
audit_sector = by_code.get('SEC-IA&')
if audit_sector:
    cursor.execute("UPDATE OrganizationUnits SET Level = 1, ParentId = ? WHERE Id = ?", ceo_id, audit_sector.Id)
    print(f"Fixed: مكتب التدقيق الداخلي والمخاطر -> Level 1 under CEO")

# مكتب الشؤون القانونية - currently Level 0 (SEC-LAO)
legal_sector = by_code.get('SEC-LAO')
if legal_sector:
    cursor.execute("UPDATE OrganizationUnits SET Level = 1, ParentId = ? WHERE Id = ?", ceo_id, legal_sector.Id)
    print(f"Fixed: مكتب الشؤون القانونية -> Level 1 under CEO")

# مكتب المدير التنفيذي - currently Level 0 (SEC-CEO)
ceo_office_sector = by_code.get('SEC-CEO')
if ceo_office_sector:
    cursor.execute("UPDATE OrganizationUnits SET Level = 1, ParentId = ? WHERE Id = ?", ceo_id, ceo_office_sector.Id)
    print(f"Fixed: مكتب المدير التنفيذي -> Level 1 under CEO")

# إدارة الاستراتيجية والتطوير - currently Level 0 (SEC-S&D)
strategy_sector = by_code.get('SEC-S&D')
if strategy_sector:
    cursor.execute("UPDATE OrganizationUnits SET Level = 1, ParentId = ? WHERE Id = ?", ceo_id, strategy_sector.Id)
    print(f"Fixed: إدارة الاستراتيجية والتطوير -> Level 1 under CEO")

# Step 4: Fix departments that are under MBRHE root - reassign to correct sectors
mbrhe = by_code.get('MBRHE')

# Map of department codes (old seed data) -> correct parent sector
dept_reassign = {
    # Housing Sector departments
    'ENG': housing_sector.Id if housing_sector else None,   # إدارة المشاريع الهندسية
    'CHP': housing_sector.Id if housing_sector else None,   # إدارة إسعاد المتعاملين
    'INV': housing_sector.Id if housing_sector else None,   # إدارة الاستثمار
    'EXT': housing_sector.Id if housing_sector else None,   # إدارة الفروع الخارجية
    # Corporate Support Sector departments
    'SSV': support_sector.Id if support_sector else None,   # إدارة الخدمات المساندة
    'DIG': support_sector.Id if support_sector else None,   # إدارة التحول الرقمي
    'COM': support_sector.Id if support_sector else None,   # إدارة الاتصال والتسويق
    # CEO direct reports (old seed data depts)
    'STR': ceo_id,   # إدارة الاستراتيجية والتطوير
    'IAO': ceo_id,   # مكتب التدقيق الداخلي والمخاطر
    'LEG': ceo_id,   # مكتب الشؤون القانونية
    'CEO': ceo_id,   # مكتب المدير التنفيذي
}

for code, parent_id in dept_reassign.items():
    unit = by_code.get(code)
    if unit and parent_id:
        cursor.execute("UPDATE OrganizationUnits SET ParentId = ? WHERE Id = ?", parent_id, unit.Id)
        print(f"Reassigned [{code}] {unit.NameAr} -> correct parent")

# Step 5: Remove duplicate departments (DEP-* codes that duplicate the seed data ones)
# These were created during Excel import and duplicate the original seed data departments
duplicates_to_remove = []

# Find DEP-* units that are duplicates of existing departments
dup_map = {
    'DEP-PED': 'ENG',    # إدارة المشاريع الهندسية (duplicate)
    'DEP-CHD': 'CHP',    # إدارة إسعاد المتعاملين (duplicate)
    'DEP-IMD': 'INV',    # إدارة الاستثمار (duplicate)
    'DEP-IMD2': 'INV',   # إدارة الاستثمار (another duplicate, no parent)
    'DEP-IA&': 'IAO',    # مكتب التدقيق الداخلي والمخاطر (duplicate)
    'DEP-CEO': 'CEO',    # مكتب المدير التنفيذي (duplicate)
}

# For each duplicate, reassign its children to the original, then delete it
for dup_code, orig_code in dup_map.items():
    dup_unit = by_code.get(dup_code)
    orig_unit = by_code.get(orig_code)
    if dup_unit and orig_unit:
        # Reassign children
        cursor.execute("UPDATE OrganizationUnits SET ParentId = ? WHERE ParentId = ?", orig_unit.Id, dup_unit.Id)
        # Reassign ProcessTasks OwningUnitId
        cursor.execute("UPDATE ProcessTasks SET OwningUnitId = ? WHERE OwningUnitId = ?", orig_unit.Id, dup_unit.Id)
        duplicates_to_remove.append(dup_unit.Id)
        print(f"Merged [{dup_code}] into [{orig_code}]")

# Step 6: Remove duplicate sections (SCT-* codes that duplicate existing sections)
dup_sections = {
    'SCT-AMS': 'ENG-AS',    # قسم الأصول (duplicate)
    'SCT-EOS': 'ENG-ES',    # قسم الرقابة الهندسية (duplicate)
    'SCT-PP&': 'ENG-PD',    # قسم التخطيط والتصميم (duplicate)
    'SCT-R&C': 'SSV-RC',    # قسم الإيرادات والتحصيل (duplicate)
    'SCT-P&C': 'SSV-CP',    # قسم العقود والمشتريات (duplicate)
    'SCT-AAS': 'SSV-AD',    # قسم الشؤون الإدارية (duplicate)
    'SCT-SD&': 'DIG-SD',    # قسم تطوير النظم والخدمات الذكية (duplicate)
    'SCT-SPS': 'CHP-SE',    # قسم ريادة الخدمات (duplicate)
    'SCT-APS': 'CHP-RP',    # قسم معالجة الطلبات (duplicate)
    'SCT-HPS': 'INV-HP',    # قسم الشراكات الإسكانية (duplicate)
    'SCT-HPS2': 'STR-HP',   # قسم السياسات الإسكانية (duplicate)
}

for dup_code, orig_code in dup_sections.items():
    dup_unit = by_code.get(dup_code)
    orig_unit = by_code.get(orig_code)
    if dup_unit and orig_unit:
        cursor.execute("UPDATE ProcessTasks SET OwningUnitId = ? WHERE OwningUnitId = ?", orig_unit.Id, dup_unit.Id)
        duplicates_to_remove.append(dup_unit.Id)
        print(f"Merged section [{dup_code}] into [{orig_code}]")

# Also handle the 3 office-level duplicates at section level
office_dup_sections = {
    'SCT-IA&': audit_sector.Id if audit_sector else None,   # duplicate section under audit
    'SCT-LAO': legal_sector.Id if legal_sector else None,   # duplicate section under legal
    'SCT-CEO': ceo_office_sector.Id if ceo_office_sector else None,  # duplicate section under CEO office
}
for dup_code, orig_id in office_dup_sections.items():
    dup_unit = by_code.get(dup_code)
    if dup_unit and orig_id:
        cursor.execute("UPDATE ProcessTasks SET OwningUnitId = ? WHERE OwningUnitId = ?", orig_id, dup_unit.Id)
        duplicates_to_remove.append(dup_unit.Id)
        print(f"Merged office section [{dup_code}] into parent")

# Step 7: Delete all duplicate units
for dup_id in duplicates_to_remove:
    cursor.execute("DELETE FROM OrganizationUnits WHERE Id = ?", dup_id)
print(f"\nDeleted {len(duplicates_to_remove)} duplicate units")

# Step 8: Remove the MBRHE root unit (no longer needed)
if mbrhe:
    # First reassign any remaining children
    cursor.execute("UPDATE OrganizationUnits SET ParentId = NULL WHERE ParentId = ? AND Level = 0", mbrhe.Id)
    cursor.execute("SELECT COUNT(*) FROM OrganizationUnits WHERE ParentId = ?", mbrhe.Id)
    remaining = cursor.fetchone()[0]
    if remaining == 0:
        cursor.execute("DELETE FROM OrganizationUnits WHERE Id = ?", mbrhe.Id)
        print(f"Deleted MBRHE root unit")
    else:
        print(f"WARNING: MBRHE still has {remaining} children, not deleting")

conn.commit()

# Step 9: Verify final structure
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

cursor.execute("SELECT COUNT(*) FROM OrganizationUnits")
total = cursor.fetchone()[0]
print(f"\nTotal units: {total}")

conn.close()

