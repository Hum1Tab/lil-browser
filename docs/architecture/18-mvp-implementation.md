# 18. lIl Browser 0.3.0 — 実装済み MVP

状態: **実装済み・スモークテスト済み**
日付: 2026-10-23
場所: [`app/PxBrowser/`](../../app/PxBrowser/)
配布物: [`dist/PxBrowser/`](../../dist/PxBrowser/)、[`dist/PxBrowser-win-x64.zip`](../../dist/PxBrowser-win-x64.zip)

## 1. 何を作ったか

「軽量 + 高速」ではなく、**メモリ軽量・プライバシー・制御**に絞った Windows ブラウザ MVP。
.NET 8 WinForms + WebView2 で実装し、自己完結型の `PxBrowser.exe` として配布できる。

**これは Chrome/Edge の完全代替ではない。** 拡張、Google の完全ログイン、Netflix の DRM、速い起動は対象外。

## 2. 実装した機能

| 機能 | 状態 |
|---|---|
| タイトルバー統合 | 動作。タブを最上部に配置、最小化 / 最大化 / 閉じるは自前 |
| タブ（新規、閉じる、選択、タイトル） | 動作。＋ は最後のタブの右、右端の × で閉じる |
| フレームアイコン | テーマ色の PX アイコン（16〜256px） |
| アドレスバー、戻る、進む、再読み込み、停止、ホーム | 動作 |
| ダーク / ホワイトテーマ | 動作。chrome、設定、履歴に反映 |
| ホーム / 新規タブ | ホームは Google。新規タブは検索エンジンが Google なら Google を開く |
| ブックマーク（☆、Ctrl+D、一覧、JSON 保存） | 動作 |
| 履歴（JSON、90 日 / 5000 件、削除 UI） | 動作 |
| 設定（ホーム、検索 URL、遮断、Strict、復元、終了時削除） | 動作 |
| 簡易広告遮断（ホストリスト + `filters.txt`） | 動作（テストで 1 件遮断） |
| プライベートウィンドウ（InPrivate） | 動作（再起動後に cookie/local 空） |
| 権限ダイアログ（許可 / 拒否） | 動作 |
| ダウンロード開始 | 動作（既定の保存先） |
| セッション復元 | 動作（`session.json`） |
| ページ内検索（Ctrl+F） | 動作（JS `window.find`、件数表示） |
| ズーム（Ctrl+±/0） | 動作（0.25〜3.0） |
| 全画面（F11 / Esc） | 動作 |
| 印刷（Ctrl+P） / 名前を付けて保存（Ctrl+S） | 動作 |
| HTML フルスクリーン（YouTube 等） | 動作。ページの全画面要求で chrome を隠す |
| タブ切替 / 閉じたタブを開く | Ctrl+Tab、Ctrl+Shift+Tab、Ctrl+1〜9、Ctrl+Shift+T |
| ダウンロード一覧 | 動作。Ctrl+J / メニューから開く・フォルダ表示 |
| 権限の記憶（サイト別、プライベート除く） | 動作 |
| クラッシュ復元（`clean.exit` マーカー） | 動作 |
| インストーラ / アンインストーラ | 動作（PowerShell） |
| アイコン | 動作 |
| キーボード操作 | 動作 |

## 3. 構成

```text
app/PxBrowser/
  Program.cs          起動、引数、例外処理
  MainForm.cs         UI、タブ、履歴、設定、遮断、プライベート、スモーク
  Settings.cs         設定の JSON 永続化
  HistoryStore.cs     履歴の JSON 永続化、90 日 / 5000 件
  Adblock.cs          ホストリスト + filters.txt
  AppPaths.cs         %LOCALAPPDATA%\PxBrowser のパス
  filters.txt         簡易フィルタ
  tests/server.py     スモーク用ローカルサーバー
  README.md           利用・ビルド・制限
```

データ:

