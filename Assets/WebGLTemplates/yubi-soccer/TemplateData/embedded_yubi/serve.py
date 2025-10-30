#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
import argparse
import os


class CoopCoepHandler(SimpleHTTPRequestHandler):
    # 正しい MIME を付与（Python の既定は .mjs/.wasm が不十分な場合がある）
    extensions_map = {
        **SimpleHTTPRequestHandler.extensions_map,
        ".mjs": "text/javascript",
        ".js": "text/javascript",
        ".wasm": "application/wasm",
        ".json": "application/json",
        ".task": "application/octet-stream",
    }
    def end_headers(self):
        # Cross-origin isolation
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        # CORS (ゆるめ)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Headers', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, OPTIONS')
        # その他、キャッシュは短め（開発用）
        self.send_header('Cache-Control', 'no-cache, no-store, must-revalidate')
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(200, 'ok')
        self.end_headers()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=3002)
    parser.add_argument('--bind', type=str, default='127.0.0.1')
    parser.add_argument('--dir', type=str, default='.')
    args = parser.parse_args()

    os.chdir(args.dir)
    srv = ThreadingHTTPServer((args.bind, args.port), CoopCoepHandler)
    print(f"Serving at http://{args.bind}:{args.port} (dir={os.getcwd()}) with COOP/COEP")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        srv.server_close()


if __name__ == '__main__':
    main()
