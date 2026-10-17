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
import sys
from collections import Counter
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "src" / "Sts2Emulator.Core"
SCHEMA_ID = "emulator-rng-callsite-source-inventory-v5"
SITE_REGISTRY_PATH = SOURCE_ROOT / "StochasticSiteRegistryV111.cs"

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

SITE_ARRAY_PATTERN = re.compile(
    r"\bstring\[\]\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*\[(?P<body>.*?)\];",
    re.DOTALL,
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


def _migration_matrix_site_rows() -> list[dict[str, Any]]:
    matrix_path = ROOT / "docs" / "DESIRED_RNG_MIGRATION_STATUS.md"
    site_rows: list[dict[str, Any]] = []
    for line in matrix_path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"\|\s*`([^`]+)`\s*\|", line)
        if match:
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if len(cells) != 8:
                raise ValueError(
                    f"Migration row for {match.group(1)} has {len(cells)} "
                    "cells; expected 8."
                )
            links = [target for _, target in re.findall(
                r"\[([^]]+)\]\(([^)]+)\)", line
            )]
            site_rows.append({
                "site_id": match.group(1),
                "owner": cells[1],
                "source_audit": cells[2],
                "native_implemented": cells[3],
                "native_parity": cells[4],
                "controlled_implemented": cells[5],
                "controlled_mechanics": cells[6],
                "reference_law_status": cells[7],
                "links": links,
            })
    return site_rows


def _site_matrix_contract_evidence(
    site_rows: list[dict[str, Any]],
) -> dict[str, Any]:
    """Locate row-level S0 evidence without treating keyword matches as proof."""
    facet_patterns = {
        "private_constraint": re.compile(
            r"constraint|fingerprint|eligible|eligibility|ordered pool|filter|support",
            re.IGNORECASE,
        ),
        "audience_projection": re.compile(
            r"player.?visible|public.?view|observation|nature.?view|"
            r"privileged.?inspector|inspector.?view|projection",
            re.IGNORECASE,
        ),
        "resolver_continuation": re.compile(
            r"resolver|continuation|resume|replay|hook.once|source.?owned",
            re.IGNORECASE,
        ),
        "probability_provenance": re.compile(
            r"provenance|UNVERIFIED|source.verified|calibrated",
            re.IGNORECASE,
        ),
        "unsupported_handling": re.compile(
            r"fail(?:s|ed)? closed|unsupported|reject|outside|source.?owned|"
            r"no controlled|not supported",
            re.IGNORECASE,
        ),
    }
    doc_text: dict[str, str] = {}
    audit_rows: list[dict[str, Any]] = []
    missing_by_facet: dict[str, list[str]] = {
        facet: [] for facet in facet_patterns
    }

    for row in site_rows:
        linked_text: list[str] = []
        linked_docs: list[str] = []
        for target in row["links"]:
            parsed = urlparse(target)
            if parsed.scheme or parsed.netloc:
                continue
            doc_path = (ROOT / "docs" / parsed.path).resolve()
            try:
                relative = doc_path.relative_to(ROOT.resolve()).as_posix()
            except ValueError:
                continue
            if not doc_path.is_file():
                continue
            if relative not in doc_text:
                doc_text[relative] = doc_path.read_text(encoding="utf-8")
            linked_docs.append(relative)
            linked_text.append(doc_text[relative])

        row_text = " ".join(
            row[key] for key in (
                "owner", "source_audit", "native_implemented",
                "native_parity", "controlled_implemented",
                "controlled_mechanics", "reference_law_status",
            )
        )
        evidence = {
            "private_constraint": bool(
                facet_patterns["private_constraint"].search(row["owner"] + " " + row["source_audit"])
                or any(facet_patterns["private_constraint"].search(doc) for doc in linked_text)
            ),
            "audience_projection": bool(
                facet_patterns["audience_projection"].search(row_text)
                or any(facet_patterns["audience_projection"].search(doc) for doc in linked_text)
            ),
            "resolver_continuation": bool(
                facet_patterns["resolver_continuation"].search(row_text)
                or any(facet_patterns["resolver_continuation"].search(doc) for doc in linked_text)
            ),
            "probability_provenance": bool(
                facet_patterns["probability_provenance"].search(row["reference_law_status"])
                or any(facet_patterns["probability_provenance"].search(doc) for doc in linked_text)
            ),
            "unsupported_handling": bool(
                facet_patterns["unsupported_handling"].search(row["controlled_implemented"])
                or any(facet_patterns["unsupported_handling"].search(doc) for doc in linked_text)
            ),
        }
        missing = [facet for facet, present in evidence.items() if not present]
        for facet in missing:
            missing_by_facet[facet].append(row["site_id"])
        audit_rows.append({
            "site_id": row["site_id"],
            "linked_contract_docs": sorted(set(linked_docs)),
            "evidence_presence": evidence,
            "missing_facets": missing,
        })

    return {
        "method": (
            "Keyword and linked-document locator for manual S0 review. A match "
            "only identifies a place to inspect; it does not prove that the "
            "site-specific contract is complete or correct."
        ),
        "row_count": len(audit_rows),
        "rows_with_all_facets_located": sum(not row["missing_facets"] for row in audit_rows),
        "missing_by_facet": {
            facet: {
                "count": len(site_ids),
                "site_ids": site_ids,
            }
            for facet, site_ids in missing_by_facet.items()
        },
        "rows": audit_rows,
    }


