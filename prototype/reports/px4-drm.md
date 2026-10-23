# PX4: DRM / EME プローブ結果

日付: 2026-10-23
環境: Windows 11 Home 10.0.26200、WebView2 Runtime 155.0.4283.62
コード: [`prototype/px1-webview2/`](../px1-webview2/)
生データ: [`prototype/results/px4-drm/`](../results/px4-drm/)、[`px4-play/`](../results/px4-play/)

## 1. 目的

「WebView2 は Widevine を提供しない」という設計上の前提を、EME（Encrypted Media Extensions）で反証する。実際の再生可否は別途確認する。

## 2. 方法

WebView2 で `https://example.com/` を開き、次の 3 つのキーシステムに対して `navigator.requestMediaKeySystemAccess` を呼んだ。

- `com.widevine.alpha`
- `com.microsoft.playready`
- `org.w3.clearkey`

要求した構成は、`cenc`、AAC 音声（`mp4a.40.2`）、H.264 映像（`avc1.42E01E`）。結果を JSON で保存した。あわせて Bitmovin の DRM デモ、Netflix のトップ、Spotify Web を開き、ページの表示と video 要素の状態を記録した。

## 3. 結果

### 3.1 EME

```json
{
  "com.widevine.alpha": "supported:com.widevine.alpha",
  "com.microsoft.playready": "supported:com.microsoft.playready",
  "org.w3.clearkey": "supported:org.w3.clearkey"
}
```

**Widevine と PlayReady の両方が `supported` を返した。**

### 3.2 ページ

| URL | 結果 |
|---|---|
| `https://www.netflix.com/jp/` | トップページが表示。ログイン前。タイトル「Netflix (ネットフリックス) 日本」 |
| `https://open.spotify.com/intl-ja/` | Web プレイヤーが表示。ログイン前。曲は未再生 |
| `https://bitmovin.com/demos/drm` | ページは表示。manifest の読み込みに失敗（`SOURCE_COULD_NOT_LOAD_MANIFEST`）。`video.readyState=0`、`duration=null` |

Bitmovin の失敗は、DRM ライセンスではなく、manifest の取得（ネットワーク、同意ダイアログ、CORS など）に起因する可能性がある。この結果だけでは、Widevine の実再生が通るとは言えない。

## 4. 解釈

1. **WebView2 の EME は Widevine と PlayReady を認識する。** これは、CDM がランタイムに存在し、`requestMediaKeySystemAccess` が成功することを意味する。
2. **実再生は未検証である。** ライセンスサーバーからの鍵取得、HDCP、 robustness、Netflix のポリシー、ユーザーエージェントの判定は別である。
3. **「Widevine 非対応」という一般化は、この環境では再現しない。** `WebView2Feedback #4828` の報告は、古いバージョン、特定の環境、または特定の構成に依存する可能性がある。
4. **Netflix と Spotify のページは表示される。** ログイン前なので、再生の可否は分からない。

## 5. 設計への影響

- Gate 0 の「Widevine が必須なら WebView2 は失格」を、「PX4 で実再生を確認。EME は supported」に変更する。
- 製品の対象を「DRM を必要としない用途」に限定する根拠は弱くなった。ただし、実再生が通るまでは、動画配信を主目的にしない。
- 公式ドキュメントと実機の差を、再び記録する。EME の supported は、再生の保証ではない。

## 6. 限界

- 1 台の Windows 11 Home、1 つの WebView2 Runtime での結果である。
- ライセンスサーバーを使う実際の再生をしていない。
- Netflix、Spotify はログインしていない。
- Bitmovin の失敗原因を切り分けていない。
- Widevine の robustness / HDCP / 4K の可否は未検証。

## 7. 次の試験

1. 公開の Widevine テストストリーム（Shaka Player のデモ、Bitmovin の同意後のデモ）で、`video.readyState` が 4 になり、`currentTime` が進むか確認する。
2. ライセンス要求（`generateRequest` / `update`）が成功するか、CDP の Network で確認する。
3. 使い捨ての Netflix アカウントで、SD 再生が通るか確認する（資格情報は保存しない）。
4. Spotify Web で、ログイン後に再生が通るか確認する。
5. 別の WebView2 バージョン、Windows N、VDI で同じ試験を行う。
