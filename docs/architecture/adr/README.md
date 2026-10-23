# Architecture Decision Records

| ID | 決定 | 状態 |
|---|---|---|
| [ADR-0001](ADR-0001-engine-webview2.md) | エンジンに WebView2 を採用する | 採用 |
| [ADR-0002](ADR-0002-no-tauri-in-product.md) | 製品に Tauri v2 を使わない | 採用 |
| [ADR-0003](ADR-0003-scope-excludes-google-identity-and-drm.md) | Google 認証と DRM を対象外にする | 採用（条件付き） |
| [ADR-0004](ADR-0004-adblock-rust-webresourcerequested.md) | 広告遮断は adblock-rust + WebResourceRequested のハイブリッド | 採用 |
| [ADR-0005](ADR-0005-persist-site-data.md) | Cookie と履歴だけでなくサイトデータも保存する | 採用 |
| [ADR-0006](ADR-0006-native-ui.md) | UI はネイティブ（Win32）にする | 採用 |
| [ADR-0007](ADR-0007-no-telemetry.md) | テレメトリを実装しない | 採用 |
| [ADR-0008](ADR-0008-signed-updates-and-filters.md) | 更新とフィルタは署名検証を必須にする | 採用 |

## 書き方

- 1 つの決定に 1 ファイル。
- 「文脈」「決定」「結果」「代替案」「再検討のトリガー」を書く。
- 事実と推測を分ける。出典は `[Sxx]` で示す。
