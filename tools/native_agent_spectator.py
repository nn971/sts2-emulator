#!/usr/bin/env python3
"""Read the native game's authenticated, human-controlled Inspector agent feed.

All calls are GET. The companion game-side spectator transport rejects every
control request and publishes only a whitelisted partial public view.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

SCHEMA = "sts2-native-agent-spectator-v1"


def base_url(host: str, port: int) -> str:
    if host not in ("127.0.0.1", "localhost", "::1"):
        raise ValueError("The native agent spectator is loopback-only.")
    if not 1024 <= port <= 65535:
        raise ValueError("Port must be in [1024, 65535].")
    return f"http://[{host}]:{port}" if host == "::1" else f"http://{host}:{port}"


def fetch(base: str, token: str, endpoint: str) -> dict:
    if not token or len(token) < 24:
        raise ValueError("STS2_INSPECTOR_AGENT_TOKEN must have 24+ characters.")
    if endpoint not in ("/v1/agent/status", "/v1/agent/boundary"):
        raise ValueError("Read-only agent endpoints only.")
    request = Request(base + endpoint, headers={
        "Authorization": "Bearer " + token,
        "Accept": "application/json"
    }, method="GET")
    with urlopen(request, timeout=3) as response:
        data = json.loads(response.read(1024 * 1024))
    state = data if endpoint.endswith("/status") else data.get("status")
    if not isinstance(state, dict) or state.get("schema_id") != SCHEMA:
        raise ValueError("Unexpected native spectator protocol schema.")
    if state.get("control_mode") != "HUMAN_ONLY" or (
            state.get("action_submission_supported") is not False):
        raise ValueError("Native game is not in verified spectator-only mode.")
    if endpoint.endswith("/boundary"):
        boundary = data.get("boundary")
        if boundary is not None and (
                not isinstance(boundary, dict)
                or boundary.get("schema_id") != SCHEMA
                or boundary.get("native_game_parity") != "NOT_TESTED"
                or boundary.get("control_mode") != "HUMAN_ONLY"):
            raise ValueError("Unexpected live native boundary contract.")
    return data


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=39221)
    parser.add_argument("--status", action="store_true",
                        help="Request capability and readiness status only")
    parser.add_argument("--follow", action="store_true",
                        help="Report new stable decisions and loss of readiness")
    parser.add_argument("--interval", type=float, default=0.5)
    args = parser.parse_args()
    try:
        base = base_url(args.host, args.port)
        token = os.environ.get("STS2_INSPECTOR_AGENT_TOKEN", "")
        if args.interval < 0.2:
            raise ValueError("Polling interval must be at least 0.2 seconds.")
        endpoint = "/v1/agent/status" if args.status else "/v1/agent/boundary"
        previous = None
        while True:
            data = fetch(base, token, endpoint)
            key = json.dumps(data, sort_keys=True, separators=(",", ":"))
            if key != previous:
                print(json.dumps(data, indent=2, ensure_ascii=False),
                      flush=True)
                previous = key
            if not args.follow:
                return 0
            time.sleep(args.interval)
    except (ValueError, HTTPError, URLError, OSError) as error:
        print(f"NATIVE_AGENT_SPECTATOR_ERROR: {error}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
