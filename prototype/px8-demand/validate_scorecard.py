#!/usr/bin/env python3
"""Validate the PX8 demand scorecard before analysis.

Usage:
  python validate_scorecard.py --csv demand-scorecard.csv
Exit code 0 if no errors, 1 if errors. Warnings do not fail.
"""
from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

EXPECTED = [
    "participant_id", "source", "segment", "browser", "tabs", "extensions",
    "google_login", "netflix", "memory_pain", "restart_pain", "switch_intent",
    "rank_1", "rank_2", "constraints_fatal", "wtp", "prototype_task_done",
    "prototype_sus", "retained_1w", "attention_ok", "notes",
]
SEGMENTS = {"heavy_tabs", "privacy", "edge_chrome", "other"}
TABS = {"1-4", "5-9", "10-19", "20+"}
RANKS = {"memory", "privacy", "speed", "extensions", "compat", "ui", "other", ""}
WTP = {"none", "free", "500mo", "1000mo", "1000mo+", "3000once", "other", ""}
YESNO = {"yes", "no", ""}
ZEROONE = {"0", "1", ""}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", required=True)
    args = ap.parse_args()
    path = Path(args.csv)
    if not path.exists():
        print(f"ERROR: not found: {path}")
        raise SystemExit(1)

    with path.open(encoding="utf-8", newline="") as fh:
        reader = csv.DictReader(fh)
        header = reader.fieldnames or []
        rows = list(reader)

    errors: list[str] = []
    warnings: list[str] = []
    if header != EXPECTED:
        errors.append(f"header mismatch: expected {EXPECTED}, got {header}")

    seen: set[str] = set()
    for index, row in enumerate(rows, start=2):
        pid = (row.get("participant_id") or "").strip()
        if not pid:
            errors.append(f"line {index}: empty participant_id")
        elif pid in seen:
            errors.append(f"line {index}: duplicate participant_id {pid}")
        else:
            seen.add(pid)

        segment = (row.get("segment") or "").strip()
        if segment not in SEGMENTS:
            errors.append(f"line {index}: invalid segment '{segment}'")
        tabs = (row.get("tabs") or "").strip()
        if tabs not in TABS:
            errors.append(f"line {index}: invalid tabs '{tabs}'")

        for field in ("google_login", "netflix", "memory_pain", "restart_pain", "switch_intent"):
            value = (row.get(field) or "").strip()
            if value == "":
                continue
            try:
                n = int(value)
            except ValueError:
                errors.append(f"line {index}: {field} not an integer '{value}'")
                continue
            if not 1 <= n <= 5:
                errors.append(f"line {index}: {field} out of range 1-5: {n}")

        for field in ("rank_1", "rank_2"):
            value = (row.get(field) or "").strip().lower()
            if value not in RANKS:
                errors.append(f"line {index}: invalid {field} '{value}'")

        fatal = (row.get("constraints_fatal") or "").strip()
        if fatal != "":
            try:
                n = int(fatal)
                if not 0 <= n <= 4:
                    errors.append(f"line {index}: constraints_fatal out of range 0-4: {n}")
            except ValueError:
                errors.append(f"line {index}: constraints_fatal not an integer '{fatal}'")

        wtp = (row.get("wtp") or "").strip().lower()
        if wtp not in WTP:
            errors.append(f"line {index}: invalid wtp '{wtp}'")

        done = (row.get("prototype_task_done") or "").strip().lower()
        if done not in YESNO:
            errors.append(f"line {index}: prototype_task_done must be yes/no/blank: '{done}'")
        retained = (row.get("retained_1w") or "").strip()
        if retained not in ZEROONE:
            errors.append(f"line {index}: retained_1w must be 0/1/blank: '{retained}'")

        sus = (row.get("prototype_sus") or "").strip()
        if sus != "":
            try:
                n = int(sus)
                if not 0 <= n <= 100:
                    errors.append(f"line {index}: prototype_sus out of range 0-100: {n}")
            except ValueError:
                errors.append(f"line {index}: prototype_sus not an integer '{sus}'")

        attention = (row.get("attention_ok") or "").strip()
        if attention not in {"0", "1"}:
            errors.append(f"line {index}: attention_ok must be 0 or 1: '{attention}'")
        elif attention == "0":
            warnings.append(f"line {index}: attention check failed; excluded from analysis")

        if (row.get("participant_id") or "").strip() and not (row.get("source") or "").strip():
            warnings.append(f"line {index}: source is empty")

    print(f"rows={len(rows)} errors={len(errors)} warnings={len(warnings)}")
    for item in errors:
        print("ERROR:", item)
    for item in warnings:
        print("WARN:", item)
    if errors:
        raise SystemExit(1)
    print("OK")


if __name__ == "__main__":
    main()
