# 証拠パッケージ（Evidence Package）

このディレクトリは、設計文書の主張を検証するための台帳と監査結果である。
プロトコルは `universal-article-writer`（MIT）の成果物形式に合わせている。

## ファイル

| ファイル | 内容 |
|---|---|
| `sources.csv` | 出典台帳。55 件。URL、取得日、検証状態、該当箇所 |
| `claims.csv` | 主張台帳。40 件。証拠 ID、推論の型、Warrant、反証条件、荷重フラグ |
| `article.md` | 設計の要約記事。`[Sxx]` と `[Cxx]` で台帳に接続 |
| `logic-report.md` | 主結論の標準形、隠れた前提、必要条件/十分条件、価値判断の分離 |
| `dossier.md` | 意思決定パッケージ。最強の反論、限界、価値前提、AI 開示 |
| `red-team-review.md` | 6 役割の赤チームレビューと修正履歴 |
| `search-log.md` | 検索語、採用基準、確認できなかったこと |
| `audit-report.json` | `audit_article.py` の出力 |
| `argument-lint.json` | `argument_lint.py` の出力 |
| `source-report.json` | `verify_sources.py` の出力 |

## 監査結果（2026-10-23）

| 監査 | 結果 |
|---|---|
| `audit_article.py` | **READY** / Critical 0 / Major 0 / Minor 9 / Info 1 |
| `argument_lint.py` | **PASS** / Critical 0 / Major 0 / Minor 0 |
| `verify_sources.py` | **PASS** / Critical 0 / Major 0 / Minor 0 |

Info 1 は、荷重主張 C02 が `contested` であることによる。PX1 で Google OAuth の識別子画面が表示されたため、公式文書の「拒否」と実機の結果が一致しない。C03（Widevine）も PX4 で EME が supported となり `contested` だが、荷重主張ではない。Minor 9 は、S33、S34、S38、S39、S41、S42、S43、S46 などを `partial` として引用していることによる。これらはコミュニティの issue であり、一次資料ではない。

## 再現方法

```bash
python scripts/audit_article.py \
  --article docs/architecture/evidence/article.md \
  --sources docs/architecture/evidence/sources.csv \
  --claims docs/architecture/evidence/claims.csv \
  --out docs/architecture/evidence/audit-report.json

python scripts/argument_lint.py \
  --article docs/architecture/evidence/article.md \
  --claims docs/architecture/evidence/claims.csv \
  --sources docs/architecture/evidence/sources.csv \
  --out docs/architecture/evidence/argument-lint.json \
  --fail-on major

python scripts/verify_sources.py \
  --sources docs/architecture/evidence/sources.csv \
  --article docs/architecture/evidence/article.md \
  --out docs/architecture/evidence/source-report.json
```

## 注意

- 監査スクリプトは構造を検査する。主張の真偽、引用の妥当性、統計の正しさは判定しない。
- 数値は取得日時点のものである。Microsoft Learn のページは更新されるため、引用時は取得日を添える。
- 個別サイトの互換性と性能は、まだ実測していない。`09-benchmark-plan.md` の手順で測る。
