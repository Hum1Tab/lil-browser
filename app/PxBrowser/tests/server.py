#!/usr/bin/env python3
"""Local test server for PX Browser smoke tests."""
from __future__ import annotations

import base64
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nXQAAAAASUVORK5CYII="
)

HOME = b"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX Test Page</title></head>
<body>
<h1>PX Test Page</h1>
<p>content page</p>
<img src="/img/content.png" width="1" height="1" alt="content">
<img src="/ad/banner.png" width="1" height="1" alt="ad">
<script>
document.cookie = 'pxtest=1; max-age=3600; path=/';
localStorage.setItem('pxtest', '1');
document.title = 'PX Test Page';
</script>
</body></html>
"""

READ = b"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX Read Page</title></head>
<body><h1>PX Read Page</h1>
<pre id="out">reading...</pre>
<script>
const cookie = document.cookie;
const local = localStorage.getItem('pxtest') || '';
document.getElementById('out').textContent = 'cookie=' + cookie + '\\nlocal=' + local;
</script>
</body></html>
"""

FULLSCREEN = b"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX Fullscreen Test</title></head>
<body style="background:#171817;color:#f7f1e7;font-family:sans-serif">
<h1>Fullscreen test</h1>
<p id="state">off</p>
<button id="btn">Enter fullscreen</button>
<script>
document.getElementById('btn').addEventListener('click', async () => {
  try {
    await document.documentElement.requestFullscreen();
  } catch (e) {
    document.getElementById('state').textContent = 'error: ' + e.message;
  }
});
document.addEventListener('fullscreenchange', () => {
  document.getElementById('state').textContent = document.fullscreenElement ? 'on' : 'off';
});
</script>
</body></html>
"""


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        path = urlparse(self.path).path
        if path in ("/", "/index.html"):
            self._send(200, "text/html; charset=utf-8", HOME)
            return
        if path == "/read":
            self._send(200, "text/html; charset=utf-8", READ)
            return
        if path == "/fullscreen":
            self._send(200, "text/html; charset=utf-8", FULLSCREEN)
            return
        if path in ("/img/content.png", "/ad/banner.png"):
            self._send(200, "image/png", PNG)
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
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8769
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"PX Browser test server on http://127.0.0.1:{port}/", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
