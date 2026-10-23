# Windows向け軽量ブラウザ 設計資料

最終更新: 2026-10-23
状態: 設計・検証フェーズ完了（PX1〜PX6、PX7 未実施）。需要検証キット作成済み。**MVP（lIl Browser 0.3.0）を実装・スモークテスト済み**。需要データは未取得。
対象: Windows 11 を主対象、Windows 10 はベストエフォート

> **重要**: 最終報告は [16-final-report.md](16-final-report.md)。元の「軽量 + 高速の Chrome/Edge 代替」は、PX3c の起動不合格と PX6 の UI 工数、広告遮断のカバレッジ天井により **No-Go** である。ユーザーの選択により、看板を「メモリが軽い・プライバシー・制御」に再定義し、需要を先に検証する段階に入った。検証計画は [17-redefinition-and-demand-validation.md](17-redefinition-and-demand-validation.md)。Widevine は PX4 で EME が supported、Google ログインとパスキーは PX1 で識別子画面とプロンプトまで確認したが、完全ログイン・認証完了・実再生は未検証である。

## 0. この資料群の読み方

`docs/` は設計文書である。`prototype/` には PX1 の検証コードと結果がある。製品の実装、依存関係の導入、既存コードの変更は行っていない（PX1 の検証用 .NET SDK と NuGet パッケージを除く）。
調査時点でワークスペースは空であり、既存ファイルの破壊は発生していない。

文書は「事実」「推測」「目標」を分けて書く。次のラベルを本文で使う。

| ラベル | 意味 | 扱い |
|---|---|---|
| 【事実】 | 一次資料（公式ドキュメント、一次コード、ライセンス原文）で確認した | 出典IDを付ける |
| 【推測】 | 一次資料から導いた設計上の予測。反証可能性がある | 検証計画を付ける |
| 【目標】 | 達成を約束しない設計目標。実測で判定する |  benchmark で判定する |
| 【未検証】 | 一次資料を確認できなかった、または環境依存で確認していない | 実装前に検証する |

数値目標は「保証」ではない。とくに他ブラウザとの比較値は、同一環境での実測前に断定しない。

## 1. 調査方法

- 一次資料を優先した。Microsoft Learn の WebView2 リファレンス、Tauri 公式ドキュメント、Tauri 本体のソースコード、Google の OAuth ポリシー、Chromium の設計文書、各プロジェクトの LICENSE を直接取得して読んだ。
- 取得日は 2026-10-23。URL と accessed 日を `evidence/sources.csv` に記録した。
- 主張と証拠の対応は `evidence/claims.csv`、論理監査は `evidence/logic-report.md` に残した。
- 反証・弱点は `evidence/red-team-review.md` にまとめ、設計に反映した。
- 数値の引用はベンダー発表を含む。ベンダー発表は「ベンダーがそう報告した」という事実として扱い、独立検証済みの事実としては扱わない。

## 2. 文書一覧

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-product-vision.md](01-product-vision.md) | 目的、対象ユーザー、非目標、成功指標 |
| 02 | [02-technology-evaluation.md](02-technology-evaluation.md) | Tauri v2 / WebView2 / CEF / Chromium fork の比較と採用理由 |
| 03 | [03-system-architecture.md](03-system-architecture.md) | コンポーネント、プロセス、信頼境界、データフロー |
| 04 | [04-security-threat-model.md](04-security-threat-model.md) | 脅威モデル、攻撃経路、緩和策、残留リスク |
| 05 | [05-performance-architecture.md](05-performance-architecture.md) | タブライフサイクル、メモリ、起動、応答性 |
| 06 | [06-adblocking-architecture.md](06-adblocking-architecture.md) | 広告遮断パイプライン、フィルタ管理、限界 |
| 07 | [07-privacy-and-storage.md](07-privacy-and-storage.md) | Cookie、履歴、一時データ、削除設計 |
| 08 | [08-compatibility-and-limitations.md](08-compatibility-and-limitations.md) | Web 互換性、OAuth、DRM、未解決制約 |
| 09 | [09-benchmark-plan.md](09-benchmark-plan.md) | 比較対象、計測条件、指標、合格基準 |
| 10 | [10-implementation-roadmap.md](10-implementation-roadmap.md) | 開発順序、マイルストーン、テスト計画 |
| 11 | [11-open-questions-and-risks.md](11-open-questions-and-risks.md) | 未確定事項、高リスク、検証仮説 |
| 12 | [12-final-recommendation.md](12-final-recommendation.md) | 最終推奨、A/B/C の比較、意思決定 |
| 13 | [13-refutation-review.md](13-refutation-review.md) | 反証レビュー。設計を破綻させる問題、再検討、検証プロトタイプ |
| 14 | [14-functional-specification.md](14-functional-specification.md) | 最小限の機能仕様、UI 挙動、ショートカット、受け入れ条件 |
| 15 | [15-requirements-traceability.md](15-requirements-traceability.md) | 依頼の各要求と設計成果物の対応表、未実施事項 |
| 16 | [16-final-report.md](16-final-report.md) | 最終報告。No-Go の理由、再定義案、需要検証、納品物 |
| 17 | [17-redefinition-and-demand-validation.md](17-redefinition-and-demand-validation.md) | 再定義した製品、需要検証の仮説・判定基準・3 週間計画 |
| 18 | [18-mvp-implementation.md](18-mvp-implementation.md) | 実装済み MVP（lIl Browser 0.3.0）の機能・テスト・配布物・制限 |
| — | [adr/](adr/README.md) | Architecture Decision Record |
| — | [evidence/](evidence/) | 出典台帳、主張台帳、論理監査、ドシエ、赤チームレビュー |

