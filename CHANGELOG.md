# Changelog

## 0.4.1

- Cancel page-initiated external URI scheme launches (`mailto:`, custom protocols).
- Page `window.close()` now closes only its tab, not the whole browser.
- Fullscreen safety bar displays an ASCII/punycode host for IDN clarity.
- Bounded update-manifest response size.
- Additional known-attack-path fixes from the security review.

## 0.4.0

### Security release

- Signed update manifest (RSA-2048/SHA-256) with an embedded public key.
- Update checks now verify the manifest signature before showing the update button.
- Private windows use a unique temporary WebView2 profile; cleanup is retried and scheduled for reboot if locked.
- Renderer process failure is surfaced in the status bar.
- Extended downloadable executable/script warnings.

## 0.3.0

### Brand

- Renamed the product and project paths from PX Browser to lIl Browser.
- Final gold-and-black Orbit service icon; app icon and theme aligned.
- New tab opens with the address bar focused and the URL selected.

### Security

- URL policy enforced for typed URLs, redirects, frame navigation, and popups.
- Popups require a user gesture; tab and popup-rate caps.
- HTML fullscreen shows the site host and `Esc で終了`.
- Build embeds the Git SHA; update checks compare SHA rather than only timestamps.
- HTTPS lock / HTTP warning indicator.
- Risky downloaded files require confirmation; MOTW presence is reported.
- Sensitive permissions are session-only and origin-scoped.
- Private profile deletion is attempted on close (best effort).
- Process failure status for renderer crashes/unresponsiveness.

### Known limitations

- Self-signed local build; no public code-signing identity.
- No signed automatic updater or automatic install.
- Private mode is not a forensic-erasure guarantee.
- Ad blocking is a small host/path list, not uBlock Origin.
- No Chrome extensions, full Google login, or verified Netflix DRM.
