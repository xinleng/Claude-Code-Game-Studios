"""Serve the repo root over localhost so production/dashboard.html can load
project documents in its in-page viewer. CORS is open so the dashboard also
works when rendered inside a sandboxed preview panel (null origin).

Run:  python tools/serve-dashboard.py
Open: http://localhost:8321/production/dashboard.html
"""
import functools
import http.server
import os

PORT = 8321
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class CorsRequestHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, fmt, *args):  # quiet
        pass


if __name__ == "__main__":
    handler = functools.partial(CorsRequestHandler, directory=REPO_ROOT)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", PORT), handler)
    print(f"Serving {REPO_ROOT} at http://localhost:{PORT}/production/dashboard.html")
    server.serve_forever()
