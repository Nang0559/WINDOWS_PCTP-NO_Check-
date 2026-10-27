#!/usr/bin/env python3
"""Build the effective PCTP runtime inventory from the A-K inventory plus reviewed corrections.

B7R2_FCC is a shared database. The A-K inventory remains the primary audit document, while
this small correction manifest prevents known runtime objects from being silently omitted from
PCTP-only CI schema generation. The script never discovers arbitrary B7R2 tables.

It also promotes the reviewed FVN_PXLB_* objects from the old "unverified/no caller" state to
Runtime because IQTChungService/QTChung flow contains direct SQL against those tables.
"""
from __future__ import annotations

import argparse
import csv
from pathlib import Path

NAME = "Tên đối tượng"
RUNTIME = "Runtime"
FORCE_RUNTIME = {
    "FVN_PXLB_ReworkPlan",
    "FVN_PXLB_ReworkMovement",
    "FVN_PXLB_ReworkQC",
    "FVN_PXLB_Disposition",
    "FVN_PXLB_Compensation",
    "FVN_PXLB_CompensationMovement",
    "FVN_PXLB_WorkflowAudit",
    "FVN_PhieuXuLyBatThuongQCInitial",
}


def rows(path: Path):
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--inventory", type=Path, required=True)
    ap.add_argument("--corrections", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    args = ap.parse_args()

    base = rows(args.inventory)
    corrections = rows(args.corrections)
    if not base:
        raise SystemExit("A-K inventory is empty")
    fields = list(base[0].keys())
    required_fields = set(fields)
    if set(corrections[0]) != required_fields:
        raise SystemExit("Scope correction manifest header does not match A-K inventory header")

    by_name = {}
    for row in base:
        name = row.get(NAME, "").strip()
        if not name:
            continue
        key = name.split(".")[-1].casefold()
        if key in by_name:
            raise SystemExit(f"Duplicate inventory object: {name}")
        by_name[key] = row

    added = 0
    promoted = 0
    for correction in corrections:
        name = correction[NAME].strip()
        key = name.split(".")[-1].casefold()
        if key in by_name:
            target = by_name[key]
            # A correction for an existing row is an explicit review decision; do not replace
            # unrelated audit text, only the fields that define PCTP runtime scope.
            if key in {x.casefold() for x in FORCE_RUNTIME}:
                target["Loại đối tượng"] = correction["Loại đối tượng"]
                target["Database/schema"] = correction["Database/schema"]
                target["Runtime hay test-only"] = RUNTIME
                target["Trạng thái xác minh"] = correction["Trạng thái xác minh"]
                promoted += 1
            continue
        by_name[key] = correction.copy()
        base.append(correction.copy())
        added += 1

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8-sig", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=fields)
        writer.writeheader()
        writer.writerows(base)

    print(f"PCTP runtime inventory: {len(base)} rows; added={added}; promoted={promoted}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