## 3. 30秒で読む結論

1. エンジンは WebView2 を第一候補にする。Microsoft が Chromium のサンドボックスとサイト分離を保守し、セキュリティ更新を継続できる現実的な選択肢である。自前の Chromium フォークは、小規模チームでは更新遅延そのものがセキュリティ後退になる。
2. ただし WebView2 は「Chrome/Edge の完全代替」にはならない。ブラウザ拡張、Web Push、Payment API、Periodic Background Sync は使えない。Google OAuth とパスキーは公式には制限されているが、PX1 の実機試験では OAuth の識別子画面と Windows Security のプロンプトが動作した。Widevine/PlayReady は PX4 で EME が supported だった。完全ログイン、認証完了、実再生は未検証である。
3. 当初は製品定義を「Google の完全ログインと DRM が確認できない場合に、それらを必要としない用途に絞った、低リソース・高プライバシーのブラウザ」に絞る方針だった。しかし PX3c で起動の優位が否定され、PX6 で UI 工数が重いことが分かった。**元の「軽量 + 高速」としては No-Go。** 続けるなら看板を「メモリ・プライバシー・制御」に再定義し、需要を先に検証する（[16](16-final-report.md)）。
4. 実装基盤に Tauri v2 は使わない。Tauri は全 WebView に `isTauri` と `__TAURI_INTERNALS__` を注入する。ただし、これは致命傷ではなく二次的な問題である。製品の第一候補は **wry + ネイティブ UI**、Tauri は 3 か月 MVP の妥協案、raw `webview2-com` は最大制御が必要な場合の第三候補とする。
5. 技術候補は「Rust + Win32 ネイティブ UI + wry 経由の WebView2 直接制御」。ただし、これは「軽量 + 高速」としての推奨ではない。看板を再定義する場合の候補である。広告遮断は、まずエンジンの追跡防止（Strict）を使い、ホストの `WebResourceRequested` は高リスクドメインに限定する。
6. 最大の技術的障害はカバレッジである。WebResourceRequested はフレーム URL を公開せず、WebSocket では発火せず、`$csp` を変更できない。third-party 判定と `$domain`、`$websocket`、`$csp` は実装できないか近似になる。Brave/uBO と同じ遮断率は出ない。往復コストは PX2 では小さかった。
7. 最大のセキュリティリスクは、ホストが設計上「通信を横取りしスクリプトを注入する存在」であること。フィルタ更新とスクリプトレットのサプライチェーンを破られると、全ページの資格情報が危険にさらされる。ここは署名・固定・レビューで守る。
8. **13 の反証レビューと PX1〜PX6 で、設計の前提は更新された。** 拡張が必須なら WebView2 は失格。Google ログイン、パスキー、Widevine は PX の完全確認が必要である。遮断コストは PX2 で小さく、制約はカバレッジに移った。PX3 では 1/5/20 タブのメモリが Edge を下回ったが、**PX3c で暖機後起動が Edge より遅く、「軽量 + 高速」は Gate 1 不合格**である。PX5 では InPrivate のサイトデータは残らないが、プロファイルファイルは残る。PX6 の UI プロトタイプは 404 行・14 コントロールで、生産 UI は 4〜6 か月と推定された。最終報告は [16](16-final-report.md)。
9. 要求の網羅性は [15-requirements-traceability.md](15-requirements-traceability.md) で確認できる。設計と PX1〜PX6 は完了、PX7 は未実施、本格実装、法務、監査は未実施である。需要検証キット（[17](17-redefinition-and-demand-validation.md)、[prototype/px8-demand](../../prototype/px8-demand/)）と、**実装済み MVP（[18](18-mvp-implementation.md)、[app/PxBrowser](../../app/PxBrowser/)）** がある。需要データは未取得である。

## 4. 表記

