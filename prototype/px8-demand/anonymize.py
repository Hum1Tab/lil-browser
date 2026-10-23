#!/usr/bin/env python3
"""Conservative transcript anonymizer for the PX8 interviews.

Redacts emails, URLs, Windows paths, Japanese phone numbers, @handles,
long digit IDs, and optional names supplied by the researcher. It cannot
detect personal names automatically; manual review is required.

Usage:
  python anonymize.py --in transcript.txt --out transcript.anon.md
  python anonymize.py --in transcript.txt --out transcript.anon.md --names "Tanaka,Suzuki"
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

EMAIL = re.compile(r"[\w.+-]+@[\w-]+\.[\w.-]+")
URL = re.compile(r"https?://[^\s<>\"']+")
WINPATH = re.compile(r"[A-Za-z]:\\[^\s<>\"']+")
PHONE_JP = re.compile(r"\b0\d{1,4}-?\d{1,4}-?\d{3,4}\b")
HANDLE = re.compile(r"(?<![\w@])@[A-Za-z0-9_]{2,}")
LONGNUM = re.compile(r"\b\d{8,}\b")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", dest="dst", required=True)
    ap.add_argument("--names", default="", help="comma-separated names to redact (optional)")
    args = ap.parse_args()

    text = Path(args.src).read_text(encoding="utf-8")
    counts: dict[str, int] = {}

    def sub(pattern: re.Pattern[str], label: str, value: str) -> str:
        def repl(_m: re.Match[str]) -> str:
            counts[label] = counts.get(label, 0) + 1
            return label
        return pattern.sub(repl, value)

    out = sub(EMAIL, "[EMAIL]", text)
    out = sub(URL, "[URL]", out)
    out = sub(WINPATH, "[PATH]", out)
    out = sub(PHONE_JP, "[PHONE]", out)
    out = sub(HANDLE, "[HANDLE]", out)
    out = sub(LONGNUM, "[NUMBER]", out)

    for raw in args.names.split(","):
        name = raw.strip()
        if len(name) >= 2 and name in out:
            count = out.count(name)
            counts["[NAME]"] = counts.get("[NAME]", 0) + count
            out = out.replace(name, "[NAME]")

    header = (
        "# 匿名化済み文字起こし\n\n"
        "- 自動匿名化: email / URL / Windows path / 電話番号 / @handle / 8桁以上の数字"
        + (" / 指定された名前" if args.names else "")
        + "\n- **手動レビュー必須**: 人名、勤務先、地名、文脈から特定できる情報は自動で消えない。\n"
        "- 原文と対応表は保存しない（復元しない）。\n\n"
        "---\n\n"
    )
    Path(args.dst).write_text(header + out, encoding="utf-8")

    print("anonymized:", args.dst)
    for key in sorted(counts):
        print(f"  {key}: {counts[key]}")
    print("WARNING: manual review required for names, employers, places, and context.")


if __name__ == "__main__":
    main()
