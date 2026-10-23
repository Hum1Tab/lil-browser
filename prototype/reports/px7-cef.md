# PX7: CEF Chrome スタイルの実行可否（未実施）

日付: 2026-10-23
環境: Windows 11 Home 10.0.26200、.NET SDK 8.0.426
状態: **未実施（配布物は確認。`cefclient.exe` は含まれず、ビルド環境なし）**

## 1. 目的

WebView2 が Gate 0 を通らない場合の代替として、CEF Chrome スタイル（uBO、Google ログイン、EME）が成立するかを確認する。

## 2. 確認したこと

- `cl.exe`（MSVC コンパイラ）: **見つからない**
- `msbuild.exe`: **見つからない**
- Visual Studio のインストール: **見つからない**
- `cmake.exe`: **見つからない**
- CEF のビルドインデックス `https://cef-builds.spotifycdn.com/index.json`: **HTTP 200、10,542,669 バイト**
- CEF 標準配布物 `156.0.7+g47dbc1d+chromium-156.0.8078.17_windows64` を **ダウンロード（366,862,060 バイト）して展開した**。

### 2.1 配布物の中身

`Release` に含まれるのは、`bootstrap.exe`、`bootstrapc.exe`、`libcef.dll`、`libcef.lib`、`chrome_elf.dll`、`v8_context_snapshot.bin`、`vk_swiftshader.dll`、`vulkan-1.dll`、`d3dcompiler_47.dll`、`dxcompiler.dll` などである。

- `cefclient.exe`: **無い**
- `cefsimple.exe`: **無い**
- `tests/cefclient` / `tests/cefsimple`: **ソースのみ**

`bootstrap.exe` を実行すると、`Missing module name` で終了する（exit code `-2147483645`）。単体ではブラウザとして動かない。

`README.txt` は、次のビルド手順を要求している。

```text
cmake -S . -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Debug --target cefclient
```

したがって、**プリビルドの `cefclient.exe` で手軽に試す道は無い**。CEF を試すには Visual Studio 2022 と CMake が必要である。

## 3. なぜ未実施か

- CEF の C++ サンプルをビルドするには MSVC と Windows SDK が必要で、現在の環境には無い。
- 標準配布物には `cefclient.exe` が含まれず、`tests/cefclient` のソースだけがある。
- Visual Studio 2022 の Build Tools は数 GB の導入が必要で、この検証の範囲を超える。
- PX3c で「軽量 + 高速」が否定されたため、CEF に移っても起動・メモリの不利は解消しない。CEF は配布物が大きく、更新責任も開発側に残る。

## 4. 判定

**PX7 は未実施であり、CEF の成立を証拠づけていない。** 「WebView2 が失格なら CEF」という代替案は、依然として未検証である。

さらに、CEF の `cefclient` は「サンプルブラウザ」であり、Chrome スタイルの拡張機能、uBO、Google の完全ログイン、Widevine の実再生をそのまま検証できるとは限らない。CEF を選んでも、これらは別途の実装と契約が必要である。

CEF を検討するなら、次の順で行う。

1. Visual Studio 2022 Build Tools（MSVC、Windows SDK）と CMake を導入する。
2. CEF の標準配布物を取得し、`cefsimple` / `cefclient` をビルドする。
3. Google OAuth の識別子画面、完全ログイン、EME（Widevine / PlayReady）、拡張機能（uBO）を確認する。
4. 起動、5 タブメモリ、配布物サイズを測る。
5. 更新追随（Chromium のリリースサイクル）と法務（Widevine、フィルタ）を確認する。

## 5. 設計への影響

- `13` の PX7 は「未実施（配布物確認済み、prebuilt cefclient なし、MSVC なし）」と明記する。
- 代替案の評価は「CEF にすれば解決する」ではなく「CEF でも未検証」に留める。
- CEF に移っても、起動・メモリ・拡張・DRM の課題は自動的には解決しない。
