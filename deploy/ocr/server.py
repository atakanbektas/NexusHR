"""Private, bounded native document boundary. Never log requests, bodies or parser stderr."""
import json
import os
import signal
import sys
import time
import select
import socket
import subprocess
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

gate = threading.BoundedSemaphore(1)
MAX_BYTES = 5 * 1024 * 1024


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def reply(self, status, value):
        body = json.dumps(value, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_GET(self):
        self.reply(200 if self.path == "/health" else 404, {"ready": self.path == "/health"})

    def do_POST(self):
        if self.path not in ("/extract", "/validate"):
            return self.reply(404, {"code": "OCR.NotFound"})
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            return self.reply(400, {"code": "OCR.InvalidFile"})
        if not 0 < length <= MAX_BYTES:
            return self.reply(413, {"code": "OCR.FileTooLarge"})
        if not gate.acquire(blocking=False):
            return self.reply(429, {"code": "OCR.Busy"})
        try:
            self.connection.settimeout(15)
            data = self.rfile.read(length)
            if len(data) != length:
                return self.reply(400, {"code": "OCR.InvalidFile"})
            # All sensitive temporary data lives in the container's bounded tmpfs.
            with tempfile.TemporaryDirectory(prefix="cv-") as directory:
                path = os.path.join(directory, "input")
                with open(path, "wb") as file:
                    file.write(data)
                process = subprocess.Popen([sys.executable, "/app/worker.py", path, directory, self.path],
                    stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, start_new_session=True)
                try:
                    until = time.monotonic() + 85
                    while True:
                        try:
                            output, _ = process.communicate(timeout=.2)
                            break
                        except subprocess.TimeoutExpired:
                            readable, _, _ = select.select([self.connection], [], [], 0)
                            try:
                                disconnected = readable and self.connection.recv(1, socket.MSG_PEEK) == b""
                            except (ConnectionResetError, BrokenPipeError):
                                disconnected = True
                            if disconnected:
                                return
                            if time.monotonic() >= until:
                                return self.reply(422, {"code": "OCR.Timeout"})
                finally:
                    # FIN, RST, timeout and unexpected errors all reap native descendants.
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.communicate()
                if process.returncode != 0 or len(output) > 12 * 1024 * 1024:
                    return self.reply(422, {"code": "OCR.InvalidOrUnsupportedDocument"})
                return self.reply(200, json.loads(output))
        except (OSError, ValueError, json.JSONDecodeError):
            self.reply(422, {"code": "OCR.InvalidOrUnsupportedDocument"})
        finally:
            gate.release()


if __name__ == "__main__":
    ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
