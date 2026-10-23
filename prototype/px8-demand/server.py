#!/usr/bin/env python3
"""Local demand-test server for the PX8 landing page.

Serves landing.html, records anonymous page views by variant, and accepts
POST /api/waitlist. Submissions are stored in submissions.jsonl, view/signup
counts in metrics.json. No IP addresses are recorded and no external network
calls are made.
"""
from __future__ import annotations

import json
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

ROOT = Path(__file__).resolve().parent
LANDING = ROOT / "landing.html"
STORE = ROOT / "submissions.jsonl"
METRICS = ROOT / "metrics.json"
LOCK = threading.Lock()


def load_metrics() -> dict:
    if METRICS.exists():
        try:
            data = json.loads(METRICS.read_text(encoding="utf-8"))
            return {
                "views": {"a": int(data.get("views", {}).get("a", 0)), "b": int(data.get("views", {}).get("b", 0))},
                "signups": {"a": int(data.get("signups", {}).get("a", 0)), "b": int(data.get("signups", {}).get("b", 0))},
            }
        except Exception:
            pass
    return {"views": {"a": 0, "b": 0}, "signups": {"a": 0, "b": 0}}


def save_metrics(data: dict) -> None:
    METRICS.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def variant_of(value: str | None) -> str:
    return "b" if str(value or "").lower() == "b" else "a"


def valid_email(email: str) -> bool:
    email = email.strip()
    return 3 <= len(email) <= 254 and "@" in email and "." in email.split("@", 1)[1]


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        path = parsed.path
        if path in ("/", "/landing.html"):
            q = parse_qs(parsed.query)
            variant = variant_of(q.get("v", ["a"])[0])
            with LOCK:
                data = load_metrics()
                data["views"][variant] += 1
                save_metrics(data)
            body = LANDING.read_bytes()
            self._send(200, "text/html; charset=utf-8", body)
            return
        if path == "/api/health":
            self._send(200, "application/json; charset=utf-8", b'{"ok":true}')
            return
        if path == "/api/metrics":
            with LOCK:
                data = load_metrics()
            body = json.dumps(data, ensure_ascii=False).encode("utf-8")
            self._send(200, "application/json; charset=utf-8", body)
            return
        self._send(404, "text/plain; charset=utf-8", b"not found")

    def do_POST(self) -> None:  # noqa: N802
        if urlparse(self.path).path != "/api/waitlist":
            self._send(404, "text/plain; charset=utf-8", b"not found")
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length <= 0 or length > 16384:
                raise ValueError("bad length")
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            email = str(payload.get("email", "")).strip()
            if not valid_email(email):
                raise ValueError("invalid email")
            if payload.get("consent") is not True:
                raise ValueError("consent required")
            variant = variant_of(payload.get("variant"))
            record = {
                "at": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
                "email": email,
                "tabs": str(payload.get("tabs", ""))[:40],
                "pain": str(payload.get("pain", ""))[:1000],
                "variant": variant,
            }
            with LOCK:
                with STORE.open("a", encoding="utf-8") as fh:
                    fh.write(json.dumps(record, ensure_ascii=False) + "\n")
                data = load_metrics()
                data["signups"][variant] += 1
                save_metrics(data)
            self._send(200, "application/json; charset=utf-8", b'{"ok":true}')
        except Exception as exc:  # noqa: BLE001
            body = json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False).encode("utf-8")
            self._send(400, "application/json; charset=utf-8", body)

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
        # Do not log IP addresses, emails, or paths with query strings.
        return


def main() -> None:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"PX8 demand server on http://127.0.0.1:{port}/?live=1", flush=True)
    print(f"submissions: {STORE}", flush=True)
    print(f"metrics: {METRICS}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
