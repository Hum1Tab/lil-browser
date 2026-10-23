# lIl Browser 0.3.0

メモリ軽量・プライバシー・制御に絞った、Windows 向けの小さなブラウザ MVP です。
Chrome/Edge の完全代替ではありません。

## できること

- タブ（新規、閉じる、選択、タイトル表示）。最上部にタブ、最後のタブの右に ＋、右端にウィンドウ操作ボタン。新規タブはアドレスバーにフォーカスし URL を全選択
- 丸いタブで、右端の × でも閉じる
- ダーク / ホワイトテーマ（設定から切替）
- GitHub の更新を検知すると、右上にアップデートボタンを表示（自動インストールはしません）
- ブックマーク（☆ / Ctrl+D で追加・解除、Ctrl+Shift+O で一覧）
- 丸いアドレスバーと、最近のブラウザ風の横並びツールバー。狭いウィンドウでもアドレスバーを残してコンパクト表示
- アドレスバー、戻る、進む、再読み込み、停止、ホーム
- 履歴（JSON に保存、90 日 / 5000 件で整理、履歴ダイアログから削除）
- 設定（ホームページ、検索 URL、簡易遮断、追跡防止 Strict、セッション復元、終了時削除）
- 簡易広告遮断（ホストリスト + `filters.txt`、`WebResourceRequested`）
- プライベートウィンドウ（WebView2 InPrivate、履歴を保存しない）
- 権限ダイアログ（許可 / 拒否、サイト別に記憶）
- ダウンロード開始（WebView2 の既定の保存先）
- セッション復元（前回のタブ URL）、クラッシュ復元
- ページ内検索（Ctrl+F）、ズーム（Ctrl+±/0）、全画面（F11）
- 印刷（Ctrl+P）、名前を付けて保存（Ctrl+S）
- HTML フルスクリーン（YouTube などの全画面ボタンに対応）
- ダウンロード一覧（Ctrl+J / メニュー）
- タブ切替（Ctrl+Tab / Ctrl+Shift+Tab / Ctrl+1〜9）、閉じたタブを開く（Ctrl+Shift+T）
- インストーラ / アンインストーラ（PowerShell）
- ホームは Google（`https://www.google.com/`）。新規タブも検索エンジンが Google なら Google を開く
- キーボード: Ctrl+T / W / L / H / J / , / R / F / P / S / D、Ctrl+Shift+T / O、Ctrl+Tab、Ctrl+1〜9、F5、F11、Alt+←/→、Ctrl+Shift+N

## できないこと

- Chrome 拡張は使えません。
- Google の完全ログインは未検証です（識別子画面までは表示）。
- Netflix などの DRM 再生は未検証です。
- 起動は Edge より遅いです（PX3c: 1 タブ 481 vs 293ms）。
- uBO と同じ遮断率は出ません（フレーム URL、WebSocket、CSP の制約）。
- 自動更新、コード署名、クラッシュレポート、既定ブラウザ登録は未実装です。
- 配布 exe はこの PC のローカル自己署名証明書で署名済みです。他 PC では正式なコード署名が必要です。ローカル署名は `sign-local.ps1` で再実行できます。

## インストール

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

`%LOCALAPPDATA%\Programs\lIlBrowser` にコピーし、スタートメニューのショートカットと
HKCU のアンインストール項目を作ります。既定ブラウザには登録しません。

アンインストール:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\lIlBrowser\uninstall.ps1"
```

ユーザーデータも消す場合: `-RemoveData`。

## 起動

`dist/lIlBrowser/lIlBrowser.exe` を実行します。WebView2 Runtime が必要です（Windows 11 は通常同梱）。
ホームと新規タブは Google です。

データは `%LOCALAPPDATA%\lIl Browser\` に保存されます。

- `settings.json`
- `history.json`
- `session.json`
- `WebView2\`（通常プロファイル）
- `WebView2Private\`（InPrivate）

## ビルド

```powershell
dotnet build app/lilBrowser/lilBrowser.csproj -c Release
```

## スモークテスト

```powershell
python app/lilBrowser/tests/server.py 8769
dotnet app/lilBrowser/bin/Release/net8.0-windows/win-x64/lIlBrowser.dll `
  --data-dir C:\Temp\pxb-data --smoke `
  --smoke-url http://127.0.0.1:8769/ --out C:\Temp\pxb-smoke
```

`smoke.json` にタブ数、履歴数、遮断数、設定往復を出力します。

プライベートモード:

```powershell
# 1 回目: cookie/localStorage を設定
lIlBrowser.exe --data-dir C:\Temp\pxb-private --private --smoke `
  --smoke-url http://127.0.0.1:8769/ --out C:\Temp\pxb-private-set

# 2 回目: 同じデータディレクトリで読む（空なら成功）
lIlBrowser.exe --data-dir C:\Temp\pxb-private --private --smoke `
  --smoke-url http://127.0.0.1:8769/read --out C:\Temp\pxb-private-read
```

## フィルタ

`filters.txt` にホストを 1 行 1 つ書きます。`||domain^` 形式も受け付けます。
これは簡易リストであり、EasyList の代替ではありません。

## 設計上の注意

- ホストは通信を横取りできる立場です。フィルタの供給網を信頼しないでください。
- プライベートモードでも、プロファイルファイルはディスクに残ります（PX5）。
- 履歴は暗号化していません。必要なら OS の ACL とディスク暗号化を使ってください。
- この MVP は署名・自動インストール・外部監査を受けていません。日常の主力ブラウザとして配布しないでください。

