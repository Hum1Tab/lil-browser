# WebView2 ベースの軽量ブラウザは成立するか

## 要旨

本稿の結論は、「メモリは軽いが、起動は Edge より遅く、広告遮断はカバレッジで劣る。『軽量 + 高速』の看板では成立しない」である。Windows 向けの軽量ブラウザを WebView2 で作る場合、エンジンのセキュリティ更新とサンドボックスは Microsoft に委ねられる。その代わり、公式には Google アカウント認証、Widevine DRM、ブラウザ拡張、Web Push、Web Payment を失うとされている。ただし、PX1 の実機試験では Google の OAuth は識別子画面まで表示され、パスキーのプロンプトも起動した。完全ログインと DRM は未検証である。PX3c では暖機後の起動が Edge より遅いと確認された。実装基盤には Tauri v2 ではなく、Rust とネイティブ UI による WebView2 の直接制御を推奨するが、それは「軽量 + 高速」の製品としてではない [C19][C20][C36]。

## 1. 問いと方法

問いは 3 つである。第一に、既存ブラウザより軽くできる合理的な根拠はあるか。第二に、セキュリティを犠牲にせずにそれを達成できるか。第三に、日常利用に耐える互換性を確保できるか。

方法は、一次資料の確認と、PX1 の限定実機試験に限定した。Microsoft Learn の WebView2 リファレンス、Tauri の公式ドキュメントと本体ソース、Google の OAuth ポリシー、Chromium の設計文書、各プロジェクトのライセンスを直接取得して読んだ。PX1 以外の個別サイト試験と性能測定は未実施であり、本稿の性能に関する記述は目標か予測である。出典は `[Sxx]`、主張は `[Cxx]` で示す。

## 2. エンジンの選択

最優先の要求は、セキュリティ更新を継続できることである。Chromium は 2 週間ごとにマイルストーンを出し、毎週セキュリティ更新を配信する [C13][S29]。この速度に追随できない fork は、機能が多くても防御が弱い。小規模チームが Chromium をフォークすると、更新遅延によってセキュリティがかえって悪化し得る [C18]。

WebView2 は Edge と同じプロセスモデルを使い、サイト分離を維持する [S05][S26]。Evergreen は Microsoft が自動更新する [S15]。ただし、実行中のアプリは再起動するまで旧ランタイムを使い続けるため、更新検知時に再起動を促す設計が要る [C14]。プロファイルは同一の user data folder 内で分離でき、ブラウザプロセスを増やさない [C22][S07]。Enhanced Security Mode の Strict は JIT を無効化し、OS の保護を追加する [S14]。追跡防止のレベルもプロファイル単位で設定できる [S13]。

CEF は制御性が高いが、配布物が大きく、更新追随の責任は開発側に残る。WebView2 の Fixed Version が 250MB を超えることが、Chromium ランタイムの規模を間接的に示す [S15]。そして CEF にしても、拡張、Google 認証、Widevine が自動的に解決するわけではない。

## 3. 互換性の壁

最初の壁は拡張機能である。WebView2 はブラウザ拡張を提供せず、設定でも有効化できない [C01][S08]。したがって uBlock Origin は使えない。広告遮断は、自前で実装するか、遮断を諦めるか、外部ブラウザに委ねるかの三択になる。本設計は自前を選ぶ。

第二の壁は Google 認証である。Google は公式には埋め込み WebView の OAuth を拒否し、WebView2 も対象とされている [C02][S08][S20][S21]。Google は拒否理由として、ホストが通信を傍受し、スクリプトを注入できることを挙げている [S20]。ただし、PX1 の実機試験では、WebView2 で Google の OAuth 認可エンドポイントが識別子画面を表示し、`disallowed_useragent` は出なかった [C29][S47]。完全ログイン、同意画面、Gmail は未検証である。公式文書と実機の差を、設計の前提にしない。