- 出典は `[S01]` 形式で示す。対応表は `evidence/sources.csv`。
- 主張は `[C01]` 形式で示す。対応表は `evidence/claims.csv`。
- 図は Mermaid で書く。GitHub と主要 Markdown ビューアで描画できる。
- 「WebView2」は Microsoft Edge WebView2 Runtime を指す。「Edge」は Microsoft Edge ブラウザ本体を指す。

## 5. プロトタイプ（PX）

- コード: [`prototype/px1-webview2/`](../../prototype/px1-webview2/)
- PX1（識別子）: [`prototype/reports/px1-identity.md`](../../prototype/reports/px1-identity.md)
- PX2（遮断コスト）: [`prototype/reports/px2-adblock.md`](../../prototype/reports/px2-adblock.md)
- PX3（メモリ・起動）: [`prototype/reports/px3-memory.md`](../../prototype/reports/px3-memory.md)
- PX3b（公平な起動）: [`prototype/reports/px3b-startup.md`](../../prototype/reports/px3b-startup.md)
- PX3c（暖機後起動）: [`prototype/reports/px3c-startup.md`](../../prototype/reports/px3c-startup.md)
- PX4（DRM/EME）: [`prototype/reports/px4-drm.md`](../../prototype/reports/px4-drm.md)
- PX5（InPrivate 残留）: [`prototype/reports/px5-privacy.md`](../../prototype/reports/px5-privacy.md)
- PX6（UI 工数）: [`prototype/reports/px6-ui.md`](../../prototype/reports/px6-ui.md)
- PX7（CEF 可否）: [`prototype/reports/px7-cef.md`](../../prototype/reports/px7-cef.md)（未実施。CEF 配布物は確認、prebuilt `cefclient.exe` なし）
- PX8（需要検証キット）: [`prototype/px8-demand/README.md`](../../prototype/px8-demand/README.md)（データ未取得）
- 生データ: [`prototype/results/`](../../prototype/results/)

PX1 では Google OAuth の識別子画面と Windows Security のプロンプトを確認した。PX2 では 500 リクエストの全件フックでナビゲーション時間の悪化が観測されなかった。PX3 の初期実測では 1/5/20 タブの Private メモリが Edge を下回った（-45%/-41%/-10%）。**PX3c の暖機後起動は 1 タブ 481 vs 293ms、5 タブ 456 vs 310ms で Edge が速く、「起動が速い」は否定された。** PX4 では Widevine/PlayReady の EME が supported を返した。PX5 では InPrivate のサイトデータは再起動後に残らないが、空の History/Cookies DB と Local Storage、Code Cache、Crashpad は UDF に残った。PX6 の UI プロトタイプは 404 行・14 コントロールで、生産 UI は 4〜6 か月と推定された。PX7 は CEF 156 の配布物を確認したが、prebuilt `cefclient.exe` は無く、VS2022/CMake が必要なため未実施。いずれも完全ログイン、認証完了、実再生、実サイトの性能は未検証である。

## 6. 実装済み MVP

- コード: [`app/PxBrowser/`](../../app/PxBrowser/)
- 配布物: [`dist/PxBrowser/`](../../dist/PxBrowser/)、[`dist/PxBrowser-win-x64.zip`](../../dist/PxBrowser-win-x64.zip)
- 実装レポート: [18-mvp-implementation.md](18-mvp-implementation.md)
- デザイン: ダーク [`ui-dark.png`](../../prototype/results/px10-design/ui-dark.png)、ホワイト [`ui-light.png`](../../prototype/results/px10-design/ui-light.png)

タブ、アドレスバー、履歴、設定、簡易遮断、プライベート、権限、ダウンロード、セッション復元、ページ内検索、ズーム、全画面、印刷、保存、クラッシュ復元、インストーラを実装した。
タブ右端の × で閉じられ、最後のタブの右の ＋ で新規タブ。ダーク / ホワイトテーマを設定から選べる。☆ / Ctrl+D のブックマークと、丸いアドレスバー・タブを持つ。
YouTube などの HTML フルスクリーン、タブ切替、閉じたタブの再オープン、ダウンロード一覧（Ctrl+J）に対応する。
ネイティブタイトルバーをなくし、タブを最上部に配置。最小化 / 最大化 / 閉じるは自前ボタンで、ドラッグ移動と外周リサイズに対応する。
スモークテストでは、2 タブ、タブ × で 1 タブ、履歴 2 件、遮断 1 件、ブックマーク 1 件、設定往復、ズーム 1.2、検索 true、全画面の往復、プライベートの再起動後 cookie/local 空を確認した。
デザインは Hum1.dev 風の ink / paper / gold。ホームと新規タブは Google である。
**これは Chrome/Edge の完全代替ではない。** 拡張、Google の完全ログイン、Netflix、速い起動は対象外である。

