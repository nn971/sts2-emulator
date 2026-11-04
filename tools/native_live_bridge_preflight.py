#!/usr/bin/env python3
"""Read-only local bridge credential preflight before starting a native run.

Runs only after the Reference Mod process has started. No gameplay is
launched, no input is sent, no event stream is left open, and secrets are
never written to the diagnostic report. The invalid SSE cursor is intentional:
an authenticated source channel responds 400 immediately, while the wrong
credential responds 401. This proves endpoint access without waiting for
the first native combat or consuming privileged observations.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

from native_live_fidelity_coordinator import _LOCAL_OPENER, _loopback_base

SCHEMA = "sts2-native-live-bridge-preflight-v1"


def _get(base: str, endpoint: str, token: str, timeout: float) -> tuple[int, dict]:
    request = urllib.request.Request(
        base + endpoint, method="GET",
        headers={"Authorization": "Bearer " + token})
    try:
        with _LOCAL_OPENER.open(request, timeout=timeout) as response:
            code = response.status
            content = response.read(4096)
    except urllib.error.HTTPError as error:
        code = error.code
        content = error.read(4096)
    try:
        parsed = json.loads(content)
        return code, parsed if isinstance(parsed, dict) else {}
    except (UnicodeDecodeError, ValueError):
        return code, {}


def inspect_bridge(base: str, public_token: str | None,
                   fidelity_token: str | None, *, timeout: float = 3.0) -> dict:
    report = {
        "schema": SCHEMA,
        "status": "BLOCKED_BRIDGE_PREFLIGHT",
        "native_game_launched_by_probe": False,
        "native_gameplay_mutated": False,
        "native_live_lockstep": False,
        "native_settlement_certified": False,
        "probe": "READ_ONLY_LOOPBACK_AUTHENTICATION",
    }
    try:
        base = _loopback_base(base)
    except ValueError as error:
        report["reason"] = "Invalid loopback origin: " + str(error)
        return report
    if (not isinstance(public_token, str) or len(public_token) < 24
        or not isinstance(fidelity_token, str) or len(fidelity_token) < 24
        or public_token == fidelity_token):
        report["reason"] = "Separate public and fidelity tokens of at least 24 characters required"
        return report
    if timeout <= 0 or timeout > 30:
        report["reason"] = "Timeout must be between 0 and 30 seconds"
        return report
    probes = (
        ("public_endpoint", "/v1/agent/status", public_token, 200),
        ("fidelity_endpoint", "/v1/fidelity/stream?after_seq=-1",
         fidelity_token, 400),
        ("public_token_fidelity_denied", "/v1/fidelity/stream?after_seq=-1",
         public_token, 401),
        ("fidelity_token_public_denied", "/v1/agent/status",
         fidelity_token, 401),
    )
    for name, endpoint, token, expected in probes:
        try:
            status, body = _get(base, endpoint, token, timeout)
        except (OSError, urllib.error.URLError, TimeoutError) as error:
            report["reason"] = "Local bridge unreachable at " + name + ": " + type(error).__name__
            report["failed_probe"] = name
            return report
        if status != expected or (
            name == "fidelity_endpoint"
            and body.get("error") != "INVALID_EVENT_CURSOR"):
            report["reason"] = "Bridge authentication/endpoint contract mismatch"
            report["failed_probe"] = name
            report["observed_http_status"] = status
            report["expected_http_status"] = expected
            return report
    report["status"] = "LIVE_BRIDGE_AUTHENTICATION_READY"
    report["reason"] = "Public and fidelity loopback endpoints authenticated separately; no native run started"
    report["verified_requests"] = len(probes)
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bridge", default="http://127.0.0.1:39221")
    parser.add_argument("--timeout", type=float, default=3.0)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    result = inspect_bridge(
        args.bridge,
        os.environ.get("STS2_INSPECTOR_AGENT_TOKEN"),
        os.environ.get("STS2_INSPECTOR_FIDELITY_TOKEN"),
        timeout=args.timeout)
    if args.out is not None:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n",
                            encoding="utf-8")
    print(json.dumps(result, sort_keys=True))
    return 0 if result["status"] == "LIVE_BRIDGE_AUTHENTICATION_READY" else 2


if __name__ == "__main__":
    sys.exit(main())