第三の壁は DRM である。公式には WebView2 は Widevine を提供しないとされてきた [C03][S33]。しかし PX4 では、`com.widevine.alpha` と `com.microsoft.playready` の両方が `requestMediaKeySystemAccess` で supported を返した [C31][S48]。Netflix と Spotify のページは表示された。実際の再生、ライセンス取得、HDCP、robustness は未検証である。

第四の壁は Web プラットフォームの欠落である。Web Push、Web Payment API、Periodic Background Sync は提供されない [C17][S08]。タブを閉じた後の通知、Web 決済の一部、オフライン同期が動かない。

パスキーは、PX1 の実機試験で `UVPAA=true` と Windows Security のプロンプト起動を観測した [C30][S47]。ただし、認証の完了、登録の再利用、attestation は未検証である。UI は OS 管理で、本製品から制御できない [C21][S34]。主要サイトで完了できるまでは「条件付き」と表示する。

## 4. 性能の現実

WebView2 のタブは 1 つのブラウザプロセスを共有し、レンダラはサイト分離に応じて共有される [S05]。したがって、タブごとに WebView2 を作っても、プロセス数はタブ数ではなく異なるサイト数に近づく。ホストが制御できるのは、ホスト自身の固定費と、タブのサスペンド・破棄である。

サスペンドは `TrySuspendAsync` で行う。タブを眠らせることに相当し、成功は best-effort である [C08][S11]。音声再生中などは失敗する。サスペンドを使わないタブには `MemoryUsageTargetLevel` の Low を使う [S12]。両者は混在させない。

参考値として、Chromium のサイト分離はデスクトップで約 10〜13% のメモリ増加をもたらした [C09][S26]。Edge の sleeping tabs は、Microsoft の報告で平均 32% のメモリ削減と 37% の CPU 削減である [C10][S27]。Chrome の Memory Saver は、一定時間でタブを破棄し、再訪時に再読み込みする [S28]。これらはベンダー公表値を含むため、独立検証済みの数値としては扱わない。

本設計では、直近 3 枚を Warm、それ以外を Cool、Cold はメモリ逼迫時と手動時だけにする。フォーム入力中のタブを落とすコストの方が、メモリ削減の便益より大きいと判断した。目標は、5 タブの総メモリが Edge 以下、Warm 切替 p95 が 100ms 未満である。これは目標であり、実測で判定する。

## 5. 広告遮断の天井

WebResourceRequested は、フィルタに合致するリクエストをホストに通知する。ホストはレスポンスを差し替えられる [S09]。このイベントはホストの UI スレッドで発火し、ページの読み込みを止めて応答を待つ [C04][S10]。ただし、PX2 では 500 リクエストの全件フック（all-allow）とフックなし（none）のナビゲーション時間に差がなかった（324ms vs 321ms）[C32][S49]。この規模では、往復コストは支配的でない。実サイト、1000 リクエスト、UI 負荷時は未検証である。

さらに、イベント引数には発信元フレームの URL が無い。Request が持つのは URI、メソッド、ヘッダ、Content だけである [C05][S17][S18]。adblock-rust の判定は source_url を必要とするため [C11][S23]、third-party 判定と `$domain` は近似になる。サービスワーカーの要求は種別で区別できるが、文脈は無い [S19]。

対応できるのは、ドメイン、パス、正規表現、リソース種別といったコンテキスト非依存のルールである。`$removeparam` は Win32 の `put_Uri` で部分対応できる [S17]。`$csp` はレスポンスを変更できないため非対応である [S09]。スクリプトレットはページと同じ権限で動くため、既定では無効にする。フィルタのライセンスは、adblock-rust が MPL-2.0 [S22][S24]、EasyList が GPL-3.0+ または CC BY-SA 3.0+ である [C12][S25]。

cosmetic 注入は全フレームに及ぶ [C16][S36]。ページからホストへのメッセージを許さない方針のため、ドメイン別のルールをページ側から要求できない。汎用 CSS と、ナビゲーション後の注入を併用する。YouTube の広告除去はベストエフォートであり、完全除去を約束しない。