- `%LOCALAPPDATA%\PxBrowser\settings.json`
- `%LOCALAPPDATA%\PxBrowser\history.json`
- `%LOCALAPPDATA%\PxBrowser\session.json`
- `%LOCALAPPDATA%\PxBrowser\WebView2\`（通常）
- `%LOCALAPPDATA%\PxBrowser\WebView2Private\`（InPrivate）

`--data-dir` でテスト用に分離できる。

## 4. スモークテスト結果

### 4.1 通常モード

`smoke.json`:

```json
{
  "private": false,
  "tabs": 2,
  "tabSelect": 0,
  "tabsAfterClose": 1,
  "tabClose": "2->1",
  "history": 2,
  "blocked": 1,
  "bookmarks": 1,
  "bookmarkMarked": true,
  "maximizeState": "Maximized",
  "maximizeBounds": "1920x1032",
  "maximizeRestored": true,
  "maximizeButtonHit": "Maximize",
  "newTabUrl": "https://www.google.com/",
  "newTabOpened": "https://www.google.com/",
  "title": "PX Test Page",
  "body": "PX Read Page
cookie=pxtest=1
local=1",
  "zoom": 1.2,
  "find": "true",
  "fullscreen": "True/True",
  "settingsRoundTrip": true
}
```

- 2 タブ、履歴 2 件。タブバーのクリックで先頭タブを選択し、右端の × で 1 タブに減る。
- テストページの `/ad/banner.png` を 1 件遮断。
- ブックマークを 1 件保存し、`bookmarkMarked=true` を確認。
- 最大化: ボタン座標の判定が `Maximize`、`Maximized`、作業領域 `1920x1032`、クライアント領域 `0,0,1920,1032`、復元後に元の Bounds へ一致（`maximizeRestored=true`）。
- 新規タブを実際に開き、`newTabOpened=https://www.google.com/` を確認。
- HTML フルスクリーン: ローカルの `/fullscreen` で `containsFullScreenElement=true`、`hostFullscreen=true`、chrome 非表示、`1920x1080`、`edgeHitTest=1`（リサイズ不可）、`contentPadding=0` を確認。結果: [`fullscreen-smoke.json`](../../prototype/results/px11-essentials/fullscreen-smoke.json)
- タブ切替・閉じたタブの再オープン・ダウンロード一覧は実装済み。フルスクリーン以外のショートカットは主にメニューからも操作できる。
- cookie と localStorage が同一セッションで読める。
- 設定の保存・再読込が一致。

### 4.2 プライベートモード

1 回目（`/` を開いて cookie/local を設定）:

```json
{
  "private": true,
  "history": 0,
  "body": "PX Read Page
cookie=pxtest=1
local=1"
}
```

2 回目（同じデータディレクトリで `/read`）:

```json
{
  "private": true,
  "history": 0,
  "body": "PX Read Page
cookie=
local="
}
```

- 履歴 0。
- 再起動後に cookie / localStorage が空。
- InPrivate の分離を確認。

### 4.3 履歴の永続化

同じデータディレクトリで通常スモークを 2 回実行すると、履歴は 2 件から **4 件** に増えた。
`history.json` の読み込み・追記・保存が動作している。

### 4.4 セッション復元

`session.json` に 2 URL を置いて `--smoke --smoke-restore` を実行すると、2 タブが復元された。

```json
{
  "tabs": 2,
  "urls": [
    "http://127.0.0.1:8769/",
    "http://127.0.0.1:8769/read"
  ],
  "sessionFile": true
}
```

### 4.5 通常起動

`PxBrowser.exe --data-dir ...` でウィンドウが開き、`session.json` と `history.json` が生成されることを確認。
終了時にセッションが保存される。

### 4.6 追加機能

- ズーム: スモークで `ZoomFactor=1.2` を確認。
- ページ内検索: `window.find('PX')` が `true` を返すことを確認。
- 全画面: F11 相当の切り替えで `fullscreen=True/True`（入って戻る）を確認。
- テーマ: ダーク / ホワイトで chrome と Google の新規タブを確認。
- 新規タブ: 検索エンジンが Google のとき `https://www.google.com/` を開く。
- タブの ×: 右端に表示し、クリックで `CloseTab` を呼ぶ。スモークで 2 タブ → 1 タブを確認。
- ブックマーク: ☆ で追加し、`bookmarks.json` に保存。一覧から開く・削除できる。スモークで 1 件保存を確認。
- 権限の記憶、印刷、保存、クラッシュ復元は実装済み。ダイアログを伴うため自動スモークの対象外。

