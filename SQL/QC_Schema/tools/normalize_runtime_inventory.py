#!/usr/bin/env python3
"""Build the effective PCTP runtime inventory from A-K plus reviewed corrections."""
from __future__ import annotations
import argparse, csv
from pathlib import Path

NAME = "Tên đối tượng"
FORCE_RUNTIME = {
    "FVN_PXLB_ReworkPlan", "FVN_PXLB_ReworkMovement", "FVN_PXLB_ReworkQC",
    "FVN_PXLB_Disposition", "FVN_PXLB_Compensation", "FVN_PXLB_CompensationMovement",
    "FVN_PXLB_WorkflowAudit", "FVN_PhieuXuLyBatThuongQCInitial",
}

def read_rows(path: Path):
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--inventory", type=Path, required=True)
    ap.add_argument("--corrections", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    args = ap.parse_args()
    base = read_rows(args.inventory)
    corrections = read_rows(args.corrections)
    if not base:
        raise SystemExit("A-K inventory is empty")
    fields = list(base[0])
    if not corrections or set(corrections[0]) != set(fields):
        raise SystemExit("scope correction manifest header does not match A-K inventory")
    by_name = {}
    for row in base:
        key = row.get(NAME, "").strip().split(".")[-1].casefold()
        if not key:
            continue
        if key in by_name:
            raise SystemExit(f"duplicate inventory object: {row[NAME]}")
        by_name[key] = row
    promoted = 0
    added = 0
    for correction in corrections:
        name = correction[NAME].strip()
        correction[NAME] = name
        key = name.split(".")[-1].casefold()
        if key in by_name:
            if key in {x.casefold() for x in FORCE_RUNTIME}:
                target = by_name[key]
                for field in ("Loại đối tượng", "Database/schema", "Runtime hay test-only", "Có trên DB57?", "Trạng thái xác minh"):
                    target[field] = correction[field]
                promoted += 1
            continue
        by_name[key] = correction.copy()
        base.append(correction.copy())
        added += 1
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8-sig", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=fields)
        writer.writeheader(); writer.writerows(base)
    print(f"PCTP runtime inventory: {len(base)} rows; added={added}; promoted={promoted}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
