# PX8: 需要検証キット

これは「メモリが軽い・プライバシー・制御」に再定義したブラウザの需要を検証するためのキットである。**まだデータは無い。** ここにあるのは、インタビュー、定量調査、ランディングページ、スコアカードの計測器であり、需要の証拠ではない。

## ファイル

| ファイル | 用途 |
|---|---|
| [landing.html](landing.html) | 需要検証用の静的ランディングページ。既定はモック、`?live=1` でローカル送信 |
| [server.py](server.py) | ローカル送信用のサーバー。`POST /api/waitlist` を `submissions.jsonl` に保存、表示/登録を `metrics.json` に記録 |
| [analyze.py](analyze.py) | スコアカードから H1〜H5 と Go/No-Go を集計 |
| [validate_scorecard.py](validate_scorecard.py) | スコアカードの列・値・範囲・重複を検証 |
| [anonymize.py](anonymize.py) | 文字起こしからメール、URL、パス、電話、handle、長い数字、指定名を除去 |
| [participant-tracker.csv](participant-tracker.csv) | 募集・同意・実施・謝礼の管理（PII なし） |
| [interview-guide.md](interview-guide.md) | 20〜30 分のインタビュー台本、スクリーニング、制約のカードソート |
| [recruitment.md](recruitment.md) | 募集文（日英）、チャネル、スクリーニング、日程調整 |
| [consent.md](consent.md) | 同意説明、録音、保存、撤回、フォーム同意文 |
| [interview-notes-template.md](interview-notes-template.md) | 参加者ごとの記録テンプレートと演繹コード |
| [coding-framework.md](coding-framework.md) | コード定義、件数の数え方、信頼性チェック、分析出力 |
| [survey.md](survey.md) | 5 分の定量調査（10 問 + 注意チェック） |
| [demand-scorecard.csv](demand-scorecard.csv) | 1 行 1 人の記録テンプレート |
| [../../docs/architecture/17-redefinition-and-demand-validation.md](../../docs/architecture/17-redefinition-and-demand-validation.md) | 仮説、判定基準、スケジュール、倫理 |

## ランディングページの開き方

そのまま開く場合:

```text
landing.html
```

ローカルサーバーで見る場合:

```powershell
python -m http.server 8080 --directory prototype/px8-demand
# http://127.0.0.1:8080/landing.html
```

見出しの A/B:

- `landing.html` → 「タブを開いても、軽い。」
- `landing.html?v=b` → 「そのタブ、メモリを食いすぎていませんか。」

フォームは既定で `localStorage` の `px8_waitlist` に保存するだけで、外部送信しない。ローカルで送信を試す場合は、サーバーを起動して `?live=1` を付ける。

```powershell
python prototype/px8-demand/server.py 8080
# http://127.0.0.1:8080/?live=1
```

ライブモードでは、同意チェックがある場合だけ `POST /api/waitlist` を受け付け、`prototype/px8-demand/submissions.jsonl` に保存する。IP アドレスは記録しない。外部送信はしない。テスト後は `submissions.jsonl` を削除する。本番では、送信先、プライバシーポリシー、削除依頼の窓口を用意する。

## 集計

```powershell
python prototype/px8-demand/analyze.py --csv prototype/px8-demand/demand-scorecard.csv --visitors 200 --signups 12
```

`demand-summary.json` と `demand-report.md` を出力する。参加者が 0 人の場合は `NO_DATA` と表示し、判定を捏造しない。`--visitors` / `--signups` はランディングページの実績を渡す。未指定なら H3 は「未取得」になる。

`server.py` の `metrics.json` を使う場合は、次で A/B 別の登録率も出る。

```powershell
python prototype/px8-demand/analyze.py `
  --csv prototype/px8-demand/demand-scorecard.csv `
  --metrics prototype/px8-demand/metrics.json
```

## 前処理

スコアカードを分析する前に検証する。

```powershell
python prototype/px8-demand/validate_scorecard.py --csv prototype/px8-demand/demand-scorecard.csv
```

文字起こしは、分析前に匿名化する。

```powershell
python prototype/px8-demand/anonymize.py `
  --in transcript.txt --out transcript.anon.md --names "山田,佐藤"
```

自動匿名化は、人名、勤務先、地名、文脈から特定できる情報までは消さない。**必ず手動でレビューする。** 原文と対応表は保存しない。

## 進め方

1. インタビュー対象者を 20 人募集する（10 人: 10 タブ以上、5 人: プライバシー関心、5 人: Edge/Chrome 常用）。
2. [interview-guide.md](interview-guide.md) で 10 人、次に 10 人。
3. [survey.md](survey.md) を 30〜50 人に配る（インタビュー参加者と重複させない）。
4. [landing.html](landing.html) を公開し、チャネル別の登録数を測る。
5. [demand-scorecard.csv](demand-scorecard.csv) に記録する。
6. 3 週間後に、[17](../../docs/architecture/17-redefinition-and-demand-validation.md) の判定基準で Go / No-Go を決める。

## 判定の要約

- **Go**: H1（メモリ/プライバシーが上位 2）と H2（制約の許容）を満たし、H3（登録 8% 以上）、H4（1 週間継続 4/10 以上）、H5（支払い意思 5/20 以上）。
- **条件付き Go**: H1/H2 は満たすが、H3〜H5 のどれかが未達。訴求か価格を修正して再検証。
- **No-Go**: H1 または H2 が殺す条件。または H3〜H5 がすべて未達。

## 注意

- 20〜50 人では統計的な代表性は無い。市場の大きさを証明しない。
- 「軽い」「安全」を保証しない。実測値と限界を表示する。
- 回答者の PII を保存しない。録音は同意制。引用は匿名化する。
- このキットの数値を、実測結果と混同しない。着目する数値には「初期測定、1 回、軽量ページ」と注記する。
