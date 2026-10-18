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
SCHEMA_ID = "emulator-rng-callsite-source-inventory-v13"
SITE_REGISTRY_PATH = SOURCE_ROOT / "StochasticSiteRegistryV111.cs"

CALL_PATTERN = re.compile(
    r"\b(?P<api>PrototypeRng|NativeRngV111|NativeFisherYatesV111)"
    r"\.(?P<operation>NextInt|NextBool|NextFloat|NextDouble|"
    r"NextGaussianInt|NextGaussianDouble|Shuffle|ShuffleInPlace|Execute|NextItem)"
    r"\s*\("
)

AMBIENT_ENTROPY_PATTERNS = (
    ("system_random_constructor", re.compile(
        r"\bnew\s+(?:(?:global::)?System\.)?Random\s*\("
    )),
    ("system_random_target_typed_constructor", re.compile(
        r"\b(?:(?:global::)?System\.)?Random\??\s+"
        r"[A-Za-z_][A-Za-z0-9_]*\s*=\s*new\s*\("
    )),
    ("system_random_shared", re.compile(
        r"\b(?:(?:global::)?System\.)?Random\s*\.\s*Shared\b"
    )),
    ("cryptographic_random_api", re.compile(
        r"\b(?:(?:global::)?System\.Security\.Cryptography\.)?"
        r"RandomNumberGenerator\s*\.\s*"
        r"(?:GetInt32|GetBytes|GetNonZeroBytes|Fill|Create)\s*\("
    )),
)

REQUEST_CONSTRUCTION_PATTERN = re.compile(
    r"\bnew\s+(?P<request_type>RandomIndexRequestV111|"
    r"RandomUnitFloatRequestV111|RandomRangedFloatRequestV111|"
    r"RandomRangedDoubleRequestV111|RandomGaussianIntRequestV111|"
    r"RandomGaussianDoubleRequestV111)\s*\("
)

STRING_CONSTANT_PATTERN = re.compile(
    r"\bconst\s+string\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r'"(?P<value>[^"\\]*(?:\\.[^"\\]*)*)"\s*;'
)

STRING_LITERAL_PATTERN = re.compile(
    r'"(?P<value>[^"\\]*(?:\\.[^"\\]*)*)"'
)

SITE_ARRAY_PATTERN = re.compile(
    r"\bstring\[\]\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r"\[(?P<body>.*?)\];",
    re.DOTALL,
)

