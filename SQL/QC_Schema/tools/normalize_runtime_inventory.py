#!/usr/bin/env python3
"""Build the effective PCTP runtime inventory from A-K plus reviewed corrections.

The inventory is a logical object list, not a SQL Server case-sensitive catalog.  SQL
object names are therefore matched by schema-qualified name with case folded.  When
A-K contains the same logical object more than once (for example
Purchase_Order_YMVN/PURCHASE_ORDER_YMVN), the rows are merged instead of failing.
"""
from __future__ import annotations

import argparse
import csv
from pathlib import Path

NAME = "Tên đối tượng"
FIELDS_TO_MERGE = (
    "Loại đối tượng",
    "Database/schema",
    "Repository/caller",
    "SQL thao tác",
    "Khách hàng",
    "Được tạo bởi script nào",
    "Có trên DB57?",
    "Có trong schema Git?",
    "Runtime hay test-only",
    "Trạng thái xác minh",
)
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


def read_rows(path: Path):
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def object_key(name: str) -> str:
    """Return a logical SQL object key independent of dbo prefix and case."""
    value = (name or "").strip()
    if not value:
        return ""
    # Inventory may use dbo.X, [dbo].[X], or just X.
    value = value.replace("[", "").replace("]", "")
    parts = [part.strip() for part in value.split(".") if part.strip()]
    return (".".join(parts[-2:]) if len(parts) >= 2 else parts[-1]).casefold()


def canonical_name(name: str) -> str:
    """Keep a clean display name while preserving the first inventory spelling."""
    value = (name or "").strip().replace("[", "").replace("]", "")
    parts = [part.strip() for part in value.split(".") if part.strip()]
    return parts[-1] if parts else value


def split_values(value: str):
    return [part.strip() for part in (value or "").split(";") if part.strip()]


def merge_field(left: str, right: str) -> str:
    """Merge inventory evidence without duplicating semicolon-separated values."""
    if not left:
        return right or ""
    if not right:
        return left

    left_parts = split_values(left)
    right_parts = split_values(right)
    if len(left_parts) == 1 and len(right_parts) == 1:
        if left_parts[0].casefold() == right_parts[0].casefold():
            return left
        # For status/type fields the first curated value is authoritative.  Evidence
        # fields are handled as unions below.
        return left

    result = list(left_parts)
    seen = {item.casefold() for item in result}
    for item in right_parts:
        if item.casefold() not in seen:
            result.append(item)
            seen.add(item.casefold())
    return "; ".join(result)


def merge_rows(target: dict, incoming: dict) -> dict:
    """Merge duplicate logical-object evidence into target."""
    for field in FIELDS_TO_MERGE:
        if field not in target:
            continue
        if field in {"Repository/caller", "SQL thao tác", "Khách hàng"}:
            target[field] = merge_field(target.get(field, ""), incoming.get(field, ""))
        elif not target.get(field) or target[field].strip() in {"Chưa xác định", "Chưa đối chiếu", "Chưa xác nhận"}:
            if incoming.get(field):
                target[field] = incoming[field]
    target[NAME] = canonical_name(target.get(NAME) or incoming.get(NAME, ""))
    return target


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

    # First normalize A-K itself into one logical row per SQL object.  This is
    # deliberately case-insensitive so Purchase_Order_YMVN and PURCHASE_ORDER_YMVN
    # cannot become two runtime objects.
    by_name: dict[str, dict] = {}
    normalized_base: list[dict] = []
    duplicate_base = 0
    for row in base:
        name = canonical_name(row.get(NAME, ""))
        key = object_key(name)
        if not key:
            continue
        row[NAME] = name
        if key in by_name:
            merge_rows(by_name[key], row)
            duplicate_base += 1
            continue
        by_name[key] = row
        normalized_base.append(row)

    promoted = 0
    added = 0
    correction_keys = {name.casefold() for name in FORCE_RUNTIME}
    for correction in corrections:
        name = canonical_name(correction.get(NAME, ""))
        correction[NAME] = name
        key = object_key(name)
        if not key:
            continue

        if key in by_name:
            if key in {x.casefold() for x in FORCE_RUNTIME}:
            target = by_name[key]
            merge_rows(target, correction)
            if key.split(".")[-1] in correction_keys:
                # The correction manifest is authoritative for Runtime promotion.
                for field in (
                    "Loại đối tượng",
                    "Database/schema",
                    "Runtime hay test-only",
                    "Có trên DB57?",
                    "Trạng thái xác minh",
                ):
                    if correction.get(field):
                        target[field] = correction[field]
                promoted += 1
            continue

        by_name[key] = correction.copy()
        normalized_base.append(correction.copy())
        added += 1

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8-sig", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=fields)
        writer.writeheader()
        writer.writerows(normalized_base)

    print(
        "PCTP runtime inventory: "
        f"{len(normalized_base)} rows; "
        f"merged_duplicates={duplicate_base}; added={added}; promoted={promoted}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
