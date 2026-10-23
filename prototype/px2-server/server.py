#!/usr/bin/env python3
"""PX2 local page server.

Serves one page with 50 "ad" images and 50 "content" images.
Each image response is delayed by 2 ms to make request overhead visible.
"""
from __future__ import annotations

import base64
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nXQAAAAASUVORK5CYII="
)

ad_imgs = "\n".join(
    f'<img src="/ad/{i}.png" width="1" height="1" alt="ad{i}">' for i in range(250)
)
content_imgs = "\n".join(
    f'<img src="/content/{i}.png" width="1" height="1" alt="content{i}">'
    for i in range(250)
)

HTML = f"""<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8">
<title>PX2 adblock cost page</title>
</head>
<body>
<h1>PX2</h1>
<p>50 ad images and 50 content images.</p>
{ad_imgs}
{content_imgs}
<script>window.__px2_ready = true;</script>
</body>
</html>
""".encode("utf-8")


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        path = urlparse(self.path).path
        if path in ("/", "/index.html"):
            self._send(200, "text/html; charset=utf-8", HTML)
            return
        if path.startswith("/ad/") or path.startswith("/content/"):
            time.sleep(0.002)
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
        # Keep the console quiet; the WebView2 app counts requests.
        return


def main() -> None:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"PX2 server on http://127.0.0.1:{port}/", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()

