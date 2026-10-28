#!/usr/bin/env python3
"""Build the effective PCTP runtime inventory from A-K plus reviewed corrections.

SQL object names are matched by case-folded logical name, ignoring brackets and a
dbo prefix. Duplicate rows are merged; reviewed corrections update existing rows
instead of appending a second copy.
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
    "fvn_pxlb_reworkplan",
    "fvn_pxlb_reworkmovement",
    "fvn_pxlb_reworkqc",
    "fvn_pxlb_disposition",
    "fvn_pxlb_compensation",
    "fvn_pxlb_compensationmovement",
    "fvn_pxlb_workflowaudit",
    "fvn_phieuxulybatthuongqcinitial",
}
UNKNOWN_VALUES = {"chưa xác định", "chưa đối chiếu", "chưa xác nhận", "chưa có ddl git"}


def read_rows(path: Path):
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def object_key(name: str) -> str:
    """Logical key independent of schema prefix, brackets, and identifier case."""
    value = (name or "").strip().replace("[", "").replace("]", "")
    parts = [part.strip() for part in value.split(".") if part.strip()]
    if not parts:
        return ""
    # Inventory is scoped to dbo; schema-qualified and unqualified names should
    # match, while non-dbo schemas retain their schema in the key.
    if len(parts) >= 2:
        schema, obj = parts[-2], parts[-1]
        return (obj if schema.casefold() == "dbo" else f"{schema}.{obj}").casefold()
    return parts[-1].casefold()


def canonical_name(name: str) -> str:
    value = (name or "").strip().replace("[", "").replace("]", "")
    parts = [part.strip() for part in value.split(".") if part.strip()]
    return parts[-1] if parts else value


def split_values(value: str):
    return [part.strip() for part in (value or "").split(";") if part.strip()]


def merge_evidence(left: str, right: str) -> str:
    """Union semicolon-separated evidence without losing distinct callers/operations."""
    if not left:
        return right or ""
    if not right:
        return left
    result = split_values(left)
    seen = {item.casefold() for item in result}
    for item in split_values(right):
        if item.casefold() not in seen:
            result.append(item)
            seen.add(item.casefold())
    return "; ".join(result)


def merge_rows(target: dict, incoming: dict) -> dict:
    """Merge evidence and fill unknown metadata without replacing known facts."""
    evidence_fields = {"Repository/caller", "SQL thao tác", "Khách hàng"}
    for field in FIELDS_TO_MERGE:
        left = (target.get(field) or "").strip()
        right = (incoming.get(field) or "").strip()
        if not right:
            continue
        if field in evidence_fields:
            target[field] = merge_evidence(left, right)
        elif not left or left.casefold() in UNKNOWN_VALUES:
            target[field] = right
        elif left.casefold() != right.casefold() and field in {
            "Được tạo bởi script nào", "Có trong schema Git?"
        }:
            target[field] = merge_evidence(left, right)
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

    by_name: dict[str, dict] = {}
    normalized: list[dict] = []
    duplicate_base = 0
    for source_row in base:
        row = source_row.copy()
        name = canonical_name(row.get(NAME, ""))
        key = object_key(row.get(NAME, ""))
        if not key:
            continue
        row[NAME] = name
        if key in by_name:
            merge_rows(by_name[key], row)
            duplicate_base += 1
        else:
            by_name[key] = row
            normalized.append(row)

    promoted = 0
    added = 0
    for source_correction in corrections:
        correction = source_correction.copy()
        key = object_key(correction.get(NAME, ""))
        if not key:
            continue
        correction[NAME] = canonical_name(correction.get(NAME, ""))

        if key in by_name:
            target = by_name[key]
            merge_rows(target, correction)
            runtime_value = (correction.get("Runtime hay test-only") or "").strip()
            if key in FORCE_RUNTIME or runtime_value.casefold().startswith("runtime"):
                # A reviewed correction is authoritative for runtime classification.
                if runtime_value:
                    target["Runtime hay test-only"] = runtime_value
                for field in ("Loại đối tượng", "Database/schema", "Có trên DB57?", "Trạng thái xác minh"):
                    value = (correction.get(field) or "").strip()
                    if value and value.casefold() not in UNKNOWN_VALUES:
                        target[field] = value
                promoted += 1
            continue

        by_name[key] = correction
        normalized.append(correction)
        added += 1

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8-sig", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(normalized)

    print(
        "PCTP runtime inventory: "
        f"{len(normalized)} rows; "
        f"merged_duplicates={duplicate_base}; added={added}; promoted={promoted}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
