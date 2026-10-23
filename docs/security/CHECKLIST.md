# lIl Browser security verification checklist

Status is deliberately split into **implemented**, **partially implemented**, and
**needs dynamic validation**. A code change is not the same as proof that a hostile page
cannot exploit a renderer or OS behavior.

Reference baseline:

- [Microsoft — Develop secure WebView2 apps](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
- [Chromium — Secure Architecture](https://www.chromium.org/Home/chromium-security/guts/)
- [Chromium — Site Isolation](https://www.chromium.org/developers/design-documents/site-isolation/)
- [OWASP ASVS](https://asvs.owasp.org/)
- [MITRE CWE Top 25](https://cwe.mitre.org/top25/archive/2025/2025_cwe_top25)
- [OWASP MASWE-0033 — native functionality exposure](https://mas.owasp.org/MASWE/MASVS-PLATFORM/MASWE-0033/)

## Matrix

| # | Check | Status | Evidence / remaining work |
|---|-------|--------|---------------------------|
| 1 | URL, redirect, dangerous schemes | **Implemented** | `UrlPolicy` is enforced at address input, top-level navigation, frame navigation, and popups. `file:`, `javascript:`, `data:`, and OS schemes are rejected. Manual `file:///` test was blocked and the address bar was restored. |
| 2 | WebView2 <-> OS permission boundary | **Implemented / partial** | Host objects and web messages are disabled; DevTools, password autosave, autofill, and swipe navigation are disabled. WebView2 remains a Microsoft-engine boundary. No native bridge exists. Dynamic renderer exploit testing still requires a sandbox. |
| 3 | Cookie / session / profile isolation | **Partial** | Normal and private profiles use separate WebView2 user-data folders. Private history is not recorded. Private profile deletion is attempted on close, but locked files may remain and this is not forensic erasure. Cross-site cookie behavior is delegated to WebView2/Site Isolation. |
| 4 | Popups and fullscreen | **Implemented** | Popups require `IsUserInitiated`, a safe scheme, max 24 tabs, and max 4 popups per 10 s. HTML fullscreen shows a host + `Esc で終了` safety bar. `fullscreenNotice=true` verified. |
| 5 | Download / local file | **Implemented / partial** | Risky extensions require confirmation; MOTW alternate data stream presence is reported. Files are not auto-executed. A full Windows Attachment Manager/SmartScreen test remains environment-dependent. |
| 6 | TLS / certificate / HTTP | **Implemented / partial** | WebView2 certificate validation is not bypassed. HTTPS shows a gold lock; HTTP shows an amber warning glyph and border. There is no custom certificate-error override. HSTS/preload behavior comes from the engine. |
| 7 | Update mechanism / dependencies | **Partial** | Build embeds the Git SHA in `AssemblyInformationalVersion`; update check compares it with the latest GitHub commit. Update execution is not automatic and metadata is not signed. A public-key signed update channel remains future work. WebView2 NuGet version is pinned. |
| 8 | Permissions / clipboard | **Implemented / partial** | Permission keys include scheme/host/port. Camera, microphone, geolocation, notifications, clipboard, file read/write, local fonts, MIDI, and sensors are session-only even if “remember” is checked. No per-permission expiry UI yet. |
| 9 | Resource exhaustion / crash resilience | **Partial** | Global tab cap 24, popup rate limit 4/10 s, download list cap 100, history cap 5000. No per-tab memory quota, renderer crash recovery test, or fuzz budget. WebView2 runs renderers in its own process model. |

## What was actually executed

- `dotnet build app/lilBrowser/lilBrowser.csproj -c Release` — success.
- Normal smoke (`--smoke`) — tabs, history, bookmarks, maximize/restore, search, zoom, fullscreen.
- HTML fullscreen smoke — `containsFullScreenElement=true`, `fullscreenNotice=true`, `chromeVisible=false`.
- Manual `file:///C:/Windows/win.ini` address-bar test — blocked; address bar restored.
- Signature check — `Valid` locally.

## Not claimed

- No full Cloudflare six-phase audit: an OS-enforced network-off sandbox and independent
  verifier agents were not available.
- No proof that WebView2/Chromium has no renderer vulnerability.
- No forensic-erasure guarantee for private mode.
- No signed auto-update or public code-signing identity.
- Ad blocking is a small host/path list, not uBlock Origin.
