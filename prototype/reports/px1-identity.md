# PX1: 識別子プローブ結果

日付: 2026-10-23
環境: Windows 11 Home 10.0.26200、WebView2 Runtime 155.0.4283.62、.NET SDK 8.0.426
コード: [`prototype/px1-webview2/`](../px1-webview2/)
生データ: [`prototype/results/px1-basic/`](../results/px1-basic/)、[`px1-flow/`](../results/px1-flow/)、[`px1-oauth/`](../results/px1-oauth/)、[`px1-dump/`](../results/px1-dump/)

## 1. 目的

「WebView2 では Google ログインと Windows Hello パスキーが使えない」という設計上の前提を、実機で反証する。設計の Gate 0 を決めるための最小のプローブであり、ブラウザではない。

## 2. 方法

WinForms + WebView2 の最小アプリを作り、次の URL を順に開いた。

- `https://accounts.google.com/ServiceLogin`
- `https://mail.google.com/`
- `https://www.youtube.com/`
- `https://github.com/login`
- `https://login.microsoftonline.com/`
- `https://webauthn.io/`
- `https://developers.google.com/oauthplayground/`（OAuth 2.0 Playground）

各ページで、タイトル、最終 URL、本文、`navigator.userAgent`、`window.chrome.webview` の有無、`PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()`（UVPAA）を記録した。OAuth は Playground の「Authorize APIs」を押し、Google の認可エンドポイントに遷移させた。WebAuthn は webauthn.io の Register を押し、Windows Security のプロンプトが起動するかを確認した。

## 3. 結果

| テスト | 結果 | 証拠 |
|---|---|---|
| Google ログイン画面 | 表示された。タイトル「ログイン - Google アカウント」 | `px1-basic/01_*.json` |
| Google にダミーメールを入力 | 「このアカウントは見つかりませんでした」と表示。`disallowed_useragent` は出ない | `px1-flow/02_*.json` |
| Google OAuth（Playground の client_id） | 認可エンドポイントが識別子画面を表示。エラーなし | `px1-oauth/01_*.json` |
| Gmail | ログイン画面に遷移（未ログインのため） | 初回実行ログ |
| YouTube | 通常のページが表示 | 初回実行ログ |
| GitHub ログイン | 表示。`Continue with passkey` と `Sign in with a passkey` がある | `px1-dump/04_*.json` |
| Microsoft ログイン | 表示 | 初回実行ログ |
| UVPAA | `{"uvpaa":true}` | 全 JSON |
| webauthn.io の Register | `CredentialUIBroker`（MainWindowTitle: `Windows セキュリティ`）が起動 | `px1-flow/03_*.json` とプロセス観測 |
| GitHub の passkey ボタン | 同上のプロンプトが起動 | `px1-flow/04_*.json` とプロセス観測 |
| `window.chrome.webview` | 全ページで `yes` | 全 JSON |
| User-Agent | Google は `Chrome/155.0.0.0`、GitHub/webauthn は `Chrome/155.0.0.0 Edg/155.0.0.0` | 各 JSON |

### 3.1 OAuth の最終 URL

OAuth Playground の「Authorize APIs」を押した後、次の URL に遷移した。

```text
https://accounts.google.com/v3/signin/identifier?opparams=...&client_id=407408718192.apps.googleusercontent.com&...&scope=https://www.googleapis.com/auth/userinfo.email&...
```

タイトルは「ログイン - Google アカウント」で、本文には「Google でログイン」「『Google OAuth 2.0 Playground』に移動」「メールアドレスまたは電話番号」が表示された。`disallowed_useragent` の文字列は本文にも HTML にも無い。

## 4. 解釈

### 4.1 Google ログイン

**「WebView2 では Google ログインが即座にブロックされる」という一般化は、この環境では再現しなかった。** 通常のログイン画面も、OAuth の認可エンドポイントも、識別子の入力画面まで表示された。Microsoft の公式ドキュメントは Google 認証が WebView2 で無効としているが、実際の挙動は異なる。

考えられる説明:

1. Google の enforcement が変わった、またはクライアント/フローによって扱いが異なる。
2. ブロックが認証後の同意画面や、特定の OAuth クライアント、特定のスコープで発生する。
3. WebView2 の UA が Google のページでは `Edg/` を落として Chrome に見えるため、検知を免れている。
4. アカウントの状態やリージョンに依存する。

**まだ言えないこと**: 実際のアカウントでパスワードを入力した後の挙動、同意画面、Gmail の利用、OAuth のコールバック。これらは未検証である。資格情報を扱わない方針のため、このプローブでは実施しない。

### 4.2 パスキー

**UVPAA は true で、webauthn.io と GitHub の操作で `Windows セキュリティ` のプロンプトが起動した。** これは、WebView2 がプラットフォーム認証器を公開し、Windows Hello の UI を呼び出せたことを示す。

`WebView2Feedback #5663` は「Windows Hello が WebView2 で動かない」と報告しているが、少なくともこの環境（Windows 11 Home、WebView2 155）では再現しない。VDI、Windows Server、ポリシー、認証器の設定に依存する可能性がある。

**まだ言えないこと**: プロンプトで実際に認証を完了できるか、登録した資格情報が再利用できるか、サイト側が attestation を要求したときに通るか。プロンプトの完了にはユーザーの生体認証が必要で、自動化できない。

### 4.3 フィンガープリント

`window.chrome.webview` は全ページで存在した。Google はこれで WebView2 を検知できる。それでもブロックしなかったため、検知とブロックは別である。

## 5. 設計への影響

1. **Google ログインを「即失格」と扱わない。** Gate 0 では「PX1 で完全ログインを確認できなければ失格」に変更する。完全ログインは未検証である。
2. **パスキーを「不安定・失格に近い」と扱わない。** 「環境依存。主要サイトで登録・認証を確認する」に変更する。
3. **公式ドキュメントと実機の差を記録する。** 公式が「無効」と言っても、実機で再現するとは限らない。逆も同じである。
4. **完全ログインの試験を追加する。** 使い捨てのテストアカウントをユーザーが用意できる場合だけ、手動で行う。資格情報は保存しない。

## 6. 限界

- 1 台の Windows 11 Home、1 つの WebView2 Runtime での結果である。
- 実際の Google アカウントでログインしていない。同意画面、Gmail、OAuth コールバックは未検証。
- Windows Hello のプロンプトは起動したが、認証は完了していない。
- User-Agent がサイトによって異なる理由は未解明。
- デスクトップ全体のスクリーンショットを一度取得したが、ユーザーの画面が映るため削除した。証拠には含めない。

## 7. 次の試験

1. 使い捨ての Google アカウントで、OAuth の同意画面まで手動で進む（資格情報は保存しない）。
2. Gmail と Google ドライブを開き、通常利用ができるか確認する。
3. パスキーの登録を最後まで完了し、再ログインできるか確認する。
4. 別のマシン（VDI、Windows Server、別の WebView2 バージョン）で同じ試験を行う。
5. 公式ドキュメントの「Google 認証は無効」が、どの条件で再現するかを切り分ける。
