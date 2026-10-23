#!/usr/bin/env python3
"""PX7 local probe page for CEF: UA, chrome object, EME support."""
from __future__ import annotations

import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

HTML = b"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX7 CEF probe</title></head>
<body><h1>PX7 CEF probe</h1><pre id="out">running...</pre>
<script>
window.__px7 = {
  ua: navigator.userAgent,
  chrome: typeof window.chrome,
  webview: !!(window.chrome && window.chrome.webview),
  secure: window.isSecureContext,
  eme: typeof navigator.requestMediaKeySystemAccess
};
function show() {
  document.getElementById('out').textContent = JSON.stringify(window.__px7, null, 2);
}
async function tryKey(k, robust) {
  const cfg = [{
    initDataTypes: ['cenc'],
    videoCapabilities: [{ contentType: 'video/mp4; codecs="avc1.42E01E"', robustness: robust }]
  }];
  try {
    const a = await navigator.requestMediaKeySystemAccess(k, cfg);
    return 'OK:' + a.keySystem;
  } catch (e) {
    return 'ERR:' + e.name + ':' + e.message;
  }
}
(async () => {
  if (!navigator.requestMediaKeySystemAccess) { window.__px7.done = true; show(); return; }
  for (const k of ['com.widevine.alpha', 'com.microsoft.playready', 'org.w3.clearkey']) {
    window.__px7[k] = await tryKey(k, 'SW_SECURE_CRYPTO');
    if (window.__px7[k].startsWith('ERR:')) {
      window.__px7[k + '_nohw'] = await tryKey(k, '');
    }
  }
  window.__px7.done = true;
  show();
})();
setInterval(show, 500);
</script>
</body></html>
"""


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        path = urlparse(self.path).path
        if path in ("/", "/index.html"):
            self._send(200, "text/html; charset=utf-8", HTML)
            return
        self._send(404, "text/plain; charset=utf-8", b"not found")

    def _send(self, status: int, ctype: str, body: bytes) -> None:
        self.send_response(status)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        try:
            self.wfile.write(body)
        except BrokenPipeError:
            pass

    def log_message(self, fmt: str, *args) -> None:
        return


def main() -> None:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8768
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"PX7 server on http://127.0.0.1:{port}/", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