## 6. Tauri の是非

Tauri v2 は、小さいバイナリ、ウィンドウ、バンドラ、署名付きアップデータを提供する [S04][S35]。試作には適する。しかし製品の土台には向かない [C20]。

第一に、Tauri は生成する全 WebView に `isTauri` と `__TAURI_INTERNALS__` を注入する [C06][S02]。リモート URL のページにも入る。capability を絞ればコマンド実行は拒否できるが [S01]、ブリッジオブジェクトはページに残る。ブラウザは、非信頼コンテンツに何も注入しないのが原則である。

第二に、1 ウィンドウに複数の WebView を置く API は unstable feature である [C07][S03]。タブ配置は中核機能であり、unstable な API に依存したくない。

第三に、ブラウザに必要な通信フック、プロファイル、サスペンドは、Tauri の公開 API では足りない。結局 COM を直接叩くなら、抽象を二重に持つ意味は小さい。wry を直接使う選択肢もあるが [S36]、いずれにせよ WebView2 の制御を自前で持つことになる。

## 7. セキュリティとプライバシー

ホストは中整合性で動かし、管理者権限を要求しない。コンテンツページにはホストオブジェクトを公開せず、Web メッセージも無効にする [S16]。これは WebView2 の公式な推奨と一致する。IPC ブリッジを注入しないため、ページからホストの API を叩く経路は無い。

保存は、Cookie、サイトデータ、履歴、設定に限定する。Cookie と履歴だけに絞ると、LocalStorage や IndexedDB を必要とするサイトが壊れる [C15][S06]。パスワード、オートフィル、支払い情報、同期、テレメトリは保存しない。プライベートモードは InPrivate プロファイルで分離する [S07]。PX5 では、InPrivate のサイトデータ（cookie / history / localStorage / IndexedDB）は再起動後に残らなかった。一方、空の History / Cookies DB と Local Storage LevelDB、Code Cache、Crashpad は UDF に残った [C37][S53]。

測定は、Job Object でプロセスツリーを囲み、Private Working Set を合計する [S30][S31]。タブ切替のフレーム提示は PresentMon で確認する [S32]。これにより、共有メモリの二重計上と、子プロセスの計測漏れを避ける。

保証できないことも明記する。エンジンの 0-day、同一ユーザー権限のマルウェアによる Cookie 窃取、削除の物理的な完全性、フィッシングの判定、WebRTC の IP 漏洩は、設計だけでは防げない。

## 8. 最強の反論

最も強い反論は、「Google ログインが不要なユーザーなど、ほとんど存在しない」である。この反論が正しければ、製品の対象は著しく狭まり、事業として成立しない。本稿の推奨は、この反論を否定しない。だからこそ、Phase 0 で対象ユーザーを調査し、Google ログインが必要な利用が半数を超えるなら中止すると決めた。

第二の反論は、「WebView2 で十分なら、Edge を使えばよい」である。これは半分正しい。Edge は sleeping tabs を持ち、同期も拡張もある [S27]。本製品が勝てるのは、固定費、プライバシーの既定値、攻撃面の小ささであって、機能ではない。この差がユーザーにとって意味を持つかは、実測と聞き取りでしか分からない。

第三の反論は、「ネイティブ UI のコストが高すぎる」である。これも正しい。Tauri の Web UI なら実装は速い。しかし Web UI はレンダラをもう 1 つ必要とし、固定費が増える。性能を主目的にするなら、ネイティブ UI のコストは受け入れる必要がある。受け入れられないなら、製品の主目的を性能からプライバシーに移すべきである。

## 9. 限界