### 4.7 インストーラ

`install.ps1 -InstallDir C:\Temp\pxb-install -NoShortcut -NoRegistry` で一時ディレクトリに導入し、
導入先の `PxBrowser.exe` でスモークテストが通ることを確認した。
`uninstall.ps1` で `PxBrowser.exe` が削除されることも確認した。

`clean.exit` マーカーを消して再起動すると、`session.json` から 1 タブが復元された（クラッシュ復元）。

## 5. 配布物

- `dist/PxBrowser/PxBrowser.exe`（単一 exe、自己完結、.NET ランタイム同梱）
- 約 **162.9MB**。
- `filters.txt` 同梱。
- `dist/PxBrowser-win-x64.zip`（約 **66.0MB**）。
- `install.ps1` / `uninstall.ps1` 同梱。

インストール:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

`%LOCALAPPDATA%\Programs\PxBrowser` にコピーし、スタートメニューのショートカットと
HKCU のアンインストール項目を作る。既定ブラウザには登録しない。

WebView2 Runtime は Windows 11 に通常同梱。無い場合は Microsoft の Evergreen Runtime が必要。

起動:

```text
dist\PxBrowser\PxBrowser.exe
```

## 6. 設計との差分

| 設計 | MVP |
|---|---|
| Rust + Win32 ネイティブ UI + wry | C# WinForms + WebView2 |
| ネイティブ UI を 4〜6 か月で作る | 既成コントロールで短期間に検証 |
| タブのサスペンド / 破棄 | 未実装 |
| 署名付きフィルタ更新 | 未実装（ローカル `filters.txt`） |
| AppContainer | 未実装 |
| 自動更新 / インストーラ | インストーラは実装、自動更新は未実装 |

MVP は **需要検証と操作感の確認**のための実装であり、設計の最終形ではない。
本格製品にするなら、ネイティブ UI、更新、署名、AppContainer、サスペンド、クラッシュ復旧が必要。

## 7. 制限

- Chrome 拡張は使えない。
- Google の完全ログインは未検証（識別子画面まで）。
- Netflix などの DRM 再生は未検証。
- 起動は Edge より遅い（PX3c: 1 タブ 481 vs 293ms）。
- uBO と同じ遮断率は出ない。
- 履歴は暗号化していない。
- 自動更新、コード署名、既定ブラウザ登録、外部監査は無い。
- 配布 exe は、この PC のローカル自己署名証明書（CN=PX Browser Local Test）で署名済み。他 PC では同じ証明書を信頼するか、正式なコード署名証明書が必要。
- **日常の主力ブラウザとして配布しない。** まずは限定テスト用。

## 8. デザインとホーム

`D:\my project\Hum1.dev` の雰囲気（ink / paper / gold、モノラベルの余白、セリフの見出し）を参考に、
**軽い範囲で** UI を寄せた。

