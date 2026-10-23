# lIl Browser

<img src="design/icons/final/lil-service-icon-1024.png" width="96" alt="lIl Browser icon">

A lightweight Windows browser built with C# / WinForms / WebView2. Gold-on-black brand system.

- Custom tab strip, address bar, dark/light themes
- Bookmarks, history, downloads, private mode, ad blocking, find, zoom, print
- GitHub update check: when the repository gets a newer commit, an update button appears at the top right
- Single-file self-contained Windows x64 build (not stored in this repository)

## Build

Requirements:

- .NET 8 SDK
- Microsoft Edge WebView2 Runtime

```powershell
dotnet publish app/lilBrowser/lilBrowser.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -o dist/lIlBrowser
```

The executable is `dist/lIlBrowser/lIlBrowser.exe`.

## Run

```powershell
dist/lIlBrowser/lIlBrowser.exe
```

Data is stored in `%LOCALAPPDATA%\lIl Browser\`. Settings from the previous `PxBrowser` folder are copied on first launch (settings/history/bookmarks/session only).

## Install

```powershell
powershell -ExecutionPolicy Bypass -File .\app\lilBrowser\install.ps1
```

The installer copies the app to `%LOCALAPPDATA%\Programs\lIlBrowser`, creates a Start Menu shortcut, and registers an HKCU uninstall entry. It does not register the app as the default browser.

## Update check

`app/lilBrowser/UpdateChecker.cs` fetches `update.json` and `update.json.sig` from `Hum1Tab/lil-browser`. It verifies the RSA-2048/SHA-256 signature with an embedded public key, checks the manifest version and SHA-256, and then shows the update button. The button opens the signed release page; it does not install automatically.

## Signing

There is no public code-signing certificate. The local build uses a self-signed test certificate via `app/lilBrowser/sign-local.ps1`. Other PCs need their own trusted certificate, or the executable will be blocked by application-control policy.

## Security notes

- [Security review](docs/security/SECURITY-AUDIT.md)
- [9-item verification checklist](docs/security/CHECKLIST.md)
- [Changelog](CHANGELOG.md)

## Limitations

- No Chrome extensions
- Google account login is not fully supported
- Netflix DRM playback is unverified
- Startup is slower than Edge
- No automatic installer update
- History is not encrypted

## License

MIT