本稿には、次の限界がある。第一に、個別サイトの実機試験をしていない。互換性の記述は、プラットフォーム能力からの推定である。第二に、性能を測っていない。数値は目標か、他社の公表値である。第三に、フィルタのライセンス解釈は法務確認前である。第四に、WebView2 の Cookie 暗号化の詳細は未検証である。InPrivate のサイトデータ非永続は PX5 で確認したが、プロファイルファイルは残る。第五に、CEF と Chromium フォークの実際の保守コストは、文献からの推定である。

これらは設計の前提にしない。Phase 0 と実機試験で確認し、結果に応じて主張を修正する。

## 10. 結論

構成 B' を「軽量 + 高速」のブラウザとして推奨することはできない。PX3c で暖機後起動が 1 タブ 481ms vs Edge 293ms、5 タブ 456ms vs 310ms となり、起動の優位が否定された [C36][S52]。B' を選ぶなら、看板を「メモリが軽い・プライバシー・制御」に限定し、起動の速さを主張しない。その製品に十分な需要があるかは未検証である。Google の完全ログインと Widevine の実再生が PX で確認できない場合は、それらを対象外にする [C19][C20]。

構成 A（Chromium フォーク）は、専任チームと Widevine の契約がある場合にだけ意味がある。小規模チームでは、更新遅延がセキュリティ後退になる [C18]。構成 C（Tauri プロトタイプ）は、計測専用であり、製品の土台にはしない [C06][C07]。

この判断は、性能の追求を諦めたものではない。性能を追求するために、互換性の一部を製品契約として明示し、更新の継続性を確保したのである。達成できない性能や、保証できない安全性を、事実として扱わないことが、この設計の立場である。

## 11. 追記: 反証レビューで追加した証拠

反証レビュー（13）で、設計に不利な一次資料を追加した。

第一に、Windows Hello のパスキーが WebView2 で動かないという報告がある [C23][S38]。一方で、PX1 の実機試験では `UVPAA=true` と Windows Security のプロンプト起動を観測した [C30]。報告と実機が矛盾するため、環境依存と判断する。認証完了は未検証である。

第二に、WebSocket では WebResourceRequested が発火しない [C24][S39]。したがって `$websocket` は実装できず、WebSocket を使う広告やトラッカーは素通りする。

第三に、InPrivate でもプロファイルファイルはディスクに作られ、in-memory モードは無い [C25][S41]。PX5 ではサイトデータの行は残らなかったが、History / Cookies DB（0 行）と Local Storage LevelDB、Code Cache、Crashpad は残った [C37][S53]。プライバシー表示で「痕跡が残らない」とは言えない。

第四に、Microsoft の性能ガイドは、コールドスタートを既知のボトルネックとし、各コントロールがプロセス群を起動すると説明する [C26][S40]。5 タブメモリと起動で Edge を上回れるかは、実測でしか決められない。

第五に、CEF は Alloy スタイルで chrome://extensions をブロックするが、RequestContext レベルの拡張機能は動く [C27][S42]。CEF を「拡張が使えない」と切り捨てるのは早すぎた。Chrome スタイルの CEF は、uBO と Google ログインを検証する価値がある。

第六に、WebView2 を AppContainer から動かせる [S43]。ホストの隔離を強化できる可能性がある。wry は controller と environment を公開するため、Tauri を介さずに WebView2 を制御できる [S44]。Brave の adblock-lists は MPL-2.0 で、リスト再利用の候補になる [S45]。reCAPTCHA やポップアップを使うログインの失敗報告もある [S46]。

第七に、PX1 の実機試験（Windows 11、WebView2 155）では、Google の OAuth が識別子画面まで表示され、パスキーのプロンプトが起動した [C29][C30][S47]。これは「Google ログインは即失格」「パスキーは動かない」という一般化を、少なくともこの環境では支持しない。ただし、完全ログイン、同意画面、Gmail、認証完了は未検証である。公式文書と実機の差を、どちらか一方に決めつけない。

第八に、PX4 では WebView2 の EME が Widevine と PlayReady を supported と返した [C31][S48]。Netflix と Spotify のページは表示されたが、実再生は未検証である。Bitmovin の DRM デモは manifest の読み込みに失敗した。「Widevine 非対応」という一般化も、この環境では支持しない。