SITE_ARRAY_PROPERTY_PATTERN = re.compile(
    r"\bstring\[\]\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*"
    r"(?P<body>[^;]+);",
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
    expression = re.sub(r"\s+", "", expression)
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
        candidate_files = ([class_file] if class_file.is_file() else []) + [
            path for path in sorted(SOURCE_ROOT.rglob("*.cs"))
            if path != class_file
        ]
        for candidate in candidate_files:
            source = candidate.read_text(encoding="utf-8")
            if not re.search(
                rf"\b(?:class|record|struct)\s+"
                rf"{re.escape(class_name.rsplit('.', 1)[-1])}\b",
                source,
            ):
                continue
            candidates.update(_string_constants(source).get(name, set()))
    else:
        candidates = local_constants.get(name, set())
        if not candidates:
            candidates = global_constants.get(name, set())
    return next(iter(candidates)) if len(candidates) == 1 else None


def _conditional_site_branches(expression: str) -> tuple[str, str] | None:
    """Split one top-level C# conditional expression, ignoring strings/nesting."""
    question_index = None
    depth = {"(": 0, "[": 0, "{": 0}
    closing = {")": "(", "]": "[", "}": "{"}
    in_string = False
    escaped = False
    for index, character in enumerate(expression):
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
        elif character in depth:
            depth[character] += 1
        elif character in closing:
            depth[closing[character]] -= 1
        elif character == "?" and not any(depth.values()) and \
                not expression.startswith(("??", "?."), index):
            question_index = index
            break
    if question_index is None:
        return None

    nested_conditionals = 0
    depth = {"(": 0, "[": 0, "{": 0}
    in_string = False
    escaped = False
    for index in range(question_index + 1, len(expression)):
        character = expression[index]
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
        elif character in depth:
            depth[character] += 1
        elif character in closing:
            depth[closing[character]] -= 1
        elif any(depth.values()):
            continue
        elif character == "?" and not expression.startswith(("??", "?."), index):
            nested_conditionals += 1
        elif character == ":":
            if nested_conditionals:
                nested_conditionals -= 1
            else:
                return (
                    expression[question_index + 1:index].strip(),
                    expression[index + 1:].strip(),
                )
    return None


def _resolve_site_values(
    expression: str | None,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve exact or statically visible alternatives in a site expression."""
    if expression is None:
        return []
    branches = _conditional_site_branches(expression)
    if branches is not None:
        values: set[str] = set()
        for branch in branches:
            values.update(_resolve_site_values(
                branch, local_constants, global_constants
            ))
        return sorted(values)
    exact = _resolve_site_value(
        expression, local_constants, global_constants
    )
    if exact is not None:
        return [exact]

    values = {
        match.group("value")
        for match in STRING_LITERAL_PATTERN.finditer(expression)
    }
    member_pattern = re.compile(
        r"\b[A-Za-z_][A-Za-z0-9_]*"
        r"(?:\.[A-Za-z_][A-Za-z0-9_]*)+\b"
    )
    for match in member_pattern.finditer(expression):
        value = _resolve_site_value(
            match.group(0), local_constants, global_constants
        )
        if value is not None:
            values.add(value)
    return sorted(values)


def _resolve_site_array_property(
    expression: str,
    global_constants: dict[str, set[str]],
    active: frozenset[str] = frozenset(),
) -> tuple[list[str], list[str]]:
    """Resolve a spread of a source-owned string-array property.

    The registry can compose its existing site lists without copying their
    stable IDs. Follow those property references for the census as well.
    """
    reference = expression.strip()
    if reference.startswith(".."):
        reference = reference[2:].strip()
    match = re.fullmatch(
        r"(?P<class>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\."
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)",
        reference,
    )
    if match is None:
        return [], [expression.strip()]
    class_name = match.group("class").rsplit(".", 1)[-1]
    property_name = match.group("name")
    identity = f"{class_name}.{property_name}"
    if identity in active:
        return [], [expression.strip()]
    class_file = SOURCE_ROOT / f"{class_name}.cs"
    candidate_files = (
        [class_file] if class_file.is_file()
        else sorted(SOURCE_ROOT.rglob("*.cs"))
    )
    property_body = None
    class_source = None
    for candidate in candidate_files:
        source = candidate.read_text(encoding="utf-8")
        if not re.search(rf"\bclass\s+{re.escape(class_name)}\b", source):
            continue
        property_body = next((item.group("body")
            for item in SITE_ARRAY_PROPERTY_PATTERN.finditer(source)
            if item.group("name") == property_name), None)
        if property_body is not None:
            class_source = source
            break
    if property_body is None or class_source is None:
        return [], [expression.strip()]

    next_active = active | {identity}
    property_expression = re.sub(r"\s+", "", property_body)
    if property_expression.startswith("[") and property_expression.endswith("]"):
        property_body = property_body.strip()[1:-1]
        property_expression = re.sub(r"\s+", "", property_body)
    clone = re.fullmatch(
        r"\(string\[\]\)(?P<name>[A-Za-z_][A-Za-z0-9_]*)\.Clone\(\)",
        property_expression,
    )
    if clone is not None:
        array_name = clone.group("name")
        array = next((item for item in SITE_ARRAY_PATTERN.finditer(class_source)
                      if item.group("name") == array_name), None)
        if array is None:
            return [], [expression.strip()]
        property_body = array.group("body")
    else:
        dictionary_keys = re.fullmatch(
            r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\.Keys"
            r"\.Order\(StringComparer\.Ordinal\)\.ToArray\(\)",
            property_expression,
        )
        if dictionary_keys is not None:
            dictionary_name = dictionary_keys.group("name")
            assignment = re.search(
                rf"\b{re.escape(dictionary_name)}\s*=\s*new\b",
                class_source,
            )
            opening = -1 if assignment is None else class_source.find(
                "{", assignment.end()
            )
            closing = -1 if opening < 0 else class_source.find("};", opening)
            if closing < 0:
                return [], [expression.strip()]
            dictionary_body = class_source[opening:closing]
            keys = [match.group("key").strip() for match in re.finditer(
                r"\[\s*(?P<key>[^\]]+?)\s*\]\s*=", dictionary_body
            )]
            if not keys:
                return [], [expression.strip()]
            property_body = ",".join(keys)

    values: list[str] = []
    unresolved: list[str] = []
    for item in _split_call_arguments(property_body):
        if item.startswith(".."):
            nested, missing = _resolve_site_array_property(
                item, global_constants, next_active
            )
            values.extend(nested)
            unresolved.extend(missing)
            continue
        site_id = _resolve_site_value(item, {}, global_constants)
        if site_id is None:
            unresolved.append(item)
        else:
            values.append(site_id)
    return values, unresolved


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
    if re.match(r"^Conditional(?:\b|:)", status, re.IGNORECASE):
        return "conditional"
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
            if expression.startswith(".."):
                values, missing = _resolve_site_array_property(
                    expression, global_constants
                )
                site_ids.update(values)
                unresolved.extend(missing)
                continue
            site_id = _resolve_site_value(
                expression, local_constants, global_constants
            )
            if site_id is None:
                unresolved.append(expression)
            else:
                site_ids.add(site_id)
    return site_ids, sorted(set(unresolved))


def _site_id_forwarder_calls(
    sources: dict[Path, str],
    global_constants: dict[str, set[str]],
    registry_site_ids: set[str],
    matrix_site_ids: set[str],
) -> list[dict[str, Any]]:
    """Trace site arguments at known RNG-helper call boundaries.

    NativeFisherYates call expressions inside these helpers receive their
    semantic site from the owning mechanics call. Keep this supplemental to
    the direct RNG-call census: the helper callsites are where the stable
    site is chosen, while provider and transcript wrappers are not sites.
    """
    specifications = (
        {
            "callee": "NativeEventContextRngV111.ShuffleInPlace",
            "pattern": re.compile(
                r"\bNativeEventContextRngV111\.ShuffleInPlace\s*\("
            ),
            "site_argument": 4,
            "paths": None,
        },
        {
            "callee": "PrototypeGameEngine.ShuffleCombatCardPool",
            "pattern": re.compile(r"\bShuffleCombatCardPool\s*\("),
            "site_argument": 3,
            "paths": {"PrototypeCombat.cs"},
        },
        {
            "callee": "NativeMapRandom.Shuffle",
            "pattern": re.compile(r"\brandom\.Shuffle\s*\("),
            "site_argument": 1,
            "paths": {
                "PrototypeNativeLaterActMap.cs",
                "PrototypeNativeOvergrowthMap.cs",
            },
        },
    )
    calls: list[dict[str, Any]] = []
    for path, source in sources.items():
        for specification in specifications:
            allowed_paths = specification["paths"]
            if allowed_paths is not None and path.name not in allowed_paths:
                continue
            for match in specification["pattern"].finditer(source):
                arguments = _call_arguments(source, match.start())
                position = specification["site_argument"]
                if len(arguments) <= position:
                    expression = None
                    value = None
                else:
                    expression = arguments[position]
                    value = _resolve_site_value(
                        expression, _string_constants(source),
                        global_constants,
                    )
                line = source.count("\n", 0, match.start()) + 1
                calls.append({
                    "path": path.relative_to(ROOT).as_posix(),
                    "line": line,
                    "helper": specification["callee"],
                    "site_id_expression": expression,
                    "site_id_value": value,
                    "registry_status": (
                        "mapped" if value in registry_site_ids else
                        "unresolved" if value is None else "not_registered"
                    ),
                    "matrix_status": (
                        "mapped" if value in matrix_site_ids else
                        "unresolved" if value is None else "missing"
                    ),
                })
    return sorted(calls, key=lambda call: (call["path"], call["line"]))


def _ambient_entropy_calls(
    sources: dict[Path, str],
) -> list[dict[str, Any]]:
    """Find direct ad hoc entropy APIs in the inventoried Core sources."""
    calls: list[dict[str, Any]] = []
    for path, source in sources.items():
        for api, pattern in AMBIENT_ENTROPY_PATTERNS:
            for match in pattern.finditer(source):
                line_number = source.count("\n", 0, match.start()) + 1
                arguments = _call_arguments(source, match.start())
                finding = api
                if api in {
                    "system_random_constructor",
                    "system_random_target_typed_constructor",
                }:
                    finding = (
                        "system_random_unseeded_constructor"
                        if not arguments else
                        "system_random_ad_hoc_seeded_constructor"
                    )
                calls.append({
                    "path": path.relative_to(ROOT).as_posix(),
                    "line": line_number,
                    "api": api,
                    "finding": finding,
                    "source_excerpt": source.splitlines()[
                        line_number - 1
                    ].strip(),
                })
    return sorted(calls, key=lambda call: (call["path"], call["line"]))


def _typed_request_constructions(
    sources: dict[Path, str],
    global_constants: dict[str, set[str]],
    registry_site_ids: set[str],
    matrix_site_ids: set[str],
) -> list[dict[str, Any]]:
    """Inventory explicit typed random-request construction boundaries."""
    calls: list[dict[str, Any]] = []
    for path, source in sources.items():
        local_constants = _string_constants(source)
        for match in REQUEST_CONSTRUCTION_PATTERN.finditer(source):
            arguments = _call_arguments(source, match.start())
            expression = arguments[0] if arguments else None
            if expression is not None:
                named = re.match(r"SiteId\s*:\s*(.+)$", expression, re.DOTALL)
                if named:
                    expression = named.group(1).strip()
            values = _resolve_site_values(
                expression, local_constants, global_constants
            )
            value = values[0] if len(values) == 1 else None
            registry_misses = [
                item for item in values if item not in registry_site_ids
            ]
            matrix_misses = [
                item for item in values if item not in matrix_site_ids
            ]
            registry_status = (
                "dynamic_expression" if not values and expression is not None
                else "missing" if not values
                else "mapped" if not registry_misses and len(values) == 1
                else "conditional" if not registry_misses
                else "not_registered" if len(registry_misses) == len(values)
                else "partially_unregistered"
            )
            matrix_status = (
                "dynamic_expression" if not values and expression is not None
                else "missing" if not values
                else "mapped" if not matrix_misses and len(values) == 1
                else "conditional" if not matrix_misses
                else "unmapped" if len(matrix_misses) == len(values)
                else "partially_unmapped"
            )
            line_number = source.count("\n", 0, match.start()) + 1
            calls.append({
                "path": path.relative_to(ROOT).as_posix(),
                "line": line_number,
                "request_type": match.group("request_type"),
                "site_id_expression": expression,
                "site_id_value": value,
                "site_id_values": values,
                "registry_status": registry_status,
                "matrix_status": matrix_status,
                "source_excerpt": source.splitlines()[
                    line_number - 1
                ].strip(),
            })
    return sorted(calls, key=lambda call: (call["path"], call["line"]))


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
    ambient_entropy_calls = _ambient_entropy_calls(sources)
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
    matrix_site_ids_without_registry = sorted(
        matrix_site_ids - registry_site_ids
    )
    matrix_site_ids_without_registry_set = set(
        matrix_site_ids_without_registry
    )
    matrix_rows_without_registry_by_law_status: Counter[str] = Counter(
        _law_status_kind(row["reference_law_status"])
        for row in matrix_site_rows
        if row["site_id"] in matrix_site_ids_without_registry_set
    )
    site_id_forwarder_calls = _site_id_forwarder_calls(
        sources, string_constants, registry_site_ids, matrix_site_ids
    )
    typed_request_constructions = _typed_request_constructions(
        sources, string_constants, registry_site_ids, matrix_site_ids
    )
    site_id_forwarder_failures = [
        call for call in site_id_forwarder_calls
        if call["matrix_status"] != "mapped"
    ]
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
            "A separate direct-API scan reports typed and target-typed "
            "System.Random construction, shared-instance use, and "
            "RandomNumberGenerator use; it cannot prove the absence of indirect "
            "or aliased entropy sources. Explicit typed random-request "
            "constructions are listed with their site ID and registry/matrix "
            "resolution status. "
            "The site-matrix report shows registry-to-ledger and ledger-to-registry "
            "differences, unique ledger IDs, and explicit reference-law status. "
            "Unregistered ledger rows are informational because legacy and conditional "
            "sites may remain outside the active registry. The S0 locator reports text or "
            "linked-document matches for private constraints, audience projections, "
            "resolvers/continuations, probability provenance, and unsupported handling; "
            "these matches require manual review and do not prove source completeness."
        ),
        "total_direct_calls": len(entries),
        "ambient_entropy_calls": ambient_entropy_calls,
        "typed_request_constructions": typed_request_constructions,
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
            "matrix_site_ids_without_registry":
                matrix_site_ids_without_registry,
            "matrix_rows_without_registry_by_law_status": {
                "unverified": matrix_rows_without_registry_by_law_status[
                    "unverified"],
                "conditional": matrix_rows_without_registry_by_law_status[
                    "conditional"],
                "calibrated": matrix_rows_without_registry_by_law_status[
                    "calibrated"],
                "unspecified": matrix_rows_without_registry_by_law_status[
                    "unspecified"],
            },
            "duplicate_matrix_site_ids": duplicate_matrix_site_ids,
            "unresolved_registry_references":
                unresolved_registry_references,
            "reference_law_status": {
                "unverified_rows": law_status_counts["unverified"],
                "conditional_rows": law_status_counts["conditional"],
                "calibrated_rows": law_status_counts["calibrated"],
                "unspecified_rows": law_status_counts["unspecified"],
                "rows_without_explicit_status":
                    rows_without_explicit_law_status,
            },
        },
        "site_id_forwarder_coverage": {
            "call_count": len(site_id_forwarder_calls),
            "mapped_call_count": len(site_id_forwarder_calls)
                - len(site_id_forwarder_failures),
            "calls_without_matrix_rows":
                site_id_forwarder_failures,
            "calls": site_id_forwarder_calls,
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
        "Direct ad hoc entropy API expressions in Core: "
        f"**{len(report['ambient_entropy_calls'])}**. The scan covers direct "
        "System.Random typed or target-typed construction/shared-instance use and "
        "RandomNumberGenerator APIs; it does not prove the absence of indirect "
        "or aliased entropy sources.",
        *[
            f"- {call['path']}:{call['line']} {call['finding']}"
            for call in report["ambient_entropy_calls"]
        ],
        "",
        "Explicit typed random-request constructions: "
        f"**{len(report['typed_request_constructions'])}**; site IDs resolved "
        f"to registry: **{sum(call['registry_status'] in ('mapped', 'conditional') for call in report['typed_request_constructions'])}**; "
        f"dynamic or missing IDs: **{sum(call['registry_status'] in ('dynamic_expression', 'missing') for call in report['typed_request_constructions'])}**; "
        f"static IDs absent from registry: **{sum(call['registry_status'] in ('not_registered', 'partially_unregistered') for call in report['typed_request_constructions'])}**.",
        "",
        "| File:line | Request type | Site ID expression | Resolved site ID | Registry | Matrix |",
        "| --- | --- | --- | --- | --- | --- |",
        *[
            f"| [{call['path']}:{call['line']}](../{call['path']}#L{call['line']}) "
            f"| {call['request_type']} | {call['site_id_expression'] or '—'} | "
            f"{', '.join(call['site_id_values']) or '—'} | {call['registry_status']} | "
            f"{call['matrix_status']} |"
            for call in report["typed_request_constructions"]
        ],
        "",
        "Shuffle site IDs mapped to migration-matrix rows: "
        f"**{report['shuffle_matrix_mapping_counts']['mapped']}**; dynamic "
        f"expressions: **{report['shuffle_matrix_mapping_counts']['dynamic_expression']}**; "
        f"unmapped: **{report['shuffle_matrix_mapping_counts']['unmapped']}**; "
        f"missing: **{report['shuffle_matrix_mapping_counts']['missing']}**.",
        "Registered stochastic site IDs mapped to migration-matrix rows: "
        f"**{report['registry_matrix_coverage']['mapped_site_count']}/"
        f"{report['registry_matrix_coverage']['registered_site_count']}**; "
        f"IDs without rows: **{len(report['registry_matrix_coverage']['site_ids_without_matrix_rows'])}**; "
        f"duplicate matrix IDs: **{len(report['registry_matrix_coverage']['duplicate_matrix_site_ids'])}**; "
        f"unresolved registry references: **{len(report['registry_matrix_coverage']['unresolved_registry_references'])}**.",
        "Migration-matrix rows absent from the active registry: "
        f"**{len(report['registry_matrix_coverage']['matrix_site_ids_without_registry'])}** "
        "(informational; legacy and conditional rows may remain unregistered). "
        "Their law-status counts are "
        f"UNVERIFIED {report['registry_matrix_coverage']['matrix_rows_without_registry_by_law_status']['unverified']}, "
        f"conditional {report['registry_matrix_coverage']['matrix_rows_without_registry_by_law_status']['conditional']}, "
        f"calibrated {report['registry_matrix_coverage']['matrix_rows_without_registry_by_law_status']['calibrated']}, "
        f"unspecified {report['registry_matrix_coverage']['matrix_rows_without_registry_by_law_status']['unspecified']}.",
        "Matrix-only IDs for manual disposition:",
        "",
        *[
            f"- `{site_id}`"
            for site_id in report['registry_matrix_coverage'][
                'matrix_site_ids_without_registry'
            ]
        ],
        "Reference-law column status: "
        f"**{report['registry_matrix_coverage']['reference_law_status']['unverified_rows']} UNVERIFIED**; "
        f"**{report['registry_matrix_coverage']['reference_law_status']['conditional_rows']} conditional**; "
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
        "--check-site-matrix --check-s0-contract-links "
        "--check-no-ambient-entropy --format markdown > "
        "docs/RNG_DIRECT_CALLSITE_CENSUS.md`.",
        "Rows distinguish legacy prototype calls, native primitive calls, "
        "generic shuffle-helper calls, and recognized provider/fixture support. "
        "Shuffle rows include their `siteId` argument, resolved constant where "
        "available, and exact migration-matrix match for static IDs. The "
        "supplemental table traces site IDs passed through the known event, "
        "combat-pool, and map shuffle helpers. Generic provider adapters and "
        "transcript replay are infrastructure rather than stochastic sites.",
        "",
        "Known RNG helper callsites with forwarded stable site IDs:",
        "",
        f"**{report['site_id_forwarder_coverage']['mapped_call_count']}/"
        f"{report['site_id_forwarder_coverage']['call_count']}** mapped.",
        "",
        "| Helper callsite | Stable site ID expression | Resolved site | Registry | Matrix |",
        "| --- | --- | --- | --- | --- |",
    ]
    for call in report["site_id_forwarder_coverage"]["calls"]:
        callsite = f"{call['path']}:{call['line']}"
        source_link = "../" + call["path"] + "#L" + str(call["line"])
        expression = call["site_id_expression"] or "—"
        resolved = call["site_id_value"] or "—"
        lines.append(
            f"| [`{callsite}`]({source_link}) `{call['helper']}` | "
            f"`{expression}` | `{resolved}` | "
            f"`{call['registry_status']}` | `{call['matrix_status']}` |"
        )
    lines.extend([
        "",
        "| File:line | API | Operation | Stable site ID argument | Matrix row | Source expression | Review status |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ])
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
    parser.add_argument(
        "--check-no-ambient-entropy", action="store_true",
        help="fail if Core directly constructs or consumes ad hoc entropy APIs",
    )
    args = parser.parse_args()
    report = collect()
    if args.check_site_matrix:
        coverage = report["registry_matrix_coverage"]
        if (coverage["registered_site_count"] == 0 or
                coverage["site_ids_without_matrix_rows"] or
                coverage["duplicate_matrix_site_ids"] or
                coverage["unresolved_registry_references"] or
                coverage["reference_law_status"]["rows_without_explicit_status"] or
                report["site_id_forwarder_coverage"][
                    "calls_without_matrix_rows"]):
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
    if args.check_no_ambient_entropy and report["ambient_entropy_calls"]:
        print(json.dumps({
            "ambient_entropy_calls": report["ambient_entropy_calls"],
        }, indent=2, sort_keys=True), file=sys.stderr)
        return 1
    if args.format == "markdown":
        print(render_markdown(report), end="")
    else:
        print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