def _law_status_kind(status: str) -> str:
    if "UNVERIFIED" in status:
        return "unverified"
    if re.match(r"^Yes(?:\b|:)", status):
        return "calibrated"
    return "unspecified"


def _registry_site_ids(
    registry_source: str,
    global_constants: dict[str, set[str]],
) -> tuple[set[str], list[str]]:
    """Resolve the site arrays already declared by the authoritative registry."""
    local_constants = _string_constants(registry_source)
    site_ids: set[str] = set()
    unresolved: list[str] = []
    for array in SITE_ARRAY_PATTERN.finditer(registry_source):
        for expression in _split_call_arguments(array.group("body")):
            site_id = _resolve_site_value(
                expression, local_constants, global_constants
            )
            if site_id is None:
                unresolved.append(expression)
            else:
                site_ids.add(site_id)
    return site_ids, sorted(set(unresolved))


def collect() -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    matrix_site_rows = _migration_matrix_site_rows()
    matrix_site_id_counts: Counter[str] = Counter(
        row["site_id"] for row in matrix_site_rows
    )
    matrix_site_ids = set(matrix_site_id_counts)
    law_status_counts: Counter[str] = Counter(
        _law_status_kind(row["reference_law_status"])
        for row in matrix_site_rows
    )
    rows_without_explicit_law_status = sorted(
        row["site_id"] for row in matrix_site_rows
        if _law_status_kind(row["reference_law_status"]) == "unspecified"
    )
    site_matrix_contract_evidence = _site_matrix_contract_evidence(
        matrix_site_rows
    )
    sources = {
        path: path.read_text(encoding="utf-8")
        for path in sorted(SOURCE_ROOT.rglob("*.cs"))
    }
    string_constants: dict[str, set[str]] = {}
    for source in sources.values():
        for name, values in _string_constants(source).items():
            string_constants.setdefault(name, set()).update(values)
    registry_source = sources.get(SITE_REGISTRY_PATH)
    if registry_source is None:
        raise FileNotFoundError(SITE_REGISTRY_PATH)
    registry_site_ids, unresolved_registry_references = _registry_site_ids(
        registry_source, string_constants
    )
    registry_site_ids_without_matrix_rows = sorted(
        registry_site_ids - matrix_site_ids
    )
    duplicate_matrix_site_ids = sorted(
        site_id for site_id, count in matrix_site_id_counts.items()
        if count > 1
    )
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
            "source callsites, or prove that every source operation is mapped. "
            "The site-matrix checks verify registry coverage, unique ledger IDs "
            "and explicit reference-law status. The S0 locator reports text or "
            "linked-document matches for private constraints, audience projections, "
            "resolvers/continuations, probability provenance, and unsupported handling; "
            "these matches require manual review and do not prove source completeness."
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
        "registry_matrix_coverage": {
            "registered_site_count": len(registry_site_ids),
            "mapped_site_count": len(registry_site_ids & matrix_site_ids),
            "site_ids_without_matrix_rows":
                registry_site_ids_without_matrix_rows,
            "duplicate_matrix_site_ids": duplicate_matrix_site_ids,
            "unresolved_registry_references":
                unresolved_registry_references,
            "reference_law_status": {
                "unverified_rows": law_status_counts["unverified"],
                "calibrated_rows": law_status_counts["calibrated"],
                "unspecified_rows": law_status_counts["unspecified"],
                "rows_without_explicit_status":
                    rows_without_explicit_law_status,
            },
        },
        "site_matrix_contract_evidence": site_matrix_contract_evidence,
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
        "ReferenceNature registry IDs mapped to migration-matrix rows: "
        f"**{report['registry_matrix_coverage']['mapped_site_count']}/"
        f"{report['registry_matrix_coverage']['registered_site_count']}**; "
        f"IDs without rows: **{len(report['registry_matrix_coverage']['site_ids_without_matrix_rows'])}**; "
        f"duplicate matrix IDs: **{len(report['registry_matrix_coverage']['duplicate_matrix_site_ids'])}**; "
        f"unresolved registry references: **{len(report['registry_matrix_coverage']['unresolved_registry_references'])}**.",
        "Reference-law column status: "
        f"**{report['registry_matrix_coverage']['reference_law_status']['unverified_rows']} UNVERIFIED**; "
        f"**{report['registry_matrix_coverage']['reference_law_status']['calibrated_rows']} calibrated**; "
        f"**{report['registry_matrix_coverage']['reference_law_status']['unspecified_rows']} unspecified**.",
        "S0 contract-evidence locator: "
        f"**{report['site_matrix_contract_evidence']['rows_with_all_facets_located']}/"
        f"{report['site_matrix_contract_evidence']['row_count']} rows** have a text or linked-document match for every facet. "
        "Matches are review pointers, not evidence that a contract is complete.",
        "Rows without a text or linked-document match by S0 facet:",
        "",
        *[
            f"- `{facet}`: {details['count']}"
            for facet, details in sorted(
                report["site_matrix_contract_evidence"]["missing_by_facet"].items()
            )
        ],
        "",
        "Regenerate this report with `python3 tools/rng_callsite_inventory.py "
        "--check-site-matrix --check-s0-contract-links --format markdown > "
        "docs/RNG_DIRECT_CALLSITE_CENSUS.md`.",
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
    parser.add_argument(
        "--check-site-matrix", "--check-registry-matrix",
        dest="check_site_matrix", action="store_true",
        help="fail on registry gaps, duplicate rows, or unspecified reference-law status",
    )
    parser.add_argument(
        "--check-s0-contract-links", action="store_true",
        help="fail if a matrix row links to a missing local contract document",
    )
    args = parser.parse_args()
    report = collect()
    if args.check_site_matrix:
        coverage = report["registry_matrix_coverage"]
        if (coverage["registered_site_count"] == 0 or
                coverage["site_ids_without_matrix_rows"] or
                coverage["duplicate_matrix_site_ids"] or
                coverage["unresolved_registry_references"] or
                coverage["reference_law_status"]["rows_without_explicit_status"]):
            print(json.dumps(coverage, indent=2, sort_keys=True),
                  file=sys.stderr)
            return 1
    if args.check_s0_contract_links:
        broken_contract_links = sorted({
            target
            for row in _migration_matrix_site_rows()
            for target in row["links"]
            if not urlparse(target).scheme
            and not urlparse(target).netloc
            and not (ROOT / "docs" / urlparse(target).path).is_file()
        })
        if broken_contract_links:
            print(json.dumps({
                "broken_local_contract_links": broken_contract_links,
            }, indent=2, sort_keys=True), file=sys.stderr)
            return 1
    if args.format == "markdown":
        print(render_markdown(report), end="")
    else:
        print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