第九に、PX2 では 500 リクエストの全件フックでナビゲーション時間の悪化が観測されず、遮断による短縮だけが見られた [C32][S49]。広告遮断の制約は、速度よりもカバレッジ（frame URL、WebSocket、CSP）にある。

第十に、PX3 の初期実測では、1/5/20 タブの Private メモリが Edge を下回った（204 vs 373、290 vs 494、769 vs 852 MB）[C33][S50]。ただし各条件 1 回、軽量・同一オリジンである。起動は、本が全タブ読込完了、Edge がウィンドウ表示で定義が異なり、速いとは言えない [C34]。

第十一に、PX3b では `load` ビーコンで起動を同じ定義にした。1 タブは 5.13 vs 5.90 秒、5 タブは 4.24 vs 7.56 秒、20 タブは 7.47 vs 6.73 秒（本 vs Edge、各 3 回の中央値）[C35][S51]。5 タブでは遅延復元の効果が見えるが、1 タブの差は小さく、20 タブでは逆転した。分散が大きく、起動の優位はまだ結論できない。

第十二に、PX3c では同じ UDF / プロファイルを再利用した暖機後起動を測った。1 タブ 481.5 vs 293.1ms、5 タブ 456.3 vs 310.5ms で Edge が速い。20 タブは本 533.8 vs Edge 588.8ms だが、本は最初のタブのみである [C36][S52]。「起動が速い」は支持されない。

第十三に、PX6 の UI プロトタイプは 404 行・14 コントロールで、タブ、アドレスバー、履歴、設定、権限、ダウンロードの基本操作を実装した [C38][S54]。生産 UI は、タブモデル、永続化、アクセシビリティ、DPI、復元、更新 UI を含めると 4〜6 か月と推定される [C39]。M1 の 10〜14 週は UI だけで過小である。

第十四に、PX7（CEF Chrome スタイル）は MSVC / cmake / Visual Studio が無いため未実施である。CEF のビルドインデックスは取得できたが、ビルドと実行はしていない。CEF の成立は未検証である [C40][S55]。

これらを踏まえると、「Google ログインを必要としないユーザー」という対象定義は、エンジンの制約から逆算したものであり、需要の証拠ではない [C28][S37]。実装前に PX1〜PX7 で、識別子、遮断コスト、軽さ、DRM、プライバシー、UI 工数、CEF を検証する必要がある。

## 参考文献

