#!/usr/bin/env python3
"""PX3 local page server with load beacons.

Serves a light page. The page sends a beacon on `load` to /mark with
run/tab/performance.now(), so the measurement script can compute a fair
"process start -> first tab load" time for both our app and Edge.
"""
from __future__ import annotations

import base64
import json
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nXQAAAAASUVORK5CYII="
)

imgs = "\n".join(
    f'<img src="/img/{i}.png" width="1" height="1" alt="i{i}">' for i in range(10)
)
HTML = f"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX3 memory page</title></head>
<body><h1>PX3</h1><p>Light page for multi-tab memory measurement.</p>{imgs}
<script>
window.addEventListener('load', () => {{
  const p = new URLSearchParams(location.search);
  const run = p.get('run') || '';
  const tab = p.get('tab') || '';
  fetch(`/mark?run=${{encodeURIComponent(run)}}&tab=${{encodeURIComponent(tab)}}&t=${{performance.now()}}`,
        {{cache: 'no-store'}}).catch(() => {{}});
}});
</script>
</body></html>
""".encode("utf-8")

MARKS = []
LOCK = threading.Lock()


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        path = parsed.path
        if path in ("/", "/index.html"):
            self._send(200, "text/html; charset=utf-8", HTML)
            return
        if path.startswith("/img/"):
            self._send(200, "image/png", PNG)
            return
        if path == "/mark":
            q = parse_qs(parsed.query)
            rec = {
                "time": time.time(),
                "run": q.get("run", [""])[0],
                "tab": q.get("tab", [""])[0],
                "perf": q.get("t", [""])[0],
                "ua": self.headers.get("User-Agent", ""),
            }
            with LOCK:
                MARKS.append(rec)
                with open("marks.jsonl", "a", encoding="utf-8") as fh:
                    fh.write(json.dumps(rec, ensure_ascii=False) + "\n")
            self._send(204, "text/plain", b"")
            return
        if path == "/marks":
            q = parse_qs(parsed.query)
            run = q.get("run", [""])[0]
            with LOCK:
                data = [m for m in MARKS if not run or m["run"] == run]
            body = json.dumps(data, ensure_ascii=False).encode("utf-8")
            self._send(200, "application/json; charset=utf-8", body)
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
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8766
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"PX3 server on http://127.0.0.1:{port}/", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
