#!/usr/bin/env python3
"""Check the correct ProcessGroup -> MainProcess -> SubProcess hierarchy from Excel."""
import json

with open(r'ExcelReader\analysis_output.json', 'r', encoding='utf-8') as f:
    excel = json.load(f)

# Build the correct hierarchy from ProcedureDetails
hierarchy = {}  # ProcessGroup -> MainProcess -> SubProcess -> [Procedures]

for pd in excel['ProcedureDetails']:
    pg = pd.get('ProcessGroup', '') or '(empty)'
    mp = pd.get('MainProcessAr', '') or '(empty)'
    sp = pd.get('SubProcessAr', '') or '(empty)'
    proc = pd.get('NameAr', '')
    
    if pg not in hierarchy:
        hierarchy[pg] = {}
    if mp not in hierarchy[pg]:
        hierarchy[pg][mp] = {}
    if sp not in hierarchy[pg][mp]:
        hierarchy[pg][mp][sp] = []
    hierarchy[pg][mp][sp].append(proc)

print("=" * 100)
print("CORRECT HIERARCHY FROM EXCEL (ProcessGroup -> MainProcess -> SubProcess -> Procedures)")
print("=" * 100)

for pg, mps in hierarchy.items():
    print(f"\n{'='*80}")
    print(f"PROCESS GROUP: {pg}")
    print(f"{'='*80}")
    for mp, sps in mps.items():
        total_procs = sum(len(procs) for procs in sps.values())
        print(f"\n  MAIN PROCESS: {mp} ({total_procs} procedures)")
        for sp, procs in sps.items():
            print(f"    SUB-PROCESS: {sp} ({len(procs)} procedures)")
            for p in procs[:3]:
                print(f"      - {p}")
            if len(procs) > 3:
                print(f"      ... and {len(procs)-3} more")

print("\n\n" + "=" * 100)
print("SUMMARY: Which MainProcess belongs to which ProcessGroup?")
print("=" * 100)
for pg, mps in hierarchy.items():
    print(f"\n  ProcessGroup: {pg}")
    for mp in mps.keys():
        print(f"    -> MainProcess: {mp}")

