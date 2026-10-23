#!/usr/bin/env python3
"""PX5 local pages for storage / InPrivate residue tests."""
from __future__ import annotations

import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

SET_HTML = b"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX5 set</title></head>
<body><h1>PX5 set</h1><p id="out">setting...</p>
<script>
document.cookie = 'px5cookie=1; max-age=3600; path=/';
localStorage.setItem('px5local', '1');
sessionStorage.setItem('px5session', '1');
try {
  const req = indexedDB.open('px5db', 1);
  req.onupgradeneeded = (e) => {
    const db = e.target.result;
    if (!db.objectStoreNames.contains('s')) db.createObjectStore('s');
  };
  req.onsuccess = (e) => {
    const db = e.target.result;
    const tx = db.transaction('s', 'readwrite');
    tx.objectStore('s').put('px5idb', 'k');
  };
} catch (e) { console.log('idb ' + e); }
try {
  caches.open('px5cache').then((c) => c.put('/cache-me', new Response('px5cache')));
} catch (e) { console.log('cache ' + e); }
document.getElementById('out').textContent = 'set';
</script>
</body></html>
"""

READ_HTML = b"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><title>PX5 read</title></head>
<body><h1>PX5 read</h1><pre id="out">reading...</pre>
<script>
const lines = [];
lines.push('cookie=' + document.cookie);
lines.push('local=' + (localStorage.getItem('px5local') || ''));
lines.push('session=' + (sessionStorage.getItem('px5session') || ''));
const render = () => { document.getElementById('out').textContent = lines.join('\\n'); };
render();
try {
  const req = indexedDB.open('px5db', 1);
  req.onsuccess = (e) => {
    const db = e.target.result;
    if (!db.objectStoreNames.contains('s')) { lines.push('idb='); render(); return; }
    const tx = db.transaction('s', 'readonly');
    const get = tx.objectStore('s').get('k');
    get.onsuccess = () => { lines.push('idb=' + (get.result || '')); render(); };
    get.onerror = () => { lines.push('idb=err'); render(); };
  };
  req.onerror = () => { lines.push('idb=open-err'); render(); };
} catch (e) { lines.push('idb=exc'); render(); }
setTimeout(render, 500);
</script>
</body></html>
"""


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        path = urlparse(self.path).path
        if path == "/set":
            self._send(200, "text/html; charset=utf-8", SET_HTML)
            return
        if path == "/read":
            self._send(200, "text/html; charset=utf-8", READ_HTML)
            return
        if path == "/cache-me":
            self._send(200, "text/plain; charset=utf-8", b"px5cache")
            return
        self._send(200, "text/html; charset=utf-8",
                   b"<!doctype html><title>PX5</title><p>PX5 server</p>")

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
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8767
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"PX5 server on http://127.0.0.1:{port}/", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
