#!/usr/bin/env python3
"""Demand scorecard analyzer for the redefined browser.

Reads demand-scorecard.csv and computes the H1-H5 measures from
docs/architecture/17-redefinition-and-demand-validation.md.

No data is bundled. If the CSV has no participants, the script says
NO_DATA instead of inventing a result.
"""
from __future__ import annotations

import argparse
import csv
import json
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

TOP2 = {"memory", "privacy"}
WTP_PAID = {"500mo", "1000mo", "1000mo+", "3000once", "3000", "paid"}


def num(value: str | None) -> float | None:
    if value is None:
        return None
    text = str(value).strip().replace(",", "")
    if text == "":
        return None
    try:
        return float(text)
    except ValueError:
        return None


def truthy(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"1", "yes", "true", "y"}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", default="demand-scorecard.csv")
    ap.add_argument("--visitors", type=int, default=None, help="landing page qualified visitors")
    ap.add_argument("--signups", type=int, default=None, help="landing page signups")
    ap.add_argument("--metrics", default=None, help="metrics.json from server.py (views/signups by variant)")
    ap.add_argument("--out", default="demand-report.md")
    args = ap.parse_args()

    path = Path(args.csv)
    if not path.exists():
        raise SystemExit(f"CSV not found: {path}")

    with path.open(encoding="utf-8", newline="") as fh:
        rows = list(csv.DictReader(fh))

    excluded = [r for r in rows if str(r.get("attention_ok", "")).strip().lower() in {"0", "no", "false"}]
    data = [r for r in rows if r not in excluded]
    n = len(data)

    h1_rows = [r for r in data if r.get("rank_1", "").strip().lower() in TOP2
               or r.get("rank_2", "").strip().lower() in TOP2]
    h2_rows = [r for r in data if num(r.get("constraints_fatal")) is not None
               and num(r.get("constraints_fatal")) <= 2]
    h2_denom = [r for r in data if num(r.get("constraints_fatal")) is not None]

    proto = [r for r in data if truthy(r.get("prototype_task_done"))]
    retained = [r for r in proto if truthy(r.get("retained_1w"))]
    wtp = [r for r in data if str(r.get("wtp", "")).strip().lower() in WTP_PAID]

    switch_vals = [num(r.get("switch_intent")) for r in data]
    switch_vals = [v for v in switch_vals if v is not None]

    h1_rate = len(h1_rows) / n if n else None
    h2_rate = len(h2_rows) / len(h2_denom) if h2_denom else None
    h4_rate = len(retained) / len(proto) if proto else None
    h5_rate = len(wtp) / n if n else None
    h3_rate = (args.signups / args.visitors) if args.visitors and args.signups is not None else None
    h3_visitors = args.visitors
    h3_signups = args.signups
    h3_by_variant = None
    if args.metrics:
        metrics = json.loads(Path(args.metrics).read_text(encoding="utf-8"))
        views = metrics.get("views", {})
        signups = metrics.get("signups", {})
        total_views = sum(int(v or 0) for v in views.values())
        total_signups = sum(int(v or 0) for v in signups.values())
        h3_visitors = total_views
        h3_signups = total_signups
        h3_rate = total_signups / total_views if total_views else None
        h3_by_variant = {}
        for key in ("a", "b"):
            v = int(views.get(key, 0) or 0)
            s = int(signups.get(key, 0) or 0)
            h3_by_variant[key] = {"views": v, "signups": s, "rate": (s / v if v else None)}

    reasons: list[str] = []
    decision = "NO_DATA"

    if n == 0:
        reasons.append("参加者が 0 人。需要の判定はできない。")
    else:
        h1_ok = h1_rate is not None and h1_rate >= 0.60
        h2_ok = h2_rate is not None and h2_rate >= 0.50
        if not h1_ok:
            reasons.append(f"H1 未達: メモリ/プライバシーが上位2つ {len(h1_rows)}/{n}（基準 60%）。")
        if not h2_ok:
            reasons.append(f"H2 未達: 致命的な制約が2つ以下 {len(h2_rows)}/{len(h2_denom)}（基準 50%）。")

        if not h1_ok or not h2_ok:
            decision = "NO_GO"
        else:
            conditional = False
            if h3_rate is not None and h3_rate < 0.08:
                conditional = True
                reasons.append(f"H3 未達: 登録率 {h3_rate:.1%}（基準 8%）。")
            if h4_rate is not None and h4_rate < 0.40:
                conditional = True
                reasons.append(f"H4 未達: 1週間継続 {len(retained)}/{len(proto)}（基準 40%）。")
            if h5_rate is not None and h5_rate < 0.25:
                conditional = True
                reasons.append(f"H5 未達: 支払い意思 {len(wtp)}/{n}（基準 25%）。")
            decision = "CONDITIONAL_GO" if conditional else "GO"
            if not conditional:
                reasons.append("H1/H2 を満たし、H3〜H5 も基準を満たした。")

    summary = {
        "n_rows": len(rows),
        "n_used": n,
        "n_excluded_attention": len(excluded),
        "switch_intent_mean": round(sum(switch_vals) / len(switch_vals), 2) if switch_vals else None,
        "h1_top2_memory_privacy": {"count": len(h1_rows), "n": n, "rate": h1_rate, "threshold": 0.60},
        "h2_constraints_fatal_le2": {"count": len(h2_rows), "n": len(h2_denom), "rate": h2_rate, "threshold": 0.50},
        "h3_landing": {"signups": h3_signups, "visitors": h3_visitors, "rate": h3_rate, "threshold": 0.08,
                       "by_variant": h3_by_variant},
        "h4_retained_1w": {"count": len(retained), "n": len(proto), "rate": h4_rate, "threshold": 0.40},
        "h5_wtp_paid": {"count": len(wtp), "n": n, "rate": h5_rate, "threshold": 0.25},
        "decision": decision,
        "reasons": reasons,
    }

    out_path = Path(args.out)
    out_json = out_path.with_suffix(".json")
    out_json.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    def fmt(value, pct=False):
        if value is None:
            return "未取得"
        return f"{value:.1%}" if pct else str(value)

    lines = [
        "# 需要検証レポート",
        "",
        f"- 入力: `{path}`",
        f"- 回答者: {len(rows)} 人（注意チェック除外: {len(excluded)} 人、分析対象: {n} 人）",
        f"- 乗り換え意欲の平均: {fmt(summary['switch_intent_mean'])} / 5",
        f"- **判定: {decision}**",
        "",
        "| 指標 | 値 | 基準 | 判定 |",
        "|---|---:|---:|---|",
        f"| H1 メモリ/プライバシーが上位2つ | {len(h1_rows)}/{n} ({fmt(h1_rate, True)}) | 60% | {'未取得' if n == 0 or h1_rate is None else ('合格' if h1_rate >= 0.6 else '未達')} |",
        f"| H2 致命的な制約が2つ以下 | {len(h2_rows)}/{len(h2_denom)} ({fmt(h2_rate, True)}) | 50% | {'未取得' if not h2_denom or h2_rate is None else ('合格' if h2_rate >= 0.5 else '未達')} |",
        f"| H3 ランディング登録率 | {fmt(h3_rate, True)} | 8% | {'合格' if h3_rate is not None and h3_rate >= 0.08 else '未達/未取得'} |",
        f"| H4 1週間継続 | {len(retained)}/{len(proto)} ({fmt(h4_rate, True)}) | 40% | {'合格' if h4_rate is not None and h4_rate >= 0.4 else '未達/未取得'} |",
        f"| H5 支払い意思 | {len(wtp)}/{n} ({fmt(h5_rate, True)}) | 25% | {'合格' if h5_rate is not None and h5_rate >= 0.25 else '未達/未取得'} |",
        "",
    ]
    if h3_by_variant:
        lines += [
            "### ランディング A/B",
            "",
            "| 見出し | 表示 | 登録 | 登録率 |",
            "|---|---:|---:|---:|",
        ]
        for key, label in (
            ("a", "A: タブを開いても、軽い。"),
            ("b", "B: そのタブ、メモリを食いすぎていませんか。"),
        ):
            item = h3_by_variant[key]
            lines.append(f"| {label} | {item['views']} | {item['signups']} | {fmt(item['rate'], True)} |")
        lines.append("")
    if reasons:
        lines.append("## 理由")
        lines.append("")
        lines.extend(f"- {r}" for r in reasons)
        lines.append("")

    Path(args.out).write_text("\n".join(lines), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    print(f"wrote {out_json} and {args.out}")


if __name__ == "__main__":
    main()
