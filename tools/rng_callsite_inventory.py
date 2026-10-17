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
SCHEMA_ID = "emulator-rng-callsite-source-inventory-v2"

CALL_PATTERN = re.compile(
    r"\b(?P<api>PrototypeRng|NativeRngV111|NativeFisherYatesV111)"
    r"\.(?P<operation>NextInt|NextBool|NextFloat|NextDouble|"
    r"NextGaussianInt|NextGaussianDouble|Shuffle|ShuffleInPlace|Execute|NextItem)"
    r"\s*\("
)

STRING_CONSTANT_PATTERN = re.compile(
    r"\bconst\s+string\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r'"(?P<value>[^"\\]*(?:\\.[^"\\]*)*)"\s*;'
)


def _split_call_arguments(arguments: str) -> list[str]:
    """Split C# arguments at commas outside nested delimiters/strings."""
    result: list[str] = []
    start = 0
    depths = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    in_string = False
    escaped = False
    for index, character in enumerate(arguments):
        if in_string:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                in_string = False
            continue
        if character == '"':
            in_string = True
        elif character in depths:
            depths[character] += 1
        elif character in closing:
            depths[closing[character]] -= 1
        elif character == "," and not any(depths.values()):
            result.append(arguments[start:index].strip())
            start = index + 1
    tail = arguments[start:].strip()
    if tail:
        result.append(tail)
    return result


def _call_arguments(source: str, call_start: int) -> list[str]:
    open_paren = source.find("(", call_start)
    if open_paren < 0:
        return []
    depth = 0
    in_string = False
    escaped = False
    for index in range(open_paren, len(source)):
        character = source[index]
        if in_string:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                in_string = False
            continue
        if character == '"':
            in_string = True
        elif character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0:
                return _split_call_arguments(
                    source[open_paren + 1:index]
                )
    return []


def _site_id_argument(api: str, operation: str,
                      arguments: list[str]) -> str | None:
    if api != "NativeFisherYatesV111":
        return None
    for argument in arguments:
        named = re.match(r"siteId\s*:\s*(.+)$", argument, re.DOTALL)
        if named:
            return named.group(1).strip()
    position = {"ShuffleInPlace": 5, "Execute": 4}.get(operation)
    if position is None or len(arguments) <= position:
        return None
    return arguments[position]


def _string_constants(source: str) -> dict[str, set[str]]:
    constants: dict[str, set[str]] = {}
    for match in STRING_CONSTANT_PATTERN.finditer(source):
        constants.setdefault(match.group("name"), set()).add(
            match.group("value")
        )
    return constants


def _resolve_site_value(
    expression: str,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> str | None:
    if expression.startswith('"') and expression.endswith('"'):
        return expression[1:-1]
    constant = re.fullmatch(
        r"(?:(?P<class>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\.)?"
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)",
        expression,
    )
    if constant is None:
        return None
    name = constant.group("name")
    class_name = constant.group("class")
    candidates: set[str] = set()
    if class_name is not None:
        class_file = SOURCE_ROOT / (class_name.rsplit(".", 1)[-1] + ".cs")
        if class_file.is_file():
            candidates = _string_constants(
                class_file.read_text(encoding="utf-8")
            ).get(name, set())
    else:
        candidates = local_constants.get(name, set())
        if not candidates:
            candidates = global_constants.get(name, set())
    return next(iter(candidates)) if len(candidates) == 1 else None


def _migration_matrix_site_ids() -> set[str]:
    matrix_path = ROOT / "docs" / "DESIRED_RNG_MIGRATION_STATUS.md"
    site_ids: set[str] = set()
    for line in matrix_path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"\|\s*`([^`]+)`\s*\|", line)
        if match:
            site_ids.add(match.group(1))
    return site_ids


def collect() -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    matrix_site_ids = _migration_matrix_site_ids()
    sources = {
        path: path.read_text(encoding="utf-8")
        for path in sorted(SOURCE_ROOT.rglob("*.cs"))
    }
    string_constants: dict[str, set[str]] = {}
    for source in sources.values():
        for name, values in _string_constants(source).items():
            string_constants.setdefault(name, set()).update(values)
    for path, source in sources.items():
        relative = path.relative_to(ROOT).as_posix()
        local_constants = _string_constants(source)
        source_offset = 0
        for line_number, line in enumerate(
            source.splitlines(), start=1
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
                arguments = _call_arguments(
                    source, source_offset + match.start()
                )
                site_expression = _site_id_argument(
                    api, match.group("operation"), arguments
                )
                site_value = None
                if site_expression is not None:
                    site_value = _resolve_site_value(
                        site_expression, local_constants, string_constants
                    )
                if api == "NativeFisherYatesV111":
                    if site_expression is None:
                        review_status = "shuffle_site_id_argument_missing"
                    elif site_value is not None:
                        review_status = "shuffle_site_id_resolved"
                    else:
                        review_status = "shuffle_site_id_is_dynamic_or_unresolved"
                    matrix_status = (
                        "mapped"
                        if site_value in matrix_site_ids
                        else "unmapped"
                        if site_value is not None
                        else "dynamic_expression"
                        if site_expression is not None
                        else "missing"
                    )
                else:
                    matrix_status = "not_applicable"
                entries.append(
                    {
                        "path": relative,
                        "line": line_number,
                        "api": api,
                        "operation": match.group("operation"),
                        "category": category,
                        "review_status": review_status,
                        "site_id_argument": site_expression,
                        "site_id_value": site_value,
                        "matrix_row_status": matrix_status,
                        "source_excerpt": line.strip(),
                    }
                )
            source_offset += len(line) + 1

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
        "shuffle_matrix_mapping_counts": {
            status: sum(
                entry["matrix_row_status"] == status
                for entry in entries
                if entry["api"] == "NativeFisherYatesV111"
            )
            for status in (
                "mapped", "dynamic_expression", "unmapped", "missing"
            )
        },
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
        "Shuffle site IDs mapped to migration-matrix rows: "
        f"**{report['shuffle_matrix_mapping_counts']['mapped']}**; dynamic "
        f"expressions: **{report['shuffle_matrix_mapping_counts']['dynamic_expression']}**; "
        f"unmapped: **{report['shuffle_matrix_mapping_counts']['unmapped']}**; "
        f"missing: **{report['shuffle_matrix_mapping_counts']['missing']}**.",
        "Rows distinguish legacy prototype calls, native primitive calls, "
        "generic shuffle-helper calls, and recognized provider/fixture support. "
        "Shuffle rows include their `siteId` argument, resolved constant where "
        "available, and exact migration-matrix match for static IDs. Dynamic "
        "expressions still need caller-level tracing.",
        "",
        "| File:line | API | Operation | Stable site ID argument | Matrix row | Source expression | Review status |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    for entry in report["entries"]:
        path = entry["path"]
        operation = entry["operation"]
        excerpt = entry["source_excerpt"].replace("|", "\\|")
        status = entry["review_status"]
        matrix_status = entry["matrix_row_status"]
        site_expression = entry["site_id_argument"] or "—"
        site_expression = site_expression.replace("|", "\\|")
        if entry["site_id_value"] is not None:
            site_expression = (
                "`" + site_expression + "` → `"
                + entry["site_id_value"] + "`"
            )
        elif site_expression != "—":
            site_expression = "`" + site_expression + "`"
        source_link = "../" + path + "#L" + str(entry["line"])
        lines.append(
            f"| [`{path}:{entry['line']}`]({source_link}) | `{entry['api']}` | "
            f"`{operation}` | {site_expression} | `{matrix_status}` | "
            f"`{excerpt}` | `{status}` |"
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