- ツールバー、タブ、ウィンドウ操作部を dark ink（`#171817`）とクリーム（`#f7f1e7`）に変更。
- アクセントはゴールド（`#d3aa67`）に統一（mint / coral を削除）。
- ネイティブタイトルバーを廃止し、**最上部にタブバー**を配置。ウィンドウの最小化 / 最大化 / 閉じるは自前ボタン。
- 最大化は `WindowState=Maximized` に任せず、通常ウィンドウのまま現在のモニタ作業領域へ Bounds を広げる。タスクバー復帰時に画面外へ飛ばないための措置。
- 最大化 / フルスクリーン中は `WS_THICKFRAME` を外し、クライアント領域をウィンドウ全体に一致させる。外周 7px のデスクトップが見える問題を防ぐ。
- タブバーの余白をドラッグして移動、ダブルクリックで最大化。外周ドラッグでリサイズ。
- タブは自前描画の丸いタブバー。選択タブにゴールドの下線。× は選択中 / ホバー中だけ表示し、クリックで閉じる。タブ上端を含むスロット全体をクリック判定に使う。
- 新しいタブの ＋ は最後のタブのすぐ右。ツールバーからは外した。
- 新規タブ（＋ / Ctrl+T / メニュー）はアドレスバーへフォーカスし、URL を全選択する。
- ツールバーとウィンドウ操作のアイコンは Segoe Fluent Icons に統一。アプリアイコンも ink / gold の PX に刷新。
- タブ帯の右側もテーマ色で埋める。
- メイン画面の外周もテーマ色。ネイティブ `TabControl` の白い枠を避けるため、`BrowserTab` + `PxTabHost` でホストする。
- 一覧は `PxListView` でヘッダー・行・選択色を自前描画。最終列を伸ばして右端の白い余白をなくす。
- アドレスバーは丸いピル。戻る / 進む / 再読み込み / ホーム / ☆ / ＋ / 履歴 / 設定 / メニューを左右に配置。
- アドレスバーは左のナビゲーションと右端のアクション群の間を埋める。右側に不自然な空白を残さない。
- アドレスバーの外側はタブバー色、内側のピルだけを InkSoft にする。テキストボックスは高さを固定して垂直中央に配置。
- 幅に応じたコンパクト表示。通常はタブバー 40px / ツールバー 50px、1000px 未満で 34px / 44px、860px 未満で 32px / 40px。履歴・設定はメニューへ集約。
- タブ幅はタブ数とウィンドウ幅から 70〜200px で自動計算。タブが増えても横に収まる。
- ツールバーのオーバーフローは無効。幅が狭くてもアドレスバーを残し、固定幅からアドレス幅を再計算する。
- 無効な戻る / 進むは淡色表示。アドレスバーはフォーカス時にゴールドの枠。
- **ダーク / ホワイトテーマ**を設定から選択できる。chrome、設定、履歴に反映。
- GitHub `Hum1Tab/lil-browser` の最新コミットを起動時と 15 分ごとに確認。ローカル exe より新しければ右上に更新ボタンを表示する（自動インストールはしない）。
- 設定 / 履歴 / ブックマーク / ダウンロード / 権限確認 / 確認ダイアログは共通の `PxDialog`。ヘッダー、閉じる、角丸、影、テーマを統一。
- DWM の border / corner 色もテーマに合わせ、フレーム外周を揃える。
- **新規タブは検索エンジンが Google なら `https://www.google.com/` を開く**。ローカルの `start.html` は廃止。
- **ホームも `https://www.google.com/`**。設定の既定値も Google。
- **ブックマーク**は ☆ / Ctrl+D で追加・解除、Ctrl+Shift+O で一覧。`bookmarks.json` に保存。
- ステータスバーは表示しない。「遮断: ...」「完了: ...」のような下部の通知を削除した。エラーはページ側に表示される。
- Web UI フレームワーク、画像、外部フォントは追加していない。

これにより、ブラウザ本体のプロセスやメモリを増やさずに、見た目だけを Hum1.dev に寄せている。
本格的なネイティブ UI のデザインシステム化は、需要検証の後に行う。

- 画面: ダーク [`ui-dark.png`](../../prototype/results/px10-design/ui-dark.png)、ホワイト [`ui-light.png`](../../prototype/results/px10-design/ui-light.png)

## 9. 次の作業

1. 需要検証のプロトタイプテストで、PX6 の代わりにこの MVP を使う。
2. 5〜10 人に 1 週間使ってもらい、タブ、履歴、遮断、プライベートの操作を観察する。
3. クラッシュ、メモリ、起動のログを取る。
4. タブのサスペンド / 破棄を実装する。
5. 署名付き更新を実装する（インストーラは実装済み）。
6. 外部セキュリティ監査を受ける。

