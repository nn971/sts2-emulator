#!/usr/bin/env python3
"""Read the current native Reference Mod fidelity feedback, without gameplay input.

The native server accepts only the private bearer token on this endpoint.
Only small coordinator receipt metadata are returned; raw native events,
snapshots, player decisions and bearer tokens are never transmitted to
stdout or to a report file.
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


def read_status(base: str, token: str | None, *, timeout: float = 4.0) -> dict:
    result = {"schema": "sts2-native-fidelity-status-probe-v1",
              "status": "BLOCKED_FIDELITY_STATUS_PROBE",
              "native_gameplay_mutated": False}
    try:
        base = _loopback_base(base)
    except ValueError:
        result["reason"] = "Bridge must be a loopback HTTP origin with a port"
        return result
    if not isinstance(token, str) or len(token) < 24:
        result["reason"] = "Private fidelity credential unavailable (24+ characters)"
        return result
    request = urllib.request.Request(
        base + "/v1/fidelity/status", method="GET",
        headers={"Authorization": "Bearer " + token})
    try:
        with _LOCAL_OPENER.open(request, timeout=timeout) as response:
            if response.status != 200:
                result["reason"] = "Native feedback endpoint returned non-200"
                result["http_status"] = response.status
                return result
            body = json.loads(response.read(16384))
    except urllib.error.HTTPError as error:
        result["reason"] = "Native feedback endpoint returned HTTP error"
        result["http_status"] = error.code
        return result
    except (TimeoutError, OSError, ValueError, TypeError):
        result["reason"] = "Native feedback endpoint unavailable or malformed"
        return result
    if not isinstance(body, dict) or body.get("schema") != (
            "sts2-native-fidelity-status-v1"):
        result["reason"] = "Unexpected private status endpoint response schema"
        return result
    receipt = body.get("latest_result")
    if receipt is not None and not isinstance(receipt, dict):
        result["reason"] = "Native feedback latest_result is not a JSON object"
        return result
    result.update(status="NATIVE_FIDELITY_FEEDBACK_OBSERVED",
                  journal_failure=body.get("journal_failure"),
                  last_source_sequence=body.get("last_source_sequence"),
                  latest_result=receipt,
                  native_settlement_certified=False,
                  native_live_lockstep=False)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bridge", default="http://127.0.0.1:39221")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    report = read_status(
        args.bridge, os.environ.get("STS2_INSPECTOR_FIDELITY_TOKEN"))
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(report, sort_keys=True, indent=2) + "\n",
                            encoding="utf-8")
    print(json.dumps(report, sort_keys=True))
    return 0 if report["status"] == "NATIVE_FIDELITY_FEEDBACK_OBSERVED" else 2


if __name__ == "__main__":
    sys.exit(main())
