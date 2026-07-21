#!/usr/bin/env python3
"""Send a python file to the running Blender via the blender-mcp addon socket.

Usage: python3 Scripts/blender_exec.py <script.py>
The script runs inside Blender (bpy available). Output is printed to stdout.
"""
import json
import socket
import sys

HOST, PORT = "127.0.0.1", 9876


def recv_all(sock):
    chunks = []
    while True:
        data = sock.recv(65536)
        if not data:
            break
        chunks.append(data)
        try:
            return b"".join(chunks)  # complete JSON received
        except Exception:
            continue
    return b"".join(chunks)


def main():
    with open(sys.argv[1], "r", encoding="utf-8") as f:
        code = f.read()

    command = {"type": "execute_code", "params": {"code": code}}
    with socket.create_connection((HOST, PORT), timeout=30) as sock:
        sock.sendall(json.dumps(command).encode("utf-8"))
        sock.settimeout(60)
        response = recv_all(sock)

    try:
        result = json.loads(response.decode("utf-8"))
    except json.JSONDecodeError:
        print("RAW RESPONSE:", response[:2000])
        return 2

    status = result.get("status")
    print("STATUS:", status)
    payload = result.get("result") or result.get("message") or result
    if isinstance(payload, dict) and "executed" in payload:
        print("executed:", payload["executed"])
    else:
        print(json.dumps(payload, indent=2)[:3000])
    return 0 if status == "success" else 1


if __name__ == "__main__":
    sys.exit(main())
