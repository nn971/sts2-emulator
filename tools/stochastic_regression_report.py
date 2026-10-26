#!/usr/bin/env python3
"""Compare source-independent emulator test outcomes without conflating new tests and regressions.

This tool consumes real VSTest TRX results from the frozen main and a feature
revision. It distinguishes tests that passed on main and now fail from tests
that were added on the feature branch and have never passed on main.

No installed-game traces, Inspector, reference mod or training dependencies.
"""
from __future__ import annotations

import argparse
from collections import Counter
from pathlib import Path
import json
import re
import sys
import xml.etree.ElementTree as ET


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def results(path: Path) -> dict[str, dict[str, str]]:
    """Return {fully-qualified test case identity: {outcome, message}}."""
    root = ET.parse(path).getroot()
    definitions = {}
    for element in root.iter():
        if local_name(element.tag) != "UnitTest":
            continue
        method = next((e for e in element if local_name(e.tag) == "TestMethod"), None)
        if method is None:
            continue
        cls = method.get("className", "").split(",")[0]
        definitions[element.get("id", "")] = (cls, method.get("name", ""))

    found = {}
    for element in root.iter():
        if local_name(element.tag) != "UnitTestResult":
            continue
        tid = element.get("testId", "")
        cls, method = definitions.get(tid, ("", ""))
        name = element.get("testName") or method
        if cls and name.startswith(cls + "."):
            full = name
        elif cls and name.startswith(cls.rsplit(".", 1)[-1] + "."):
            full = cls.rsplit(".", 1)[0] + "." + name
        else:
            full = cls + "." + name if cls else name
        if not full:
            raise ValueError("TRX test has no class or case name")
        message = ""
        for child in element.iter():
            if local_name(child.tag) == "Message" and child.text:
                message = child.text.strip().splitlines()[0]
                break
        outcome = element.get("outcome", "Unknown")
        if full in found and found[full]["outcome"] != outcome:
            raise ValueError(f"Ambiguous repeated test identity: {full}")
        found[full] = {"outcome": outcome, "message": message}
    if not found:
        raise ValueError(f"No UnitTestResult rows found in {path}")
    return found


def category(message: str) -> str:
    """Diagnostic grouping only; not a proof of root cause."""
    m = message.lower()
    if "drawplayerhand checkpoint" in m:
        return "automatic_hand_draw_checkpoint"
    if "encounter-history" in m or "ineligible at act" in m:
        return "encounter_fixture_or_state_validation"
    if "card instance" in m and "missing" in m:
        return "physical_card_identity"
    if "requires a bounded-integer request" in m:
        return "rng_operation_contract"
    if "not the exact type" in m or "exception type was not an exact match" in m:
        return "assertion_or_compatibility_change"
    if "unsupported" in m:
        return "unsupported_mechanics_or_site"
    return "other_requires_triage"


def compare(baseline: dict, candidate: dict) -> dict:
    common = sorted(baseline.keys() & candidate.keys())
    new = sorted(candidate.keys() - baseline.keys())
    absent = sorted(baseline.keys() - candidate.keys())
    is_bad = lambda x: x["outcome"].lower() in ("failed", "error", "timeout")
    regressions = [n for n in common if not is_bad(baseline[n]) and is_bad(candidate[n])]
    known = [n for n in common if is_bad(baseline[n]) and is_bad(candidate[n])]
    repaired = [n for n in common if is_bad(baseline[n]) and not is_bad(candidate[n])]
    introduced_failing = [n for n in new if is_bad(candidate[n])]
    details = [{
        "test": n,
        "baseline": baseline[n]["outcome"],
        "candidate": candidate[n]["outcome"],
        "category": category(candidate[n]["message"]),
        "error": candidate[n]["message"],
    } for n in regressions]
    return {
        "baseline": dict(Counter(item["outcome"] for item in baseline.values())),
        "candidate": dict(Counter(item["outcome"] for item in candidate.values())),
        "compared_count": len(common),
        "baseline_only_count": len(absent),
        "feature_only_count": len(new),
        "new_regressions_count": len(regressions),
        "shared_existing_failures_count": len(known),
        "previously_failing_now_passing_count": len(repaired),
        "feature_only_failures_count": len(introduced_failing),
        "new_regressions_by_diagnostic": dict(sorted(Counter(
            detail["category"] for detail in details).items())),
        "new_regressions": details,
        "feature_only_failed_tests": introduced_failing,
        "repaired_tests": repaired,
        "baseline_only_tests": absent,
        "interpretation": (
            "Only tests passed on baseline and failed on candidate count as "
            "confirmed test regressions. Diagnostic categories suggest triage "
            "areas, not proven implementation root causes."
        ),
    }


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--baseline", type=Path, required=True, help="Frozen main TRX")
    p.add_argument("--candidate", type=Path, required=True, help="Feature TRX")
    p.add_argument("--json", type=Path, required=True, help="Output JSON path")
    p.add_argument("--strict", action="store_true",
                   help="Fail when previously passing tests now fail")
    a = p.parse_args()
    result = compare(results(a.baseline), results(a.candidate))
    a.json.parent.mkdir(parents=True, exist_ok=True)
    a.json.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n",
                      encoding="utf-8")
    print("Frozen baseline", result["baseline"])
    print("Candidate", result["candidate"])
    for key in ("compared_count", "new_regressions_count",
                "shared_existing_failures_count",
                "previously_failing_now_passing_count",
                "feature_only_failures_count", "baseline_only_count"):
        print(key, result[key])
    print("Diagnosis", result["new_regressions_by_diagnostic"])
    return 1 if a.strict and result["new_regressions_count"] else 0


if __name__ == "__main__":
    sys.exit(main())
