# PX5: InPrivate のディスク残留プローブ結果

日付: 2026-10-23
環境: Windows 11 Home 10.0.26200、WebView2 Runtime 155.0.4283.62
コード: [`prototype/px1-webview2/`](../px1-webview2/)（`--inprivate --udf`）
ページ: [`prototype/px5-server/server.py`](../px5-server/server.py)
生データ: [`prototype/results/px5-normal/`](../results/px5-normal/)、[`px5-inprivate/`](../results/px5-inprivate/)、[`px5-normal2/`](../results/px5-normal2/)、[`px5-inprivate2/`](../results/px5-inprivate2/)

## 1. 目的

「InPrivate はディスクに何を残すか」を、実際の UDF の中身で確認する。設計は「InPrivate でもプロファイルがディスクに作られる」としていたが、サイトデータが残るのか、ファイルだけ残るのかを分離する。

## 2. 方法

- PX1 アプリに `--inprivate` と `--udf` を追加。`CoreWebView2CreationProperties.IsInPrivateModeEnabled` を使う。
- ページ `/set` で次を設定する。
  - `document.cookie = 'px5cookie=1'`
  - `localStorage.px5local = '1'`
  - `sessionStorage.px5session = '1'`
  - IndexedDB `px5db` に `px5idb`
  - CacheStorage に `px5cache`
- 同じセッションで `/read` を開き、値が見えることを確認。
- アプリを終了し、同じ UDF で `/read` だけを再実行。
- UDF を SQLite（History / Cookies）とバイト走査（LevelDB / Cache）で検査。
- 通常モードと InPrivate モードを別 UDF で比較。

## 3. 結果

### 3.1 セッション内と再起動後

| モード | セッション内 `/read` | 再起動後 `/read` |
|---|---|---|
| 通常 | cookie=px5cookie=1、local=1、session=1、idb=px5idb | cookie=px5cookie=1、local=1、session=空、idb=px5idb |
| InPrivate | cookie=px5cookie=1、local=1、session=1、idb=px5idb | cookie=空、local=空、session=空、idb=空 |

- 通常モードは cookie / localStorage / IndexedDB が再起動後も残る。sessionStorage は残らない（期待どおり）。
- **InPrivate は再起動後にすべて空。** サイトデータは永続しない。

### 3.2 SQLite の中身

| 項目 | 通常 | InPrivate |
|---|---:|---:|
| History の URL 行数 | 2 | **0** |
| History の visit 行数 | 3 | **0** |
| Cookies の行数 | 1 | **0** |

InPrivate の History / Cookies DB はファイルとして存在するが、行は 0 である。

### 3.3 UDF に残るファイル

| ディレクトリ | 通常 | InPrivate |
|---|---:|---:|
| History | 249,856B | 249,856B（0 行） |
| Network\Cookies | 20,480B | 20,480B（0 行） |
| Local Storage | 6 ファイル / 833B | 6 ファイル / 642B（データログ 30B） |
| IndexedDB | 6 ファイル / 1,961B | **無し** |
| Session Storage | 5 ファイル / 345B | **無し** |
| Cache | 7 ファイル / 856,456B | **0 ファイル** |
| Code Cache | 4 ファイル / 144B | 4 ファイル / 144B |
| GPUCache | 5 ファイル / 557,424B | **無し** |
| Service Worker | 4 ファイル / 492B | **無し** |
| WebStorage | 2 ファイル / 40,960B | **無し** |
| Crashpad | 3 ファイル / 308B | 3 ファイル / 308B |

UDF 全体のファイル数は、2 回の実行後で通常 198、InPrivate 153。1 回目の実行直後は通常 179、InPrivate 134 だった。

### 3.4 バイト走査

- 通常の UDF では、`px5` / `127.0.0.1:8767` が History、IndexedDB、Local Storage、CacheStorage、Cookies、Session Storage などから見つかった。
- InPrivate の UDF では、`px5` / `127.0.0.1:8767` は **サイトデータのファイルから見つからなかった**。
- 唯一の `px5` ヒットは、UDF のパス名（`C:\Temp\px5-inprivate`）を含む `favorites_diagnostic.log` と LevelDB の `LOG` であった。これはサイトデータではない。

## 4. 解釈

1. **InPrivate はサイトデータを永続しない。** cookie、履歴 URL、localStorage、IndexedDB の行は、再起動後に残らない。これは設計の想定より良い。
2. **しかし「跡形もなく消える」わけではない。** History DB 249,856B、Cookies DB 20,480B、Local Storage LevelDB、Code Cache、Crashpad は UDF に残る。ファイルの存在、サイズ、更新時刻から、InPrivate を使ったことは分かる。
3. **UDF を削除するまで消えない。** in-memory モードは無い。終了時削除を「要求」しても、クラッシュや電源断では完了しない可能性がある。
4. **Cache、IndexedDB、Session Storage、Service Worker、GPUCache、WebStorage は InPrivate では作られない。** これらを作らないことで、サイトデータの残留を防いでいる。
5. **表示は「サイトデータは残らないが、プロファイルの痕跡は残る」にする。** 「痕跡が残らない」「完全に消えた」は誤り。

## 5. 設計への影響

- `07` の「InPrivate でもプロファイルがディスクに作られる」は維持しつつ、**「サイトデータの行は残らない」** を追加する。
- 「終了時に削除」は、UDF の削除まで含める必要がある。プロファイルの削除は best-effort であり、完了を保証しない。
- プライバシー表示は、次の 2 段階に分ける。
  - セッション内: サイトデータはメモリ / 一時プロファイルのみ。
  - セッション後: サイトデータの行は残らない。プロファイルファイルは残るため、UDF 削除を要求する。
- AppContainer は未検証。UDF の ACL と AppContainer の組み合わせは、PX5b で確認する。

## 6. 限界

- 1 台、1 バージョン、ローカルページ。実サイトの Service Worker、CacheStorage、動画、Cookie の SameSite / Secure を含まない。
- クラッシュ、電源断、強制終了を再現していない。
- UDF のファイル名・サイズ・更新時刻は、Windows のファイルシステムとバックアップに残る。
- SQLite のフリーリスト、LevelDB の削除済み領域、SSD のウェアレベリングは検査していない。
- AppContainer、Windows Search、EDR、ページファイル、休止状態ファイルは未検証。
- InPrivate の UDF は同じパスを使い回した。別の UDF にすれば、ファイルの痕跡はさらに減る可能性がある。

## 7. 次の試験（PX5b）

1. AppContainer で WebView2 を起動できるか。UDF の ACL と AppContainer SID を確認する。
2. クラッシュ / 電源断後の UDF 残留を確認する。
3. UDF 削除後のファイルシステム残留（USN ジャーナル、VSS、Windows Search）を確認する。
4. 実サイト 10 件で、InPrivate の CacheStorage / Service Worker / Cookie の残留を確認する。
5. 「終了時削除」が UDF 削除まで完了するか、失敗時の再試行を実装して測る。