- [S01] Tauri v2 Capabilities. https://v2.tauri.app/security/capabilities/
- [S02] tauri/crates/tauri/src/manager/webview.rs. https://raw.githubusercontent.com/tauri-apps/tauri/dev/crates/tauri/src/manager/webview.rs
- [S03] tauri/crates/tauri/src/webview/mod.rs. https://raw.githubusercontent.com/tauri-apps/tauri/dev/crates/tauri/src/webview/mod.rs
- [S04] Tauri App Size. https://v2.tauri.app/concept/size/
- [S05] Process model for WebView2 apps. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model
- [S06] Manage user data folders. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
- [S07] Support multiple profiles under a single user data folder. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/multi-profile-support
- [S08] Differences between Microsoft Edge and WebView2. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/browser-features
- [S09] Custom management of network requests. https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webresourcerequested
- [S10] Using local content in WebView2 apps. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content
- [S11] CoreWebView2.TrySuspendAsync. https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.trysuspendasync
- [S12] CoreWebView2.MemoryUsageTargetLevel. https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.memoryusagetargetlevel
- [S13] CoreWebView2Profile. https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile
- [S14] CoreWebView2EnhancedSecurityModeLevel. https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2enhancedsecuritymodelevel
- [S15] Distribute your app and the WebView2 Runtime. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
- [S16] Develop secure WebView2 apps. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security
- [S17] ICoreWebView2WebResourceRequest. https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2webresourcerequest
- [S18] CoreWebView2WebResourceRequestedEventArgs. https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2webresourcerequestedeventargs
- [S19] CoreWebView2WebResourceRequestSourceKinds. https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2webresourcerequestsourcekinds
- [S20] Google Developers Blog. https://developers.googleblog.com/upcoming-security-changes-to-googles-oauth-20-authorization-endpoint-in-embedded-webviews/
- [S21] OAuth 2.0 for Desktop Apps. https://developers.google.com/identity/protocols/oauth2/native-app
- [S22] adblock-rust README. https://github.com/brave/adblock-rust
- [S23] adblock Engine documentation. https://docs.rs/adblock/latest/adblock/struct.Engine.html
- [S24] GitHub license metadata. https://api.github.com/repos/brave/adblock-rust
- [S25] EasyList Repository Licences. https://easylist.to/pages/licence.html
- [S26] Site Isolation. https://www.chromium.org/Home/chromium-security/site-isolation/
- [S27] Sleeping Tabs in Microsoft Edge. https://blogs.windows.com/msedgedev/2020/12/09/sleeping-tabs-beta-performance/
- [S28] Chrome Memory and Energy Saver modes. https://developer.chrome.com/blog/memory-and-energy-saver-mode
- [S29] Chrome Release Cycle. https://chromium.googlesource.com/chromium/src/+/main/docs/process/release_cycle.md
- [S30] Job Objects. https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects
- [S31] GetProcessMemoryInfo. https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo
- [S32] PresentMon. https://github.com/GameTechDev/PresentMon
- [S33] WebView2Feedback issue 4828. https://github.com/MicrosoftEdge/WebView2Feedback/issues/4828
- [S34] WebView2Feedback issue 5460. https://github.com/MicrosoftEdge/WebView2Feedback/issues/5460
- [S35] Tauri Updater. https://v2.tauri.app/plugin/updater/
- [S36] wry WebViewBuilder. https://docs.rs/wry/latest/wry/struct.WebViewBuilder.html
- [S37] Google OAuth 2.0 Policies. https://developers.google.com/identity/protocols/oauth2/policies
- [S38] WebView2Feedback issue 5663. https://github.com/MicrosoftEdge/WebView2Feedback/issues/5663
- [S39] WebView2Feedback issue 4303. https://github.com/MicrosoftEdge/WebView2Feedback/issues/4303
- [S40] Performance best practices for WebView2 apps. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance
- [S41] WebView2Feedback issue 3637. https://github.com/MicrosoftEdge/WebView2Feedback/issues/3637
- [S42] CEF issue 3859. https://github.com/chromiumembedded/cef/issues/3859
- [S43] WebView2Feedback issue 198. https://github.com/MicrosoftEdge/WebView2Feedback/issues/198
- [S44] wry src/lib.rs. https://raw.githubusercontent.com/tauri-apps/wry/dev/src/lib.rs
- [S45] brave/adblock-lists license. https://api.github.com/repos/brave/adblock-lists
- [S46] WebView2Feedback issue 455. https://github.com/MicrosoftEdge/WebView2Feedback/issues/455
- [S47] PX1 WebView2 identity probe. prototype/reports/px1-identity.md
- [S48] PX4 WebView2 DRM/EME probe. prototype/reports/px4-drm.md
- [S49] PX2 adblock cost probe. prototype/reports/px2-adblock.md
- [S50] PX3 multi-tab memory and startup probe. prototype/reports/px3-memory.md
- [S51] PX3b fair startup probe. prototype/reports/px3b-startup.md
- [S52] PX3c warm startup probe. prototype/reports/px3c-startup.md
- [S53] PX5 privacy probe. prototype/reports/px5-privacy.md
- [S54] PX6 UI prototype and effort estimate. prototype/reports/px6-ui.md
- [S55] PX7 CEF feasibility check. prototype/reports/px7-cef.md
