# lIl Browser security review

Verification checklist: [CHECKLIST.md](CHECKLIST.md)

Scope: Windows WinForms + WebView2 browser source in `app/lilBrowser`.
Method: source-first review following the Cloudflare `security-audit` skill guidance
(trust boundaries, webview/native bridge, client-side navigation, desktop local-IPC,
supply chain, and release boundaries).

The skill's full audit mode requires an OS-enforced, network-off sandbox for target
execution and independent verification agents. This environment does not provide that
sandbox, so this review is a **scoped static hardening review**, not a claim of complete
coverage. No live exploit was executed and no severity is assigned to unverified
hypotheses.

## Trust boundaries reviewed

- Remote web content -> WebView2 renderer
- WebView2 renderer -> native navigation, popup, permission, and download handlers
- User-entered address -> navigation target
- Local settings/session/history JSON -> browser configuration and navigation
- GitHub update metadata -> update indicator / opened URL
- Installer and local signing -> executable release

## Findings and fixes

### SEC-001 — navigation scheme trust (fixed)

Source path: `Navigate`, `NavigateInput`, `NewWindowRequested`, `NavigationStarting`.

Before: a URL containing `://` was passed to WebView2 without an explicit scheme
allowlist. Popup URLs and navigation redirects were not checked in the native host,
and the address bar could accept `file://` and similar schemes.

Fix: added `UrlPolicy` and enforced it at all four entry points.

- Allow only `http`, `https`, `about:blank`, and origin-bound `blob` for navigation.
- Allow only `http`, `https`, and `about:blank` for popups.
- Reject unsafe typed URLs instead of passing them to the renderer.
- Cancel unsafe redirects in `NavigationStarting`.
- Keep external OS schemes (`mailto:`, `ms-*`, etc.) out of the webview host.

### SEC-002 — permission decision scope (fixed)

Source path: `PermissionKey`.

Before: saved permission decisions were keyed by permission kind + host only. A
decision on one scheme/port could be reused on another origin with the same host.

Fix: key decisions by permission kind + normalized origin
(`scheme://host:port`). Existing host-only decisions are intentionally not reused and
will be asked again.

### SEC-003 — configured home/search URL validation (fixed)

Source path: `Settings.Load`.

Before: a modified settings file could set a non-web homepage or search base.

Fix: validate both values with the same web URL policy and fall back to Google. The
theme value is also constrained to `dark`/`light`.

### SEC-004 — update URL trust (fixed)

Source path: `UpdateChecker`, `OpenUpdate`.

Before: the update button opened the GitHub API `html_url` directly.

Fix: the opened URL must be HTTPS, host `github.com`, and under
`/Hum1Tab/lil-browser/`. Otherwise the fixed repository URL is used. The update flow
still does **not** download or execute an update automatically.

### SEC-005 — WebView2 capability surface (hardened)

Source path: `Wire`.

Confirmed settings:

- DevTools disabled.
- Host objects disabled.
- Web messages disabled.
- Password autosave disabled.
- General autofill disabled.
- Swipe navigation disabled.
- Built-in renderer certificate validation remains enabled by WebView2.


### SEC-006 — popup amplification (fixed)

Source path: `NewWindowRequested`.

Before: the host checked the URL scheme but did not require a user gesture, and there was no tab/rate cap.

Fix:

- reject `!e.IsUserInitiated`;
- cap tabs at 24;
- allow at most 4 popups per 10 seconds.

### SEC-007 — fullscreen deception (fixed)

Source path: `EnterHtmlFullscreen`, `ToggleFullscreen`.

Added a persistent 32px safety bar while the page is fullscreen:

- displays the site host;
- states `Esc で終了`;
- is removed on exit and restored after F11 / HTML fullscreen.

### SEC-008 — update identity (fixed)

Source path: `UpdateChecker`, `lilBrowser.csproj`.

The release now publishes a signed `update.json` (RSA-2048/SHA-256) and `update.json.sig`. The app verifies the signature with an embedded public key, checks the manifest version and SHA-256, and only then shows the update button. The update is not downloaded or installed automatically.

### SEC-009 — HTTP indication (fixed)

Source path: `RoundedAddressHost`, `SyncAddress`.

HTTPS pages show a gold lock. HTTP pages show an amber open-lock indicator and an amber address-bar border. The tooltip states whether the connection is encrypted.

### SEC-010 — download open warning (fixed)

Source path: `ShowDownloads`, `ConfirmOpenDownload`.

Executable/script extensions (`.exe`, `.msi`, `.ps1`, `.bat`, `.js`, `.lnk`, etc.) now require confirmation before `Process.Start`. The dialog also reports whether the file has a Mark-of-the-Web alternate data stream.

### SEC-011 — sensitive permissions are session-only (fixed)

Source path: `OnPermissionRequested`.

Camera, microphone, geolocation, notifications, clipboard read/write, file read/write, local fonts, MIDI, and other sensor permissions are no longer persisted even if the user checks “remember”. They are asked again in each session. Previously saved sensitive decisions are ignored.

### SEC-012 — private profile cleanup (best effort)

Source path: `MainForm_FormClosing`.

Private windows use a unique `%TEMP%\\lIlBrowser-private-<guid>` profile. On close the app disposes the WebView2 controls, retries deletion, and asks Windows to delete a locked tree on reboot. This is still best-effort, not a forensic-erasure guarantee.
## Residual risks / needs validation

1. **Local executable signing**: the build uses a local self-signed certificate. Other
   PCs need their own trust or a public code-signing certificate. This is a release
   limitation, not a webview bug.
2. **Update authenticity**: update detection reads the public GitHub API and opens a
   commit page. There is no signed auto-updater and no automatic installation, so a
   compromised metadata response cannot execute code by itself. A future auto-updater
   would need signed metadata and payload verification.
3. **Local history/session data**: stored as plain JSON under the user profile. It is
   not encrypted; Windows account ACLs are the boundary. This is documented privacy
   behavior, not a cross-user vulnerability.
4. **Download target**: WebView2 chooses the default save path and asks the user. No
   custom privileged file-write path is used.
5. **Full dynamic verification**: an OS-enforced network-off sandbox and independent
   verifier runs are required to confirm exploitability of renderer-specific behavior.
   This report does not claim that no such issue exists.

## Regression checks

- Build: `dotnet build app/lilBrowser/lilBrowser.csproj -c Release`
- Existing smoke suite: `--smoke` and `--private-smoke` against the local test server.
- Manual checks: address bar `file://`, `javascript:`, and popup schemes are rejected;
  normal HTTPS navigation, tabs, permissions, and downloads still work.
