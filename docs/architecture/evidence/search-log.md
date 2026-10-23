# 検索ログ

日付: 2026-10-23
目的: Windows 向け軽量ブラウザの技術選定、互換性、性能、セキュリティ、ライセンスの一次資料を集める。

## 1. データベースと手段

| 手段 | 用途 |
|---|---|
| 公式ドキュメント（Microsoft Learn、Tauri、Google、Chromium、EasyList） | 仕様、制約、ライセンス |
| 一次ソースコード（GitHub raw） | Tauri の IPC 注入、unstable API の確認 |
| GitHub API | issue の本文、ライセンスメタデータ |
| docs.rs | adblock-rust と wry の API |
| ブログ（Microsoft Edge、Google Developers、Chrome for Developers） | ベンダー報告、ポリシー |

## 2. 主な検索語

- Tauri v2 multiwebview unstable
- Tauri v2 remote URL IPC capabilities
- Tauri inject __TAURI_INTERNALS__
- WebView2 process model site isolation
- WebView2 user data folder multiple profiles
- WebView2 InPrivate mode
- WebView2 WebResourceRequested performance
- WebView2 AddWebResourceRequestedFilter
- WebView2 MemoryUsageTargetLevel
- WebView2 TrySuspendAsync
- WebView2 PreferredTrackingPreventionLevel
- WebView2 Enhanced Security Mode
- WebView2 Google OAuth disallowed_useragent
- WebView2 Widevine DRM
- WebView2 passkey WebAuthn
- WebView2 extensions support
- WebView2 Web Push Payment API
- Google OAuth embedded webview policy
- adblock-rust license MPL
- EasyList license GPL CC BY-SA
- Chromium site isolation memory overhead
- Edge sleeping tabs memory CPU
- Chrome Memory Saver tab discard
- Chromium release cycle security
- Windows job object process memory
- PresentMon frame timing
- CEF binary size license
- Tauri updater signature

## 3. 採用基準

1. 一次資料（公式ドキュメント、ソースコード、ライセンス原文）を優先した。
2. 二次資料（まとめ記事、Q&A）は、一次資料へのポインタとしてだけ使った。
3. 出典が確認できないものは「未検証」とした。
4. ベンダーの数値は「ベンダー報告」と明記した。
5. 同じ主張について、可能なら 2 つ以上の独立した資料を確認した。

## 4. 除外した資料

| 資料 | 除外理由 |
|---|---|
| SEO まとめ記事 | 一次資料への参照が無い、数値の出所が不明 |
| Stack Overflow の回答 | バージョン依存で、公式仕様ではない |
| 個人ブログのベンチマーク | 環境と手法が不明 |
| CVE の要約サイト | 一次アドバイザリを優先 |

## 5. 確認できなかったこと

- WebView2 の Cookie 暗号化の具体的な方式。
- InPrivate のディスク残留の範囲。
- WebView2 の Widevine 非対応を明記した公式リファレンス。
- CEF の配布物の正確なサイズ。
- Chromium フォークの実際の保守コスト。
- Google 以外の OAuth プロバイダの WebView2 での挙動。
- Windows 10 のサポート期間と WebView2 の将来。

これらは `11-open-questions-and-risks.md` に記載し、設計の前提にしない。

## 6. 取得日と再確認

- すべて 2026-10-23 に取得。
- 公開前に、URL の生存と、引用箇所の一致を再確認する。
- Microsoft Learn のページは更新されるため、引用時は「取得日時点」と明記する。
