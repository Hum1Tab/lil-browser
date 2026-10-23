# ADR-0001: エンジンに WebView2 を採用する

状態: 採用
日付: 2026-10-23

## 文脈

Windows 向けブラウザのエンジン候補は、WebView2、CEF、Chromium フォーク、Gecko 埋め込み、独自エンジンである。最優先の要求は「セキュリティ更新を継続できること」。

## 決定

WebView2 Runtime（Evergreen）を採用する。

## 理由

1. WebView2 は Edge と同じプロセスモデルとサイト分離を使う [S05][S26]。
2. Evergreen は Microsoft が自動更新し、セキュリティ修正を配信する [S15]。
3. 同一 UDF 内で複数プロファイルを持て、ブラウザプロセスを増やさずに分離できる [S07]。
4. ホストは必要な API（通信フック、サスペンド、権限、ダウンロード）を持つ [S09][S11][S08]。

## 結果

- エンジンの 0-day には、Microsoft の更新まで待つ。実行中のプロセスは再起動まで旧ランタイムを使う [S15]。
- ブラウザ拡張、Google ログイン、Widevine、Web Push、Payment API は使えない [S08][S20][S33]。
- 広告遮断は WebResourceRequested の制約を受ける [S18]。

## 代替案

- CEF: 制御性は高いが、更新追随と配布サイズの負担が大きい。拡張・DRM は解決しない。
- Chromium フォーク: 互換性は最大だが、小規模チームでは更新追随が困難 [S29]。
- Gecko 埋め込み: Windows での埋め込み実績と API が限られる。未検証。

## 再検討のトリガー

- WebView2 が Google ログイン以外の主要機能を恒久的に失う。
- Microsoft が Evergreen の更新方針を変える。
- WebResourceRequested が非推奨になる。
