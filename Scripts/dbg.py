#!/usr/bin/env python3
"""Debug console client for NoPasaranFC.

Usage: python3 Scripts/dbg.py "state" "shot /tmp/menu.png 5" "key Enter" ...
Each argument is one command line sent to the game; responses are printed.
Port from NOPASARAN_DEBUG_PORT env or default 7777.
"""
import os
import socket
import sys


def main() -> int:
    port = int(os.environ.get("NOPASARAN_DEBUG_PORT", "7777"))
    commands = sys.argv[1:]
    if not commands:
        print(__doc__)
        return 1

    with socket.create_connection(("127.0.0.1", port), timeout=30) as sock:
        reader = sock.makefile("r", encoding="utf-8")
        writer = sock.makefile("w", encoding="utf-8")
        greeting = reader.readline().strip()
        print(f"< {greeting}")
        for cmd in commands:
            writer.write(cmd + "\n")
            writer.flush()
            response = reader.readline()
            if not response:
                print(f"> {cmd}\n< (connection closed)")
                return 2
            print(f"> {cmd}\n< {response.strip()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
