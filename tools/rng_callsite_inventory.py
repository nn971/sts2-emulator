#!/usr/bin/env python3
"""List direct emulator RNG API call expressions for the D0 source census.

This is a source inventory aid, not a source-to-game completeness proof. It
deliberately reports each direct expression with its file and line so manual
site audits can be linked to a stable matrix row. Indirect delegates and
mechanics hidden behind called helpers still require manual tracing.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "src" / "Sts2Emulator.Core"
SCHEMA_ID = "emulator-rng-callsite-source-inventory-v1"

CALL_PATTERN = re.compile(
    r"\b(?P<api>PrototypeRng|NativeRngV111|NativeFisherYatesV111)"
    r"\.(?P<operation>NextInt|NextBool|NextFloat|NextDouble|"
    r"NextGaussianInt|NextGaussianDouble|Shuffle|ShuffleInPlace|Execute|NextItem)"
    r"\s*\("
)


def collect() -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    for path in sorted(SOURCE_ROOT.rglob("*.cs")):
        relative = path.relative_to(ROOT).as_posix()
        for line_number, line in enumerate(
            path.read_text(encoding="utf-8").splitlines(), start=1
        ):
            for match in CALL_PATTERN.finditer(line):
                api = match.group("api")
                category = {
                    "PrototypeRng": "legacy_prototype_api",
                    "NativeRngV111": "native_primitive_api",
                    "NativeFisherYatesV111": "native_shuffle_mechanics",
                }[api]
                filename = path.name
                if filename in {
                    "NativeRngV111.cs",
                    "NativeRunRngV111.cs",
                    "NativeRngV111Contexts.cs",
                    "NativeShuffleSourceImportV111.cs",
                } or filename.startswith("Random") and "ProvidersV111" in filename:
                    review_status = "provider_or_codec_infrastructure"
                elif "Fixture" in filename or "Replay" in filename:
                    review_status = "fixture_or_replay_support"
                elif api == "PrototypeRng":
                    review_status = "prototype_callsite_needs_semantic_site_mapping"
                elif api == "NativeFisherYatesV111":
                    review_status = "typed_provider_shuffle_mechanics_boundary"
                else:
                    review_status = "native_primitive_callsite_needs_site_review"
                entries.append(
                    {
                        "path": relative,
                        "line": line_number,
                        "api": api,
                        "operation": match.group("operation"),
                        "category": category,
                        "review_status": review_status,
                        "source_excerpt": line.strip(),
                    }
                )

    counts: dict[str, int] = {}
    for entry in entries:
        key = entry["category"]
        counts[key] = counts.get(key, 0) + 1

    return {
        "schema_id": SCHEMA_ID,
        "scope": (
            "Direct qualified calls to PrototypeRng, NativeRngV111, and "
            "NativeFisherYatesV111 in Sts2Emulator.Core. This does not include "
            "indirect delegates, random provider interfaces, third-party/native "
            "source callsites, or prove that every source operation is mapped."
        ),
        "total_direct_calls": len(entries),
        "counts_by_category": counts,
        "entries": entries,
    }


def render_markdown(report: dict[str, Any]) -> str:
    lines = [
        f"# RNG direct-call source inventory ({report['schema_id']})",
        "",
        report["scope"],
        "",
        f"Direct call expressions: **{report['total_direct_calls']}**.",
        "",
        "Counts by direct API family:",
        "",
        *[
            f"- `{category}`: {count}"
            for category, count in sorted(
                report["counts_by_category"].items()
            )
        ],
        "",
        "Rows distinguish legacy prototype calls, native primitive calls, "
        "generic shuffle-helper calls, and recognized provider/fixture support. "
        "Unrecognized contexts still need manual source tracing.",
        "",
        "| File:line | API | Operation | Source expression | Review status |",
        "| --- | --- | --- | --- | --- |",
    ]
    for entry in report["entries"]:
        path = entry["path"]
        operation = entry["operation"]
        excerpt = entry["source_excerpt"].replace("|", "\\|")
        status = entry["review_status"]
        source_link = "../" + path + "#L" + str(entry["line"])
        lines.append(
            f"| [`{path}:{entry['line']}`]({source_link}) | `{entry['api']}` | "
            f"`{operation}` | `{excerpt}` | `{status}` |"
        )
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--format", choices=("json", "markdown"), default="json",
        help="report format (default: json)",
    )
    args = parser.parse_args()
    report = collect()
    if args.format == "markdown":
        print(render_markdown(report), end="")
    else:
        print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
