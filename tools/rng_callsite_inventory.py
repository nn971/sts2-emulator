#!/usr/bin/env python3
"""List direct emulator RNG and mapped source-adapter calls for S0.

This is a source inventory aid, not a source-to-game completeness proof. It
reports each direct provider expression, high-level MapRandom calls from the
modeled map generators, and direct NativePlayerRngV111,
LegacyRandomChoiceV111, LegacyFisherYatesV111, and
NativeEventContextRngV111 adapter calls with their file and line. It also
traces NativeCombatRngEntryV111 random boundary entry points through their
fixed or forwarded SiteId so manual audits can link them to stable matrix
rows. Other indirect delegates and mechanics hidden behind called helpers
still require manual tracing.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlparse


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "src" / "Sts2Emulator.Core"
RUNTIME_SOURCE_ROOTS = (ROOT / "src", ROOT / "native")
SCHEMA_ID = "emulator-rng-callsite-source-inventory-v58"
SITE_REGISTRY_PATH = SOURCE_ROOT / "StochasticSiteRegistryV111.cs"
NATIVE_RNG_SOURCE_FILES = frozenset({
    "NativeCombatRngEntryV111.cs",
    "NativeEventContextRngV111.cs",
    "NativeFisherYatesV111.cs",
    "NativePlayerRngV111.cs",
    "NativeRandomBoundaryProviderV111.cs",
    "NativeRandomChoiceV111.cs",
    "NativeRngV111.cs",
    "NativeRngV111Contexts.cs",
    "NativeRunRngV111.cs",
})
CHANCE_LAW_DEPENDENCY_PATTERN = re.compile(
    r"\b(?P<symbol>(?:ChanceLaw|StochasticChanceLaw)"
    r"[A-Za-z0-9_]*)\b"
)
TYPE_SOURCE_DECLARATION_PATTERN = re.compile(
    r"\b(?:class|record|struct)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\b"
)
_SOURCE_CLASS_CONTENTS: dict[str, tuple[str, ...]] | None = None

CALL_PATTERN = re.compile(
    r"\b(?P<api>PrototypeRng|NativeRngV111|NativeFisherYatesV111|"
    r"NativePlayerRngV111|LegacyRandomChoiceV111|"
    r"LegacyFisherYatesV111|"
    r"NativeEventContextRngV111|NativeRandomChoiceV111)"
    r"\.(?P<operation>NextInt|NextBool|NextFloat|NextDouble|"
    r"NextGaussianInt|NextGaussianDouble|Shuffle|ShuffleInPlace|Execute|"
    r"NextItem|NextUnitFloat|NextUnitFloatDecision|ChooseIndex|"
    r"ChooseIndexRange|"
    r"ChooseIndexDecision|ChooseBoundedInteger|"
    r"ChooseBoundedIntegerDecision|ResolveIndex|"
    r"ResolveBoundedInteger|ResolveUnitFloat)"
    r"\s*\("
)

MAP_RANDOM_CALL_PATTERN = re.compile(
    r"\brandom\.(?P<operation>NextInt|NextGaussianInt|Shuffle)\s*\("
)
MAP_RANDOM_ADAPTER_FILES = {
    "PrototypeNativeLaterActMap.cs",
    "PrototypeNativeOvergrowthMap.cs",
}

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
    ("system_random_typed_declaration", re.compile(
        r"(?<![A-Za-z0-9_.])(?:(?:global::)?System\.)?Random\??"
        r"(?:\s*\[\s*\])?\s+[A-Za-z_][A-Za-z0-9_]*\b"
    )),
    ("system_random_type_alias", re.compile(
        r"\b(?:global\s+)?using\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*"
        r"(?:(?:global::)?System\.)?Random\b"
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

STRING_CONSTANT_EXPRESSION_PATTERN = re.compile(
    r"\bconst\s+string\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r"(?P<expression>[^;]+);"
)

CLASS_STRING_CONSTANT_PATTERN = re.compile(
    r"(?m)^[ \t]*(?:public|private|internal|protected)\s+"
    r"(?:(?:static|new)\s+)*const\s+string\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r'"(?P<value>[^"\\]*(?:\\.[^"\\]*)*)"\s*;'
)

CLASS_STRING_CONSTANT_EXPRESSION_PATTERN = re.compile(
    r"(?m)^[ \t]*(?:public|private|internal|protected)\s+"
    r"(?:(?:static|new)\s+)*const\s+string\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r"(?P<expression>[^;]+);"
)

STRING_LITERAL_PATTERN = re.compile(
    r'"(?P<value>[^"\\]*(?:\\.[^"\\]*)*)"'
)

ACCESS_MEMBER_START_PATTERN = re.compile(
    r"(?m)^[ \t]*(?:public|private|internal|protected)\s+"
)

SITE_ARRAY_PATTERN = re.compile(
    r"\bstring\[\]\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r"\[(?P<body>.*?)\];",
    re.DOTALL,
)

SITE_NEW_ARRAY_CONCAT_PATTERN = re.compile(
    r"\bstring\[\]\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
    r"new\s*\[\]\s*\{(?P<body>.*?)\}(?P<chain>[^;]*);",
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
    if api in (
        "NativePlayerRngV111", "LegacyRandomChoiceV111",
        "NativeRandomChoiceV111",
    ):
        for argument in arguments:
            named = re.match(r"siteId\s*:\s*(.+)$", argument, re.DOTALL)
            if named:
                return named.group(1).strip()
        return arguments[1] if len(arguments) > 1 else None
    if api == "LegacyFisherYatesV111":
        return arguments[3] if len(arguments) > 3 else None
    if api == "NativeEventContextRngV111":
        position = 4 if operation == "ShuffleInPlace" else 3
        return arguments[position] if len(arguments) > position else None
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


def _map_random_site_id_argument(
    operation: str, arguments: list[str]
) -> str | None:
    """Read the stable site argument at one source-shaped map callsite."""
    position = {
        "NextInt": 1,
        "NextGaussianInt": 4,
        "Shuffle": 1,
    }.get(operation)
    if position is None or len(arguments) <= position:
        return None
    return arguments[position]


def _validated_native_shuffle_site_values(
    sources: dict[Path, str],
    expression: str | None,
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve a forwarded Native shuffle SiteId through the primitive guard."""
    if expression is None or not re.fullmatch(
        r"[A-Za-z_][A-Za-z0-9_]*", re.sub(r"\s+", "", expression)
    ):
        return []
    source = sources.get(SOURCE_ROOT / "NativeFisherYatesV111.cs")
    if source is None or re.search(
        r"\bValidateSiteId\s*\(\s*siteId\s*,\s*"
        r"StochasticSiteRegistryV111\s*\.\s*"
        r"NativeFisherYatesSiteIds\s*,\s*\"shuffle\"\s*\)",
        source,
    ) is None:
        return []
    validator = re.search(
        r"private\s+static\s+void\s+ValidateSiteId\s*\([^)]*\)\s*"
        r"\{(?P<body>.*?)\n\s*\}",
        source,
        re.DOTALL,
    )
    if validator is None or re.search(
        r"if\s*\(\s*!supportedSiteIds\s*\.\s*Contains\s*\(\s*"
        r"siteId\s*,\s*StringComparer\s*\.\s*Ordinal\s*\)\s*\)\s*"
        r"throw\s+new\s+NotSupportedException",
        validator.group("body"),
    ) is None:
        return []
    values, unresolved = _resolve_site_array_property(
        "StochasticSiteRegistryV111.NativeFisherYatesSiteIds",
        global_constants,
    )
    return sorted(values) if values and not unresolved else []


def _validated_shared_shuffle_core_site_values(
    path: Path,
    source: str,
    construction_start: int,
    expression: str | None,
    sources: dict[Path, str],
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve the shared swap request through both finite wrapper guards."""
    if (path.name != "NativeFisherYatesV111.cs" or
            re.sub(r"\s+", "", expression or "") != "siteId" or
            re.search(
                r"\binternal\s+static\s+RandomIndexDecisionV111\[\]\s+"
                r"ShuffleInPlaceCore\s*<",
                _containing_member_source(source, construction_start),
            ) is None):
        return []

    native_path = SOURCE_ROOT / "NativeFisherYatesV111.cs"
    legacy_path = SOURCE_ROOT / "RandomIndexProvidersV111.cs"
    native_source = sources.get(native_path, "")
    legacy_source = sources.get(legacy_path, "")
    core_calls: dict[Path, list[re.Match[str]]] = {}
    for candidate_path, candidate_source in sources.items():
        for match in re.finditer(r"\bShuffleInPlaceCore\s*\(",
                                 candidate_source):
            arguments = _call_arguments(candidate_source, match.start())
            if (len(arguments) > 5 and
                    re.sub(r"\s+", "", arguments[5]) == "siteId"):
                core_calls.setdefault(candidate_path, []).append(match)
    if (set(core_calls) != {native_path, legacy_path} or
            len(core_calls[native_path]) != 1 or
            len(core_calls[legacy_path]) != 1):
        return []

    native_caller = _containing_member_source(
        native_source, core_calls[native_path][0].start()
    )
    if re.search(
        r"ValidateSiteId\s*\(\s*siteId\s*,\s*"
        r"StochasticSiteRegistryV111\s*\.\s*"
        r"NativeFisherYatesSiteIds\s*,\s*\"shuffle\"\s*\)",
        native_caller,
    ) is None:
        return []

    legacy_caller = _containing_member_source(
        legacy_source, core_calls[legacy_path][0].start()
    )
    if re.search(
        r"if\s*\(\s*!\s*StochasticSiteRegistryV111\s*\.\s*"
        r"IsLegacyFisherYatesSite\s*\(\s*siteId\s*\)\s*\)\s*"
        r"throw\s+new\s+NotSupportedException",
        legacy_caller,
    ) is None:
        return []

    registry_source = sources.get(SITE_REGISTRY_PATH, "")
    legacy_set = re.search(
        r"private\s+static\s+readonly\s+string\[\]\s+"
        r"LegacyFisherYatesSites\s*=\s*"
        r"NativeFisherYatesSites\s*\.\s*Concat\s*\(\s*new\[\]\s*"
        r"\{(?P<legacy_only>.*?)\}\s*\)\s*\.\s*Distinct\s*\(\s*"
        r"StringComparer\s*\.\s*Ordinal\s*\)\s*\.\s*ToArray\s*\(\s*\)",
        registry_source,
        re.DOTALL,
    )
    legacy_property = re.search(
        r"public\s+static\s+string\[\]\s+LegacyFisherYatesSiteIds\s*"
        r"=>\s*\(string\[\]\)LegacyFisherYatesSites\.Clone\(\)",
        registry_source,
    )
    native_values = _validated_native_shuffle_site_values(
        sources, "siteId", global_constants
    )
    if legacy_set is None or legacy_property is None or not native_values:
        return []
    legacy_only_values = {
        match.group("value")
        for match in STRING_LITERAL_PATTERN.finditer(
            legacy_set.group("legacy_only")
        )
    }
    values = set(native_values) | legacy_only_values
    return sorted(values) if set(native_values).issubset(values) else []


def _string_constants(source: str) -> dict[str, set[str]]:
    constants: dict[str, set[str]] = {}
    for match in STRING_CONSTANT_PATTERN.finditer(source):
        constants.setdefault(match.group("name"), set()).add(
            match.group("value")
        )
    return constants


def _containing_member_bounds(source: str, offset: int) -> tuple[int, int]:
    declarations = list(ACCESS_MEMBER_START_PATTERN.finditer(
        source, 0, offset
    ))
    if not declarations:
        return 0, len(source)
    start = declarations[-1].start()
    following = ACCESS_MEMBER_START_PATTERN.search(source, offset)
    end = len(source) if following is None else following.start()
    return start, end


def _containing_member_source(source: str, offset: int) -> str:
    start, end = _containing_member_bounds(source, offset)
    return source[start:end]


def _method_string_constants(
    source: str,
    offset: int,
    global_constants: dict[str, set[str]],
) -> dict[str, set[str]]:
    """Resolve const strings declared in the containing member only."""
    member_source = _containing_member_source(source, offset)
    constants: dict[str, set[str]] = {}
    for declaration in CLASS_STRING_CONSTANT_PATTERN.finditer(source):
        constants.setdefault(declaration.group("name"), set()).add(
            declaration.group("value")
        )
    constants.update(_string_constants(member_source))
    declarations = list(
        STRING_CONSTANT_EXPRESSION_PATTERN.finditer(member_source)
    )
    for _ in range(len(declarations) + 1):
        changed = False
        for declaration in declarations:
            name = declaration.group("name")
            values = set(_resolve_site_values(
                declaration.group("expression"), constants,
                global_constants,
            ))
            existing = constants.get(name, set())
            if values and existing != values:
                constants[name] = values
                changed = True
        if not changed:
            break
    return constants


def _method_local_site_values(
    source: str,
    construction_start: int,
    expression: str | None,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
    known_site_ids: set[str],
) -> list[str]:
    """Resolve a single, unreassigned local site expression."""
    if expression is None:
        return []
    identifier = re.fullmatch(
        r"(?:this\.)?(?P<name>[A-Za-z_][A-Za-z0-9_]*)",
        re.sub(r"\s+", "", expression),
    )
    if identifier is None:
        return []
    member_start, member_end = _containing_member_bounds(
        source, construction_start
    )
    member_source = source[member_start:member_end]
    body_start = member_source.find("{")
    if body_start < 0:
        return []
    request_offset = construction_start - member_start
    if request_offset <= body_start:
        return []
    member_source = member_source[body_start + 1:request_offset]
    name = identifier.group("name")
    declaration_pattern = re.compile(
        rf"\b(?:var|string)\s+{re.escape(name)}\s*=\s*(?P<value>[^;]+);"
    )
    declarations = list(declaration_pattern.finditer(member_source))
    if len(declarations) != 1:
        return []
    declaration = declarations[0]
    assignment_pattern = re.compile(
        rf"\b{re.escape(name)}\s*=(?!=)"
    )
    if assignment_pattern.search(
        member_source, declaration.end()
    ):
        return []
    expression = declaration.group("value")
    values = _resolve_site_values(
        expression, local_constants, global_constants
    )
    if not values or any(value not in known_site_ids for value in values):
        values = _static_site_mapping_values(
            expression, global_constants,
            _nearest_type_name(source, construction_start),
        )
    if not values or any(value not in known_site_ids for value in values):
        return []
    return values


def _nearest_type_name(source: str, offset: int) -> str | None:
    """Return the innermost declared type enclosing a source expression.

    This scopes unqualified SiteIdFor* calls to their owning type so
    same-named mapping helpers in other types do not make a finite local
    mapping appear dynamic.
    """
    declarations = list(re.finditer(
        r"(?m)^[ \t]*(?:public|private|internal|protected)\s+"
        r"(?:(?:static|sealed|partial|abstract|readonly|new)\s+)*"
        r"(?:class|struct|record)\s+"
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        source[:offset],
    ))
    for declaration in reversed(declarations):
        opening = source.find("{", declaration.end(), offset)
        semicolon = source.find(";", declaration.end(), offset)
        if opening < 0 or (semicolon >= 0 and semicolon < opening):
            continue
        depth = _brace_depth(source, opening, offset)
        if depth > 0:
            return declaration.group("name")
    return None


def _brace_depth(source: str, opening: int, end: int) -> int:
    """Count block braces while skipping comments and quoted literals."""
    depth = 0
    index = opening
    while index < end:
        character = source[index]
        following = source[index + 1] if index + 1 < end else ""
        if character == "/" and following == "/":
            newline = source.find("\n", index + 2, end)
            index = end if newline < 0 else newline + 1
            continue
        if character == "/" and following == "*":
            close = source.find("*/", index + 2, end)
            index = end if close < 0 else close + 2
            continue
        if character in ('"', "'"):
            quote = character
            verbatim = quote == '"' and index > 0 and (
                source[index - 1] == "@" or
                (index > 1 and source[index - 2:index] == "@$")
            )
            index += 1
            while index < end:
                if verbatim and source[index] == '"' and \
                        index + 1 < end and source[index + 1] == '"':
                    index += 2
                elif source[index] == "\\" and not verbatim:
                    index += 2
                elif source[index] == quote:
                    index += 1
                    break
                else:
                    index += 1
            continue
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
        index += 1
    return depth


def _static_site_mapping_values(
    expression: str,
    global_constants: dict[str, set[str]],
    context_class_name: str | None = None,
) -> list[str]:
    """Resolve a local through a pure SiteIdFor* string switch."""
    normalized = re.sub(r"\s+", "", expression)
    call = re.match(
        r"(?:(?P<class>[A-Za-z_][A-Za-z0-9_]*)\.)?"
        r"(?P<method>SiteIdFor[A-Za-z0-9_]*)\(",
        normalized,
    )
    if call is None:
        return []
    opening = normalized.find("(", call.start())
    depth = 0
    closing = None
    for index in range(opening, len(normalized)):
        if normalized[index] == "(":
            depth += 1
        elif normalized[index] == ")":
            depth -= 1
            if depth == 0:
                closing = index
                break
    if closing is None:
        return []
    suffix = normalized[closing + 1:]
    fail_closed = suffix.startswith("??thrownewNotSupportedException(")
    if suffix and not fail_closed:
        return []

    class_name = call.group("class") or context_class_name
    method_name = call.group("method")
    paths = sorted(SOURCE_ROOT.rglob("*.cs"))
    if class_name is not None:
        preferred = SOURCE_ROOT / f"{class_name}.cs"
        paths = ([preferred] if preferred.is_file() else []) + [
            path for path in paths if path != preferred
        ]
    declaration_pattern = re.compile(
        r"\b(?:public|internal|private|protected)\s+static\s+"
        r"string\??\s+" + re.escape(method_name) +
        r"\s*\([^)]*\)\s*=>\s*(?P<expression>.*?);",
        re.DOTALL,
    )
    candidates: list[tuple[str, re.Match[str]]] = []
    for path in paths:
        source = path.read_text(encoding="utf-8")
        if class_name is not None and not re.search(
            r"\b(?:class|record|struct)\s+" +
            re.escape(class_name) + r"\b", source
        ):
            continue
        candidates.extend(
            (source, declaration)
            for declaration in declaration_pattern.finditer(source)
        )
    if len(candidates) != 1:
        return []
    source, declaration = candidates[0]
    results = _switch_expression_results(
        declaration.group("expression")
    )
    if results is None:
        return []
    mapping_constants = _method_string_constants(
        source, declaration.start(), global_constants
    )
    values: set[str] = set()
    saw_null = False
    for result in results:
        if result.strip() == "null":
            saw_null = True
            continue
        resolved = _resolve_member_site_values(
            source, declaration.start(), result,
            mapping_constants, global_constants,
        )
        if not resolved:
            return []
        values.update(resolved)
    if saw_null and not fail_closed:
        return []
    return sorted(values)


def _resolve_site_value(
    expression: str,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
    active: frozenset[str] = frozenset(),
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
        type_name = class_name.rsplit(".", 1)[-1]
        identity = f"{type_name}.{name}"
        if identity in active:
            return None
        if _SOURCE_CLASS_CONTENTS is None:
            class_file = SOURCE_ROOT / (type_name + ".cs")
            candidate_files = ([class_file] if class_file.is_file() else []) + [
                path for path in sorted(SOURCE_ROOT.rglob("*.cs"))
                if path != class_file
            ]
            class_sources = []
            for candidate in candidate_files:
                source = candidate.read_text(encoding="utf-8")
                if re.search(
                    rf"\b(?:class|record|struct)\s+"
                    rf"{re.escape(type_name)}\b",
                    source,
                ):
                    class_sources.append(source)
        else:
            class_sources = _SOURCE_CLASS_CONTENTS.get(type_name, ())
        for source in class_sources:
            candidates.update(_string_constants(source).get(name, set()))
            for declaration in \
                    CLASS_STRING_CONSTANT_EXPRESSION_PATTERN.finditer(source):
                if (declaration.group("name") != name or
                        _nearest_type_name(source, declaration.start()) !=
                        type_name):
                    continue
                alias = declaration.group("expression").strip()
                if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", alias):
                    alias = f"{type_name}.{alias}"
                candidates.update(_resolve_site_values(
                    alias, {}, global_constants, active | {identity}
                ))
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


def _split_csharp_or_alternatives(expression: str) -> list[str]:
    """Split C# `or` pattern alternatives outside strings and nesting."""
    parts: list[str] = []
    start = 0
    depth = 0
    in_string = False
    escaped = False
    index = 0
    while index < len(expression):
        character = expression[index]
        if in_string:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                in_string = False
            index += 1
            continue
        if character == '"':
            in_string = True
        elif character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
        elif (depth == 0 and expression.startswith("or", index) and
              (index == 0 or expression[index - 1].isspace()) and
              (index + 2 == len(expression) or
               expression[index + 2].isspace())):
            parts.append(expression[start:index].strip())
            start = index + 2
            index += 2
            continue
        index += 1
    parts.append(expression[start:].strip())
    if len(parts) == 1:
        return []
    return parts


def _switch_expression_results(expression: str) -> list[str] | None:
    match = re.search(r"\bswitch\s*\{", expression)
    if match is None:
        return None
    opening = expression.find("{", match.start())
    depth = 0
    in_string = False
    escaped = False
    closing = None
    for index in range(opening, len(expression)):
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
        elif character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                closing = index
                break
    if closing is None or expression[closing + 1:].strip():
        return None
    arms = _split_call_arguments(expression[opening + 1:closing])
    results: list[str] = []
    for arm in arms:
        arrow = arm.find("=>")
        if arrow < 0:
            return None
        results.append(arm[arrow + 2:].strip())
    return results or None


def _resolve_site_values(
    expression: str | None,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
    active: frozenset[str] = frozenset(),
) -> list[str]:
    """Resolve exact or statically visible alternatives in a site expression."""
    if expression is None:
        return []
    switch_results = _switch_expression_results(expression)
    if switch_results is not None:
        values: set[str] = set()
        for result in switch_results:
            branch_values = _resolve_site_values(
                result, local_constants, global_constants, active
            )
            if not branch_values:
                return []
            values.update(branch_values)
        return sorted(values)
    branches = _conditional_site_branches(expression)
    if branches is not None:
        values: set[str] = set()
        for branch in branches:
            values.update(_resolve_site_values(
                branch, local_constants, global_constants, active
            ))
        return sorted(values)
    exact = _resolve_site_value(
        expression, local_constants, global_constants, active
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
            match.group(0), local_constants, global_constants, active
        )
        if value is not None:
            values.add(value)
    return sorted(values)


def _resolve_member_site_values(
    source: str,
    offset: int,
    expression: str | None,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve a bare PascalCase constant in its containing type."""
    if expression is None:
        return []
    identifier = re.fullmatch(
        r"[A-Za-z_][A-Za-z0-9_]*", re.sub(r"\s+", "", expression)
    )
    if identifier is not None:
        name = identifier.group(0)
        type_name = _nearest_type_name(source, offset)
        if type_name is not None and name[0].isupper():
            value = _resolve_site_value(
                f"{type_name}.{name}", {}, global_constants
            )
            if value is not None:
                return [value]
    return _resolve_site_values(
        expression, local_constants, global_constants
    )


def _method_parameters(
    source: str, offset: int
) -> tuple[int, str, list[str], int, int] | None:
    member_start, member_end = _containing_member_bounds(source, offset)
    body_start = source.find("{", member_start, member_end)
    expression_body_start = source.find("=>", member_start, member_end)
    if expression_body_start >= 0 and (
        body_start < 0 or expression_body_start < body_start
    ):
        body_start = expression_body_start
    if body_start < 0:
        return None
    header = source[member_start:body_start].rstrip()
    signature = re.search(
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*"
        r"(?P<parameters>[^()]*)\)\s*$",
        header,
        re.DOTALL,
    )
    if signature is None:
        return None
    parameter_names: list[str] = []
    for parameter in _split_call_arguments(
        signature.group("parameters")
    ):
        parameter = parameter.split("=", 1)[0].strip()
        name = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*$", parameter)
        if name is None:
            return None
        parameter_names.append(name.group(1))
    return (
        member_start, signature.group("name"), parameter_names,
        member_start + signature.start("name"),
        member_start + body_start,
    )


def _split_string_concatenation(expression: str) -> list[str]:
    parts: list[str] = []
    start = 0
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
        elif character == "+" and not any(depth.values()):
            parts.append(expression[start:index].strip())
            start = index + 1
    if not parts:
        return []
    parts.append(expression[start:].strip())
    return parts


def _resolve_forwarded_site_expression(
    sources: dict[Path, str],
    path: Path,
    source: str,
    offset: int,
    expression: str,
    global_constants: dict[str, set[str]],
    active: frozenset[tuple[str, str, int, int]] = frozenset(),
) -> list[str]:
    expression = expression.strip()
    local_constants = _method_string_constants(
        source, offset, global_constants
    )
    branches = _conditional_site_branches(expression)
    if branches is not None:
        values: set[str] = set()
        for branch in branches:
            resolved = _resolve_forwarded_site_expression(
                sources, path, source, offset, branch,
                global_constants, active,
            )
            if not resolved:
                return []
            values.update(resolved)
        return sorted(values)

    interpolation = re.fullmatch(r'\$"(?P<body>(?:[^"\\]|\\.)*)"',
                                 expression, re.DOTALL)
    if interpolation is not None:
        values = {""}
        body = interpolation.group("body")
        tokens = re.split(r"(\{[A-Za-z_][A-Za-z0-9_]*\})", body)
        for token in tokens:
            if not token:
                continue
            if token.startswith("{") and token.endswith("}"):
                parameter = token[1:-1]
                options = _resolve_forwarded_site_expression(
                    sources, path, source, offset, parameter,
                    global_constants, active,
                )
                if not options:
                    return []
                values = {
                    prefix + option
                    for prefix in values for option in options
                }
            else:
                values = {prefix + token for prefix in values}
        return sorted(values)

    concatenation = _split_string_concatenation(expression)
    if concatenation:
        values = {""}
        for part in concatenation:
            options = _resolve_forwarded_site_expression(
                sources, path, source, offset, part,
                global_constants, active,
            )
            if not options:
                return []
            values = {
                prefix + option
                for prefix in values for option in options
            }
        return sorted(values)

    direct = _resolve_member_site_values(
        source, offset, expression, local_constants, global_constants
    )
    if direct:
        return direct

    identifier = re.fullmatch(
        r"[A-Za-z_][A-Za-z0-9_]*", re.sub(r"\s+", "", expression)
    )
    if identifier is None:
        return []
    name = identifier.group(0)
    member_start, member_end = _containing_member_bounds(source, offset)
    member_prefix = source[member_start:offset]
    local_declarations = list(re.finditer(
        rf"\b(?:var|string)\s+{re.escape(name)}\s*=\s*(?P<value>[^;]+);",
        member_prefix,
    ))
    if local_declarations:
        if len(local_declarations) != 1:
            return []
        declaration = local_declarations[0]
        declaration_end = member_start + declaration.end()
        remainder = source[declaration_end:member_end]
        if re.search(rf"\b{re.escape(name)}\s*=(?!=)", remainder):
            return []
        return _resolve_forwarded_site_expression(
            sources, path, source,
            member_start + declaration.start("value"),
            declaration.group("value"), global_constants, active,
        )

    signature = _method_parameters(source, offset)
    if signature is None:
        return []
    (member_start, method_name, parameter_names, declaration_name,
     declaration_body) = signature
    if name not in parameter_names:
        return []
    parameter_index = parameter_names.index(name)
    identity = (
        str(path), method_name, len(parameter_names), parameter_index
    )
    if identity in active:
        return []
    method_type = _nearest_type_name(source, offset)
    candidate_sources = sources.items()
    if method_type != "PrototypeGameEngine":
        candidate_sources = ((path, source),)
    call_pattern = re.compile(
        rf"\b{re.escape(method_name)}\s*\("
    )
    values: set[str] = set()
    saw_call = False
    for caller_path, caller_source in candidate_sources:
        for call in call_pattern.finditer(caller_source):
            caller_member_start, caller_member_end = \
                _containing_member_bounds(caller_source, call.start())
            caller_body_start = caller_source.find(
                "{", caller_member_start, caller_member_end
            )
            caller_expression_body_start = caller_source.find(
                "=>", caller_member_start, caller_member_end
            )
            if caller_expression_body_start >= 0 and (
                caller_body_start < 0 or
                caller_expression_body_start < caller_body_start
            ):
                caller_body_start = caller_expression_body_start
            if caller_body_start >= 0 and call.start() < caller_body_start:
                continue
            if (method_type == "PrototypeGameEngine" and
                    _nearest_type_name(caller_source, call.start()) !=
                    method_type):
                continue
            arguments = _call_arguments(caller_source, call.start())
            if len(arguments) != len(parameter_names):
                continue
            argument = arguments[parameter_index].strip()
            named = re.match(r"[A-Za-z_][A-Za-z0-9_]*\s*:\s*(.*)$",
                             argument, re.DOTALL)
            if named is not None:
                argument = named.group(1).strip()
            resolved = _resolve_forwarded_site_expression(
                sources, caller_path, caller_source, call.start(), argument,
                global_constants, active | {identity},
            )
            if not resolved:
                return []
            saw_call = True
            values.update(resolved)
    return sorted(values) if saw_call else []


def _dictionary_site_values(
    source: str,
    dictionary_name: str,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> list[str]:
    declaration = re.search(
        rf"\b{re.escape(dictionary_name)}\s*=\s*new\s*"
        r"(?:\([^)]*\))?\s*\{(?P<body>.*?)\n\s*\};",
        source,
        re.DOTALL,
    )
    if declaration is None:
        return []
    entries = list(re.finditer(
        r"\[(?P<key>[^\]]+)\]\s*=\s*new\s*\(",
        declaration.group("body"),
    ))
    if not entries:
        return []
    values: list[str] = []
    for entry in entries:
        value = _resolve_site_value(
            entry.group("key"), local_constants, global_constants
        )
        if value is None:
            return []
        values.append(value)
    return sorted(set(values))


def _dictionary_member_site_values(
    source: str,
    dictionary_name: str,
    member_index: int,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> dict[str, list[str]]:
    """Resolve a string member of each string-keyed constructor entry."""
    declaration = re.search(
        rf"\b{re.escape(dictionary_name)}\s*=\s*new\s*"
        r"(?:\([^)]*\))?\s*\{(?P<body>.*?)\n\s*\};",
        source,
        re.DOTALL,
    )
    if declaration is None:
        return {}
    body = declaration.group("body")
    entries = list(re.finditer(
        r"\[(?P<key>[^\]]+)\]\s*=\s*new\s*\(", body
    ))
    resolved: dict[str, list[str]] = {}
    for entry in entries:
        site_id = _resolve_site_value(
            entry.group("key"), local_constants, global_constants
        )
        arguments = _call_arguments(body, entry.start())
        if site_id is None or len(arguments) <= member_index:
            return {}
        member_values = _resolve_site_values(
            arguments[member_index], local_constants, global_constants
        )
        if not member_values:
            return {}
        resolved[site_id] = member_values
    return resolved


def _forwarded_site_array_values(
    expression: str,
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve one source-owned site-array property forwarding hop."""
    reference = re.sub(r"\s+", "", expression)
    match = re.fullmatch(
        r"(?P<class>[A-Za-z_][A-Za-z0-9_]*)\."
        r"(?P<property>[A-Za-z_][A-Za-z0-9_]*)",
        reference,
    )
    if match is None:
        return []

    candidate_files = sorted(SOURCE_ROOT.rglob("*.cs"))
    for candidate in candidate_files:
        source = candidate.read_text(encoding="utf-8")
        if not re.search(
            rf"\b(?:class|record|struct)\s+"
            rf"{re.escape(match.group('class'))}\b",
            source,
        ):
            continue
        body = next((item.group("body")
            for item in SITE_ARRAY_PROPERTY_PATTERN.finditer(source)
            if item.group("name") == match.group("property")), None)
        if body is None:
            continue
        forwarded = re.fullmatch(
            r"\s*(?P<target>[A-Za-z_][A-Za-z0-9_]*"
            r"(?:\.[A-Za-z_][A-Za-z0-9_]*)+)\s*",
            body,
        )
        if forwarded is None:
            return []
        values, unresolved = _resolve_site_array_property(
            forwarded.group("target"), global_constants,
        )
        return sorted(values) if values and not unresolved else []
    return []


def _guarded_site_values(
    source: str,
    construction_start: int,
    expression: str | None,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve identifier SiteIds bounded by a fail-closed method check.

    Negative pattern guards and explicit supported-site branches are followed
    only when the method rejects unsupported IDs with a NotSupportedException
    naming the same identifier. This avoids guessing from nearby literals or
    following arbitrary caller data.
    """
    if expression is None:
        return []
    identifier = re.fullmatch(
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*"
        r"(?:\.[A-Za-z_][A-Za-z0-9_]*)?)",
        re.sub(r"\s+", "", expression),
    )
    if identifier is None:
        return []
    declarations = list(ACCESS_MEMBER_START_PATTERN.finditer(
        source, 0, construction_start
    ))
    if not declarations:
        return []
    member_start = declarations[-1].start()
    next_declaration = ACCESS_MEMBER_START_PATTERN.search(
        source, construction_start
    )
    member_end = (len(source) if next_declaration is None
                  else next_declaration.start())
    member_source = source[member_start:member_end]
    name = identifier.group("name")
    direct_guard_values = _direct_fail_closed_site_values(
        member_source, name, local_constants, global_constants
    )
    if direct_guard_values:
        return direct_guard_values
    if ("NotSupportedException" not in member_source or
            not re.search(rf"\{{[^{{}}]*\b{re.escape(name)}\b[^{{}}]*\}}|"
                          rf"nameof\(\s*{re.escape(name)}\s*\)",
                          member_source)):
        registered_query_guard = re.search(
            rf"\bValidateSiteId\s*\(\s*{re.escape(name)}\s*,\s*"
            r"\"(?:index|bounded-integer|unit-float)\"\s*\)",
            member_source,
        )
        if registered_query_guard is not None:
            operation = re.search(
                r"ValidateSiteId\s*\(\s*"
                rf"{re.escape(name)}\s*,\s*"
                r"\"(?P<operation>index|bounded-integer|unit-float)\"\s*\)",
                member_source,
            )
            validator = re.search(
                r"private\s+static\s+void\s+ValidateSiteId\s*\(\s*"
                r"string\s+(?P<parameter>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*"
                r"string\s+[A-Za-z_][A-Za-z0-9_]*\s*\)\s*"
                r"\{(?P<body>.*?)\n\s*\}",
                source,
                re.DOTALL,
            )
            if validator is None:
                return []
            parameter = validator.group("parameter")
            body = validator.group("body")
            if (operation is None or "NotSupportedException" not in body or
                    not re.search(
                        rf"\{{[^{{}}]*\b{re.escape(parameter)}\b"
                        rf"[^{{}}]*\}}", body)):
                return []
            query_values, query_unresolved = _resolve_site_array_property(
                "StochasticSiteRegistryV111.ChanceLawQuerySiteIds",
                global_constants,
            )
            if not query_values or query_unresolved:
                return []
            query_guard = re.search(
                rf"StochasticSiteRegistryV111\."
                rf"IsChanceLawQuerySite\s*\(\s*"
                rf"{re.escape(parameter)}\s*\)", body,
            )
            family = operation.group("operation")
            if family in {"index", "bounded-integer"}:
                family_guard = re.search(
                    rf"StochasticSiteRegistryV111\s*\.\s*"
                    rf"IsNativeRandomIndexSite\s*\(\s*"
                    rf"{re.escape(parameter)}\s*\)", body,
                )
                bounded_values, bounded_unresolved = (
                    _resolve_site_array_property(
                        "StochasticSiteRegistryV111.NativeBoundedIntegerSiteIds",
                        global_constants,
                    )
                )
                bounded_guard = re.search(
                    rf"StochasticSiteRegistryV111\s*\.\s*"
                    rf"IsNativeBoundedIntegerSite\s*\(\s*"
                    rf"{re.escape(parameter)}\s*\)", body,
                )
                float_values, float_unresolved = _resolve_site_array_property(
                    "StochasticSiteRegistryV111.NativeUnitFloatSiteIds",
                    global_constants,
                )
                gaussian_values, gaussian_unresolved = (
                    _resolve_site_array_property(
                        "ChanceLawEngineV111.NativeMapGaussianIntQuerySiteIds",
                        global_constants,
                    )
                )
                if (query_guard is None or family_guard is None or
                        bounded_guard is None or bounded_unresolved or
                        float_unresolved or gaussian_unresolved):
                    return []
                rejected = set(float_values) | set(gaussian_values)
                if family == "index":
                    return sorted(
                        set(query_values) - rejected - set(bounded_values)
                    )
                return sorted(set(query_values) & set(bounded_values))
            family_guard = re.search(
                rf"StochasticSiteRegistryV111\s*\.\s*"
                rf"IsNativeUnitFloatSite\s*\(\s*"
                rf"{re.escape(parameter)}\s*\)", body,
            )
            float_values, float_unresolved = _resolve_site_array_property(
                "StochasticSiteRegistryV111.NativeUnitFloatSiteIds",
                global_constants,
            )
            if (query_guard is None or family_guard is None or
                    not float_values or float_unresolved):
                return []
            return sorted(set(query_values) & set(float_values))

        delegated_guard = re.search(
            rf"\bValidateSiteId\s*\(\s*{re.escape(name)}\s*,\s*"
            r"(?P<sites>[A-Za-z_][A-Za-z0-9_.]*)\s*,",
            member_source,
        )
        if delegated_guard is None:
            return []
        validator = re.search(
            r"private\s+static\s+void\s+ValidateSiteId\s*\(\s*"
            r"string\s+(?P<parameter>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*"
            r"string\[\]\s+(?P<sites_parameter>"
            r"[A-Za-z_][A-Za-z0-9_]*)\s*,\s*"
            r"string\s+[A-Za-z_][A-Za-z0-9_]*\s*\)\s*"
            r"\{(?P<body>.*?)\n\s*\}",
            source,
            re.DOTALL,
        )
        if validator is None:
            return []
        parameter = validator.group("parameter")
        sites_parameter = validator.group("sites_parameter")
        body = validator.group("body")
        if ("NotSupportedException" not in body or
                not re.search(
                    rf"{re.escape(sites_parameter)}\.Contains\s*\(\s*"
                    rf"{re.escape(parameter)}\s*,\s*"
                    r"StringComparer\.Ordinal\s*\)", body) or
                not re.search(
                    rf"\{{[^{{}}]*\b{re.escape(parameter)}\b[^{{}}]*\}}",
                    body,
                )):
            return []
        site_array = delegated_guard.group("sites")
        if not re.fullmatch(
            r"StochasticSiteRegistryV111\.Native[A-Za-z0-9]*SiteIds",
            site_array,
        ):
            return []
        values, unresolved = _resolve_site_array_property(
            site_array, global_constants,
        )
        return sorted(set(values)) if values and not unresolved else []
    finite_array_guard = re.search(
        rf"if\s*\(\s*!\s*(?P<sites>"
        r"[A-Za-z_][A-Za-z0-9_.]*)\.Contains\s*\(\s*"
        rf"{re.escape(name)}\s*,\s*StringComparer\.Ordinal\s*"
        r"\)\s*\)\s*throw\s+new\s+NotSupportedException",
        member_source,
    )
    if finite_array_guard is not None:
        site_array = finite_array_guard.group("sites")
        values, unresolved = _resolve_site_array_property(
            site_array, global_constants,
        )
        if not values or unresolved:
            values = _forwarded_site_array_values(
                site_array, global_constants,
            )
            unresolved = [] if values else [site_array]
        return sorted(values) if values and not unresolved else []

    legacy_choice_guard = re.search(
        rf"if\s*\(\s*!\s*StochasticSiteRegistryV111\s*\.\s*"
        rf"IsLegacyRandomChoiceSite\s*\(\s*{re.escape(name)}\s*\)\s*"
        r"\)\s*throw\s+new\s+NotSupportedException",
        member_source,
    )
    if legacy_choice_guard:
        registry_source = SITE_REGISTRY_PATH.read_text(encoding="utf-8")
        registry_method = re.search(
            r"public\s+static\s+bool\s+IsLegacyRandomChoiceSite\s*\(\s*"
            r"string\s+(?P<parameter>[A-Za-z_][A-Za-z0-9_]*)\s*\)\s*"
            r"=>\s*(?P<body>.*?);",
            registry_source,
            re.DOTALL,
        )
        if registry_method is None:
            return []


        parameter = registry_method.group("parameter")
        method_body = re.sub(r"\s+", "", registry_method.group("body"))
        method_prefix = (
            f"IsChanceLawQuerySite({parameter})||"
            f"NatureChoiceSiteCandidates.Contains({parameter},"
            f"StringComparer.Ordinal)||{parameter}is"
        )
        if not method_body.startswith(method_prefix):
            return []
        component_alternatives = method_body[len(method_prefix):]
        if re.fullmatch(
                r'"[^"\\]+"(?:or"[^"\\]+")*',
                component_alternatives) is None:
            return []
        component_values = [
            literal.group("value")
            for literal in STRING_LITERAL_PATTERN.finditer(
                component_alternatives
            )
        ]
        if len(component_values) != 4 or any(
                not value.endswith(".component")
                for value in component_values):
            return []
        component_parent_sites = [
            value.removesuffix(".component") for value in component_values
        ]
        query_values, query_unresolved = _resolve_site_array_property(
            "StochasticSiteRegistryV111.ChanceLawQuerySiteIds",
            global_constants,
        )
        if not query_values or query_unresolved:
            return []
        site_array = next((array for array in
            SITE_ARRAY_PATTERN.finditer(registry_source)
            if array.group("name") == "NatureChoiceSiteCandidates"), None)
        if site_array is None:
            return []
        values, unresolved = _resolve_site_array_items(
            site_array.group("body"), global_constants,
            frozenset({
                "StochasticSiteRegistryV111.NatureChoiceSiteCandidates"
            }),
            registry_source, "StochasticSiteRegistryV111",
        )
        return (sorted(set(query_values) | set(values) |
                       set(component_parent_sites))
                if values and not unresolved else [])
    dictionary_lookup = re.search(
        rf"\b(?P<dictionary>[A-Za-z_][A-Za-z0-9_]*)\."
        rf"(?:TryGetValue\s*\(\s*{re.escape(name)}\s*,|"
        rf"ContainsKey\s*\(\s*{re.escape(name)}\s*\))",
        member_source,
    )
    if dictionary_lookup:
        values = _dictionary_site_values(
            source, dictionary_lookup.group("dictionary"),
            local_constants, global_constants,
        )
        if values:
            return values
    site_array_lookup = re.search(
        rf"\b(?P<array>[A-Za-z_][A-Za-z0-9_]*"
        rf"(?:\.[A-Za-z_][A-Za-z0-9_]*)+)\.Contains\s*\(\s*"
        rf"{re.escape(name)}\s*,\s*StringComparer\.Ordinal\s*\)",
        member_source,
    )
    if site_array_lookup:
        values, unresolved = _resolve_site_array_property(
            site_array_lookup.group("array"), global_constants
        )
        if values and not unresolved:
            return sorted(set(values))
    guard_pattern = re.compile(
        rf"\b{re.escape(name)}\s+is\s+not\s*\((?P<alternatives>[^()]*)\)"
    )
    guards = list(guard_pattern.finditer(member_source))
    if guards:
        guard_expression = guards[-1].group("alternatives")
        alternatives = (_split_csharp_or_alternatives(guard_expression)
                       or [guard_expression.strip()])
        return sorted({
            value
            for alternative in alternatives
            for value in _resolve_site_values(
                alternative, local_constants, global_constants
            )
        })

    # A chain of explicit site comparisons followed by a fail-closed
    # NotSupportedException also bounds the accepted IDs. Only use this
    # pattern when the exception message names the same identifier.
    values: set[str] = set()
    site_token = r'(?:"[^"\\]*(?:\\.[^"\\]*)*"|' \
        r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)"
    alternatives = re.compile(
        rf"\b{re.escape(name)}\s+is\s+(?!not\b)(?P<alternatives>"
        rf"\([^()]*\)|{site_token}(?:\s+or\s+{site_token})*)"
    )
    for match in alternatives.finditer(member_source):
        expression = match.group("alternatives").strip()
        if expression.startswith("(") and expression.endswith(")"):
            expression = expression[1:-1]
        parts = _split_csharp_or_alternatives(expression)
        values.update(
            value for part in (parts or [expression])
            for value in _resolve_site_values(
                part, local_constants, global_constants
            )
        )
    equality = re.compile(
        rf"\b{re.escape(name)}\s*==\s*(?P<value>\"[^\"]+\"|"
        r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)"
    )
    for match in equality.finditer(member_source):
        values.update(_resolve_site_values(
            match.group("value"), local_constants, global_constants
        ))
    return sorted(values)


def _direct_fail_closed_site_values(
    member_source: str,
    name: str,
    local_constants: dict[str, set[str]],
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Resolve one SiteId equality enforced by a throwing condition."""
    site_expression = (
        r"(?:[A-Za-z_][A-Za-z0-9_]*"
        r"(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)*|"
        r'"(?:[^"\\]|\\.)*")'
    )
    comparison = re.compile(
        rf"{re.escape(name)}\s*!=\s*(?P<site>{site_expression})"
    )
    conditions: list[str] = []
    for match in re.finditer(r"\bif\s*\(", member_source):
        opening = member_source.find("(", match.start())
        depth = 0
        in_string = False
        escaped = False
        closing = None
        for index in range(opening, len(member_source)):
            character = member_source[index]
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
                    closing = index
                    break
        if closing is None or not re.match(
            r"\s*throw\s+new\s+NotSupportedException\b",
            member_source[closing + 1:],
        ):
            continue
        condition = member_source[opening + 1:closing]
        parts: list[str] = []
        start = 0
        depth = 0
        in_string = False
        escaped = False
        index = 0
        while index < len(condition):
            character = condition[index]
            if in_string:
                if escaped:
                    escaped = False
                elif character == "\\":
                    escaped = True
                elif character == '"':
                    in_string = False
                index += 1
                continue
            if character == '"':
                in_string = True
            elif character == "(":
                depth += 1
            elif character == ")":
                depth -= 1
            elif depth == 0 and condition.startswith("||", index):
                parts.append(condition[start:index].strip())
                start = index + 2
                index += 2
                continue
            index += 1
        parts.append(condition[start:].strip())
        if not all(part and "&&" not in part for part in parts):
            continue
        matches = [comparison.fullmatch(part) for part in parts]
        site_matches = [item for item in matches if item is not None]
        if len(site_matches) != 1:
            continue
        resolved = _resolve_site_values(
            site_matches[0].group("site"),
            local_constants, global_constants,
        )
        if resolved:
            conditions.extend(resolved)
    return sorted(set(conditions))


def _validated_semantic_event_site_values(
    path: Path,
    source: str,
    construction_start: int,
    expression: str | None,
    sources: dict[Path, str],
    global_constants: dict[str, set[str]],
) -> list[str]:
    """Trace finite SiteId domains through validated semantic event helpers."""
    if (path.name != "SemanticOtherChanceV111.cs" or
            re.sub(r"\s+", "", expression or "") != "pending.SiteId"):
        return []
    member = _containing_member_source(source, construction_start)
    signature = member.split("{", 1)[0]
    target_source = sources.get(SOURCE_ROOT / "SemanticTargetChoiceV111.cs", "")

    if (re.search(r"\bSemanticTargetEventV111\s+pending\b", signature) and
            "pending.Validate()" in member and
            "SiteId != expectedSiteId" in target_source and
            "expectedSiteId is null" in target_source and
            "SiteIdForTargetKind(TargetKind)" in target_source):
        resolver = re.search(
            r"internal\s+static\s+string\?\s+SiteIdForTargetKind\s*"
            r"\([^)]*\)\s*=>\s*(?P<expression>.*?);",
            target_source,
            re.DOTALL,
        )
        if resolver is not None:
            results = _switch_expression_results(
                resolver.group("expression")
            )
            if results:
                values: set[str] = set()
                for result in results:
                    if result.strip() == "null":
                        continue
                    resolved = _resolve_site_values(
                        result, {}, global_constants
                    )
                    if not resolved:
                        return []
                    values.update(resolved)
                if values:
                    return sorted(values)

    if re.search(r"\bSemanticCardShortlistEventV111\s+pending\b", signature):
        fail_closed = re.search(
            r"if\s*\(\s*!SupportsEngineOwnedCardShortlistBoundary\s*"
            r"\(\s*pending\s*\)\s*\)\s*"
            r"throw\s+new\s+NotSupportedException",
            member,
        )
        helper_start = re.search(
            r"internal\s+static\s+bool\s+"
            r"SupportsEngineOwnedCardShortlistBoundary\s*\(",
            source,
        )
        if fail_closed is not None and helper_start is not None:
            helper = _containing_member_source(source, helper_start.start())
            pattern = re.search(
                r"pending\.SiteId\s+is\s*\((?P<sites>[^()]*)\)",
                helper,
            )
            if pattern is not None:
                alternatives = _split_csharp_or_alternatives(
                    pattern.group("sites")
                )
                resolved = [
                    value
                    for alternative in alternatives
                    for value in _resolve_site_values(
                        alternative, {}, global_constants
                    )
                ]
                if alternatives and resolved:
                    return sorted(set(resolved))

    if re.search(r"\bSemanticPoolShuffleEventV111\s+pending\b", signature):
        fail_closed = re.search(
            r"!TryGetNativePoolShuffleStreamId\s*\(\s*"
            r"pending\.SiteId\s*,",
            member,
        )
        helper_start = re.search(
            r"private\s+static\s+bool\s+"
            r"TryGetNativePoolShuffleStreamId\s*\(\s*"
            r"string\s+(?P<site_parameter>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*"
            r"out\s+string\s+(?P<stream_parameter>"
            r"[A-Za-z_][A-Za-z0-9_]*)\s*\)",
            source,
        )
        if (fail_closed is not None and helper_start is not None and
                "NotSupportedException" in member):
            helper = _containing_member_source(source, helper_start.start())
            site_parameter = helper_start.group("site_parameter")
            stream_parameter = helper_start.group("stream_parameter")
            switch = re.search(
                rf"\b{re.escape(stream_parameter)}\s*=\s*"
                rf"{re.escape(site_parameter)}\s+switch\s*"
                r"\{(?P<arms>.*?)\}",
                helper,
                re.DOTALL,
            )
            if switch is not None and re.search(
                rf"return\s+{re.escape(stream_parameter)}\.Length\s*!=\s*0\s*;",
                helper,
            ):
                values: set[str] = set()
                valid = True
                for arm in _split_call_arguments(switch.group("arms")):
                    arrow = arm.find("=>")
                    if arrow < 0:
                        valid = False
                        break
                    pattern = arm[:arrow].strip()
                    outcome = arm[arrow + 2:].strip()
                    if pattern == "_":
                        valid = valid and outcome == '""'
                        continue
                    streams = _resolve_site_values(
                        outcome, {}, global_constants
                    )
                    alternatives = _split_csharp_or_alternatives(pattern)
                    if not streams or any(not stream for stream in streams):
                        valid = False
                        break
                    for alternative in alternatives or [pattern]:
                        sites = _resolve_site_values(
                            alternative, {}, global_constants
                        )
                        if not sites:
                            valid = False
                            break
                        values.update(sites)
                    if not valid:
                        break
                if valid and values:
                    return sorted(values)
    return []


def _resolve_site_array_property(
    expression: str,
    global_constants: dict[str, set[str]],
    active: frozenset[str] = frozenset(),
    class_context: str | None = None,
    class_name_context: str | None = None,
) -> tuple[list[str], list[str]]:
    """Resolve spreads of source-owned properties and local string arrays.

    Registry properties can compose existing public site lists or private
    arrays without copying their stable IDs. Follow those references for the
    census as well.
    """
    reference = re.sub(r"\s+", "", expression)
    if reference.startswith(".."):
        reference = reference[2:].strip()
    match = re.fullmatch(
        r"(?P<class>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\."
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)",
        reference,
    )
    if match is None:
        local = re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", reference)
        if local is not None and class_context is not None \
                and class_name_context is not None:
            array_name = local.group(0)
            identity = f"{class_name_context}.{array_name}"
            if identity in active:
                return [], [expression.strip()]
            array = next((item for item in SITE_ARRAY_PATTERN.finditer(
                class_context
            ) if item.group("name") == array_name), None)
            if array is not None:
                return _resolve_site_array_items(
                    array.group("body"), global_constants,
                    active | {identity}, class_context,
                    class_name_context,
                )
            property_body = next((item.group("body")
                for item in SITE_ARRAY_PROPERTY_PATTERN.finditer(
                    class_context
                ) if item.group("name") == array_name), None)
            if property_body is not None:
                forwarded_property = re.fullmatch(
                    r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+",
                    property_body.strip(),
                )
                if forwarded_property is not None:
                    return _resolve_site_array_property(
                        property_body.strip(), global_constants,
                        active | {identity}, class_context,
                        class_name_context,
                    )
                keys = re.search(
                    r"\b(?P<dictionary>[A-Za-z_][A-Za-z0-9_]*)\.Keys\b",
                    property_body,
                )
                if keys is not None:
                    values = _dictionary_site_values(
                        class_context, keys.group("dictionary"),
                        _string_constants(class_context), global_constants,
                    )
                    if values:
                        return values, []
        return [], [expression.strip()]
    class_name = match.group("class").rsplit(".", 1)[-1]
    property_name = match.group("name")
    identity = f"{class_name}.{property_name}"
    if identity in active:
        return [], [expression.strip()]
    if (class_name, property_name) == (
            "StochasticSiteRegistryV111", "ChanceLawQuerySiteIds"):
        registry_source = SITE_REGISTRY_PATH.read_text(encoding="utf-8")
        query_builder = re.search(
            r"private\s+static\s+string\[\]\s+"
            r"BuildChanceLawQuerySites\s*\(\)\s*=>\s*"
            r"\[(?P<body>.*?)\];",
            registry_source,
            re.DOTALL,
        )
        if query_builder is None:
            return [], [expression.strip()]
        return _resolve_site_array_items(
            query_builder.group("body"), global_constants,
            active | {identity}, registry_source,
            "StochasticSiteRegistryV111",
        )
    class_file = SOURCE_ROOT / f"{class_name}.cs"
    candidate_files = (
        [class_file] if class_file.is_file()
        else sorted(SOURCE_ROOT.rglob("*.cs"))
    )
    property_body = None
    class_source = None
    for candidate in candidate_files:
        source = candidate.read_text(encoding="utf-8")
        if not re.search(
                rf"\b(?:class|record|struct)\s+"
                rf"{re.escape(class_name)}\b", source):
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
        if array is not None:
            property_body = array.group("body")
        else:
            concatenated = next((item for item in
                SITE_NEW_ARRAY_CONCAT_PATTERN.finditer(class_source)
                if item.group("name") == array_name), None)
            if concatenated is None:
                return [], [expression.strip()]
            identity = f"{class_name}.{array_name}"
            values, unresolved = _resolve_site_array_items(
                concatenated.group("body"), global_constants,
                next_active | {identity}, class_source, class_name,
            )
            concat = re.search(
                r"\.Concat\(\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
                concatenated.group("chain"),
            )
            if concat is not None:
                nested, missing = _resolve_site_array_property(
                    concat.group("name"), global_constants,
                    next_active | {identity}, class_source, class_name,
                )
                values.extend(nested)
                unresolved.extend(missing)
            return list(dict.fromkeys(values)), unresolved
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

    return _resolve_site_array_items(
        property_body, global_constants, next_active,
        class_source, class_name,
    )


def _resolve_site_array_items(
    body: str,
    global_constants: dict[str, set[str]],
    active: frozenset[str],
    class_source: str,
    class_name: str,
) -> tuple[list[str], list[str]]:
    values: list[str] = []
    unresolved: list[str] = []
    for item in _split_call_arguments(body):
        if item.startswith(".."):
            nested, missing = _resolve_site_array_property(
                item, global_constants, active, class_source, class_name
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


def _markdown_heading_context(text: str, fragment: str) -> str | None:
    """Return a linked heading section, without searching unrelated sections."""
    target = unquote(fragment).lstrip("#").lower()
    headings: list[tuple[int, int, int, str]] = []
    for match in re.finditer(
        r"^(?P<marks>#{1,6})\s+(?P<title>.+?)\s*#*\s*$",
        text,
        re.MULTILINE,
    ):
        title = match.group("title").strip()
        explicit_id = re.search(r"\s+\{#([^}]+)\}$", title)
        if explicit_id:
            slug = explicit_id.group(1).lower()
            title = title[:explicit_id.start()].strip()
        else:
            slug = re.sub(r"[^\w -]", "", title.lower())
            slug = re.sub(r"\s+", "-", slug.strip())
        headings.append((len(match.group("marks")), match.start(),
                         match.end(), slug))

    for index, (level, start, heading_end, slug) in enumerate(headings):
        if slug != target:
            continue
        end = len(text)
        for next_level, next_start, _, _ in headings[index + 1:]:
            if next_level <= level:
                end = next_start
                break
        return text[start:end]
    return None


def _site_link_context(
    text: str, site_id: str, fragment: str
) -> str:
    if fragment:
        section = _markdown_heading_context(text, fragment)
        if section is not None:
            return section

    contexts: list[str] = []
    for block in re.split(r"\n\s*\n", text):
        if site_id not in block:
            continue
        table_rows = [
            line for line in block.splitlines()
            if line.lstrip().startswith("|") and site_id in line
        ]
        contexts.append("\n".join(table_rows) if table_rows else block)
    return "\n".join(contexts)


def _site_matrix_contract_evidence(
    site_rows: list[dict[str, Any]],
) -> dict[str, Any]:
    """Locate site-specific S0 evidence without treating matches as proof."""
    facet_patterns = {
        "private_constraint": re.compile(
            r"constraint|fingerprint|eligible|eligibility|ordered pool|filter|support",
            re.IGNORECASE,
        ),
        "audience_projection": re.compile(
            r"player.?visible|player.?view|public.?view|observation|"
            r"nature.?view|nature.?facing|nature.?choice|"
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
        site_specific_docs: list[str] = []
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
            text = doc_text[relative]
            context = _site_link_context(
                text, row["site_id"], parsed.fragment
            )
            if context:
                site_specific_docs.append(relative)
                linked_text.append(context)

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
                facet_patterns["unsupported_handling"].search(row_text)
                or any(facet_patterns["unsupported_handling"].search(doc) for doc in linked_text)
            ),
        }
        missing = [facet for facet, present in evidence.items() if not present]
        for facet in missing:
            missing_by_facet[facet].append(row["site_id"])
        audit_rows.append({
            "site_id": row["site_id"],
            "linked_contract_docs": sorted(set(linked_docs)),
            "site_specific_contract_docs": sorted(set(site_specific_docs)),
            "evidence_presence": evidence,
            "missing_facets": missing,
        })

    return {
        "method": (
            "Keyword locator for manual S0 review. Matches come from the row "
            "or a linked heading section or line tied to that site ID; generic "
            "document-wide matches are excluded. A match only identifies a "
            "place to inspect and does not prove the contract is complete."
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
                    expression, global_constants,
                    class_context=registry_source,
                    class_name_context="StochasticSiteRegistryV111",
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

    query_site_ids, query_unresolved = _resolve_site_array_property(
        "StochasticSiteRegistryV111.ChanceLawQuerySiteIds",
        global_constants,
    )
    site_ids.update(query_site_ids)
    unresolved.extend(query_unresolved)
    return site_ids, sorted(set(unresolved))


def _site_id_forwarder_calls(
    sources: dict[Path, str],
    global_constants: dict[str, set[str]],
    registry_site_ids: set[str],
    matrix_site_ids: set[str],
) -> list[dict[str, Any]]:
    """Trace site arguments at known RNG-helper call boundaries.

    Generic RNG wrappers receive their semantic site from the owning
    mechanics call. Keep this supplemental to the direct RNG-call census: the
    helper callsites are where the stable site is chosen, while provider and
    transcript wrappers are not sites.
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
            "callee": "PrototypeGameEngine.ResolveRandomCombatCardIndex",
            "pattern": re.compile(
                r"\bResolveRandomCombatCardIndex\s*\("
            ),
            "site_argument": 2,
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
        {
            "callee": "NativeCombatRngEntryV111.AttachAndRollInitialHp",
            "pattern": re.compile(
                r"\bNativeCombatRngEntryV111\.AttachAndRollInitialHp\s*\("
            ),
            "site_argument": None,
            "fixed_site_expression":
                "NativeCombatRngEntryV111.InitialUniqueHpSiteId",
            "paths": None,
            "exclude_paths": {"NativeCombatRngEntryV111.cs"},
        },
        {
            "callee": "NativeCombatRngEntryV111.HatchToughEgg",
            "pattern": re.compile(
                r"\bNativeCombatRngEntryV111\.HatchToughEgg\s*\("
            ),
            "site_argument": None,
            "fixed_site_expression":
                "NativeCombatRngEntryV111.ToughEggHatchHpSiteId",
            "paths": None,
            "exclude_paths": {"NativeCombatRngEntryV111.cs"},
        },
        {
            "callee": "NativeCombatRngEntryV111.AppendSummonedEnemy",
            "pattern": re.compile(
                r"\bNativeCombatRngEntryV111\.AppendSummonedEnemy\s*\("
            ),
            "site_argument": None,
            "fixed_site_expression":
                "NativeCombatRngEntryV111.InitialUniqueHpSiteId",
            "paths": None,
            "exclude_paths": {"NativeCombatRngEntryV111.cs"},
        },
        {
            "callee": "NativeCombatRngEntryV111.AdvanceMonsterLocal",
            "pattern": re.compile(
                r"\bNativeCombatRngEntryV111\.AdvanceMonsterLocal\s*\("
            ),
            "site_argument": 2,
            "paths": None,
            "exclude_paths": {"NativeCombatRngEntryV111.cs"},
        },
        {
            "callee": "NativePlayerRngV111.ChooseIndex",
            "pattern": re.compile(
                r"\bNativePlayerRngV111\.ChooseIndex\s*\("
            ),
            "site_argument": 1,
            "paths": None,
            "exclude_paths": {"NativePlayerRngV111.cs"},
        },
        {
            "callee": "NativePlayerRngV111.ChooseBoundedInteger",
            "pattern": re.compile(
                r"\bNativePlayerRngV111\.ChooseBoundedInteger\s*\("
            ),
            "site_argument": 1,
            "paths": None,
            "exclude_paths": {"NativePlayerRngV111.cs"},
        },
        {
            "callee": "NativePlayerRngV111.NextUnitFloat",
            "pattern": re.compile(
                r"\bNativePlayerRngV111\.NextUnitFloat\s*\("
            ),
            "site_argument": 1,
            "paths": None,
            "exclude_paths": {"NativePlayerRngV111.cs"},
        },
        {
            "callee": "NativeRandomChoiceV111.ChooseIndexRange",
            "pattern": re.compile(
                r"\bNativeRandomChoiceV111\.ChooseIndexRange\s*\("
            ),
            "site_argument": 1,
            "paths": None,
            "exclude_paths": {"NativePlayerRngV111.cs"},
        },
        {
            "callee": "NativeRandomChoiceV111.ChooseBoundedInteger",
            "pattern": re.compile(
                r"\bNativeRandomChoiceV111\.ChooseBoundedInteger\s*\("
            ),
            "site_argument": 1,
            "paths": None,
            "exclude_paths": {"NativePlayerRngV111.cs"},
        },
        {
            "callee": "NativeRandomChoiceV111.NextUnitFloat",
            "pattern": re.compile(
                r"\bNativeRandomChoiceV111\.NextUnitFloat\s*\("
            ),
            "site_argument": 1,
            "paths": None,
            "exclude_paths": {"NativePlayerRngV111.cs"},
        },
    )
    calls: list[dict[str, Any]] = []
    for path, source in sources.items():
        for specification in specifications:
            allowed_paths = specification["paths"]
            if allowed_paths is not None and path.name not in allowed_paths:
                continue
            if path.name in specification.get("exclude_paths", set()):
                continue
            for match in specification["pattern"].finditer(source):
                arguments = _call_arguments(source, match.start())
                if specification["site_argument"] is None:
                    expression = specification["fixed_site_expression"]
                    values = _resolve_member_site_values(
                        source, match.start(), expression,
                        _method_string_constants(
                            source, match.start(), global_constants
                        ), global_constants,
                    )
                    value = values[0] if len(values) == 1 else None
                else:
                    position = specification["site_argument"]
                    if len(arguments) <= position:
                        expression = None
                        value = None
                    else:
                        expression = arguments[position]
                        if expression.strip() == "string siteId":
                            continue
                        values = _resolve_member_site_values(
                            source, match.start(), expression,
                            _method_string_constants(
                                source, match.start(), global_constants
                            ), global_constants,
                        )
                        value = values[0] if len(values) == 1 else None
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


def _simulation_boss_law_forward_values(
    path: Path,
    source: str,
    construction_start: int,
    global_constants: dict[str, set[str]],
) -> tuple[list[str], list[str]]:
    """Trace the comparison request through its two source boss-law branches."""
    if path.name != "SimulationProviderReplayComparisonV111.cs":
        return [], []

    member = _containing_member_source(source, construction_start)
    boss_declaration = re.search(
        r"\bvar\s+bossLaw\s*=\s*(?P<expression>.*?);", member,
        re.DOTALL,
    )
    if (boss_declaration is None or
            "bossLaw is null" not in member):
        return [], []
    branch_calls = re.findall(
        r"\bChanceLawEngineV111\.(Query\w+BossEncounter)\s*\(",
        boss_declaration.group("expression"),
    )
    query_names = [
        "QueryOvergrowthBossEncounter",
        "QueryUnderdocksBossEncounter",
    ]
    if branch_calls != query_names:
        return [], []

    law_source = (SOURCE_ROOT / "ChanceLawV111.cs").read_text(
        encoding="utf-8"
    )
    wrapper_sites: set[str] = set()
    for query_name in query_names:
        wrapper = re.search(
            rf"public\s+static\s+ChanceLawV111\s+"
            rf"{re.escape(query_name)}\s*\([^)]*\)\s*=>\s*"
            r"QueryActOneBossEncounter\s*\((?P<arguments>.*?)\)\s*;",
            law_source,
            re.DOTALL,
        )
        if wrapper is None:
            return [], []
        arguments = _split_call_arguments(wrapper.group("arguments"))
        values = _resolve_site_values(
            arguments[0] if arguments else None, {}, global_constants
        )
        if len(values) != 1:
            return [], []
        wrapper_sites.add(values[0])

    helper_start = re.search(
        r"private\s+static\s+ChanceLawV111\s+"
        r"QueryActOneBossEncounter\s*\(", law_source,
    )
    if helper_start is None:
        return [], []
    helper = _containing_member_source(law_source, helper_start.end())
    site_switch = re.search(
        r"\bvar\s+\(bossPool\s*,\s*ownerPrefix\)\s*=\s*"
        r"siteId\s+switch\s*\{(?P<arms>.*?)\n\s*\};",
        helper,
        re.DOTALL,
    )
    if site_switch is None:
        return [], []
    supported_sites: set[str] = set()
    saw_fail_closed_default = False
    for arm in _split_call_arguments(site_switch.group("arms")):
        arrow = arm.find("=>")
        if arrow < 0:
            return [], []
        pattern = arm[:arrow].strip()
        result = arm[arrow + 2:]
        if pattern == "_":
            if "throw new NotSupportedException" not in result:
                return [], []
            saw_fail_closed_default = True
            continue
        values = _resolve_site_values(pattern, {}, global_constants)
        if len(values) != 1:
            return [], []
        supported_sites.add(values[0])
    if not saw_fail_closed_default or supported_sites != wrapper_sites:
        return [], []

    create_call = re.search(r"\breturn\s+Create\s*\(", helper)
    if create_call is None:
        return [], []
    create_arguments = _call_arguments(helper, create_call.start())
    operation_values = (
        _resolve_site_values(
            create_arguments[2], {}, global_constants
        )
        if len(create_arguments) > 2 else []
    )
    if len(operation_values) != 1:
        return [], []
    return sorted(wrapper_sites), operation_values


def _ambient_entropy_calls(
    sources: dict[Path, str],
) -> list[dict[str, Any]]:
    """Find direct or explicitly typed ad hoc entropy in runtime C# sources."""
    calls: list[dict[str, Any]] = []
    for path, source in sources.items():
        code_source = _mask_csharp_comments_and_literals(source)
        for api, pattern in AMBIENT_ENTROPY_PATTERNS:
            scan_source = (
                code_source if api in {
                    "system_random_typed_declaration",
                    "system_random_type_alias",
                } else source
            )
            for match in pattern.finditer(scan_source):
                line_number = source.count("\n", 0, match.start()) + 1
                arguments = _call_arguments(scan_source, match.start())
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


def _native_chance_law_dependency_references(
    sources: dict[Path, str],
) -> list[dict[str, Any]]:
    """Find direct Chance Law references in Native RNG and native mod code."""
    references: list[dict[str, Any]] = []
    native_root = ROOT / "native"
    for path, source in sources.items():
        is_native_rng_source = (
            path.parent == SOURCE_ROOT and (
                path.name in NATIVE_RNG_SOURCE_FILES or
                path.name.startswith("Random") and
                "ProvidersV111" in path.name
            )
        )
        if not is_native_rng_source and not path.is_relative_to(native_root):
            continue
        code_source = _mask_csharp_comments_and_literals(source)
        for match in CHANCE_LAW_DEPENDENCY_PATTERN.finditer(code_source):
            line_number = source.count("\n", 0, match.start()) + 1
            references.append({
                "path": path.relative_to(ROOT).as_posix(),
                "line": line_number,
                "symbol": match.group("symbol"),
                "source_excerpt": source.splitlines()[
                    line_number - 1
                ].strip(),
            })
    return sorted(references, key=lambda call: (call["path"], call["line"]))


def _mask_csharp_comments_and_literals(source: str) -> str:
    """Replace C# comments and literals with spaces while preserving lines."""
    masked = list(source)
    index = 0

    def blank(start: int, end: int) -> None:
        for position in range(start, end):
            if masked[position] not in "\r\n":
                masked[position] = " "

    while index < len(source):
        if source.startswith("//", index):
            end = source.find("\n", index + 2)
            if end < 0:
                end = len(source)
            blank(index, end)
            index = end
            continue
        if source.startswith("/*", index):
            end = source.find("*/", index + 2)
            end = len(source) if end < 0 else end + 2
            blank(index, end)
            index = end
            continue

        if source[index] not in "\"'":
            index += 1
            continue

        quote = source[index]
        if quote == "\"" and source.startswith('"""', index):
            delimiter_end = index
            while delimiter_end < len(source) and source[delimiter_end] == '"':
                delimiter_end += 1
            delimiter = source[index:delimiter_end]
            close = source.find(delimiter, delimiter_end)
            end = len(source) if close < 0 else close + len(delimiter)
            blank(index, end)
            index = end
            continue

        verbatim = quote == "\"" and index > 0 and source[index - 1] == "@"
        cursor = index + 1
        while cursor < len(source):
            if verbatim and source.startswith('""', cursor):
                cursor += 2
                continue
            if source[cursor] == quote:
                cursor += 1
                break
            if not verbatim and source[cursor] == "\\":
                cursor += 2
            else:
                cursor += 1
        blank(index, cursor)
        index = cursor

    return "".join(masked)


def _matching_csharp_delimiter(
    source: str, opening: int, left: str, right: str,
) -> int | None:
    depth = 0
    for position in range(opening, len(source)):
        if source[position] == left:
            depth += 1
        elif source[position] == right:
            depth -= 1
            if depth == 0:
                return position
    return None


def _csharp_expression_body_end(source: str, start: int) -> int | None:
    """Find an expression-bodied member's top-level terminating semicolon."""
    closing_to_opening = {")": "(", "]": "[", "}": "{"}
    opening_delimiters = set(closing_to_opening.values())
    stack: list[str] = []
    for position in range(start, len(source)):
        character = source[position]
        if character in opening_delimiters:
            stack.append(character)
        elif character in closing_to_opening:
            if stack and stack[-1] == closing_to_opening[character]:
                stack.pop()
        elif character == ";" and not stack:
            return position
    return None


def _chance_law_engine_methods(
    sources: dict[Path, str],
) -> list[dict[str, Any]]:
    """Extract static method bodies declared on ChanceLawEngine partials."""
    declaration = re.compile(
        r"^[ \t]*(?:public|private|internal|protected)\s+static\s+"
        r"(?:(?:async|unsafe|extern|new)\s+)*"
        r"[A-Za-z_][A-Za-z0-9_?.<>,\[\]()\s]*?\s+"
        r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        re.MULTILINE,
    )
    methods: list[dict[str, Any]] = []
    for path, source in sources.items():
        if not path.name.startswith("ChanceLaw") or not path.name.endswith(
            "V111.cs"
        ):
            continue
        code = _mask_csharp_comments_and_literals(source)
        class_match = re.search(
            r"\bpublic\s+static\s+partial\s+class\s+"
            r"ChanceLawEngineV111\b",
            code,
        )
        if class_match is None:
            continue
        class_opening = code.find("{", class_match.end())
        class_closing = _matching_csharp_delimiter(
            code, class_opening, "{", "}"
        )
        if class_opening < 0 or class_closing is None:
            continue
        class_body_start = class_opening + 1
        class_body = code[class_body_start:class_closing]
        for match in declaration.finditer(class_body):
            opening_paren = class_body.find("(", match.start("name"))
            closing_paren = _matching_csharp_delimiter(
                class_body, opening_paren, "(", ")"
            )
            if closing_paren is None:
                continue
            cursor = closing_paren + 1
            while cursor < len(class_body):
                while cursor < len(class_body) and class_body[cursor].isspace():
                    cursor += 1
                if cursor >= len(class_body):
                    body = ""
                    body_offset = cursor
                    break
                if class_body.startswith("=>", cursor):
                    body_start = cursor + 2
                    body_end = _csharp_expression_body_end(
                        class_body, body_start
                    )
                    if body_end is None:
                        body = ""
                        body_offset = body_start
                    else:
                        body = class_body[body_start:body_end]
                        body_offset = body_start
                    break
                if class_body[cursor] == "{":
                    body_start = cursor
                    body_end = _matching_csharp_delimiter(
                        class_body, body_start, "{", "}"
                    )
                    if body_end is None:
                        body = ""
                    else:
                        body = class_body[body_start:body_end + 1]
                    body_offset = body_start
                    break
                if class_body[cursor] == ";":
                    body = ""
                    body_offset = cursor
                    break
                cursor += 1
            else:
                continue

            absolute_name = class_body_start + match.start("name")
            absolute_body = class_body_start + body_offset
            methods.append({
                "path": path.relative_to(ROOT).as_posix(),
                "line": source.count("\n", 0, absolute_name) + 1,
                "body_line": source.count("\n", 0, absolute_body) + 1,
                "name": match.group("name"),
                "body": body,
                "source": source,
            })
    return methods


def _chance_law_outcome_blind_violations(
    sources: dict[Path, str],
) -> dict[str, Any]:
    """Follow ChanceLawEngine helper calls and find forbidden query effects."""
    patterns = (
        ("direct_random_sampling", re.compile(
            r"\b(?:PrototypeRng|NativeRngV111|NativeFisherYatesV111|"
            r"NativePlayerRngV111|NativeRandomChoiceV111|"
            r"NativeEventContextRngV111)\s*\.\s*"
            r"(?:Next\w*|Sample\w*|Shuffle\w*|Execute|"
            r"Resolve\w*|Choose\w*)\s*\("
        )),
        ("native_cursor_snapshot", re.compile(
            r"\bNativeRngV111\s*\.\s*ToSnapshot\s*\("
        )),
        ("provider_cursor_read", re.compile(
            r"\b[A-Za-z_]\w*\s*\.\s*(?:CallCount|Counter)\b"
        )),
        ("random_boundary_journal", re.compile(
            r"\bRandomBoundaryJournalV111\b"
        )),
        ("native_event_probe", re.compile(
            r"\bNativeEventRandomProbeV111\b"
        )),
        ("controlled_replay_capture", re.compile(
            r"\b(?:ControlledChanceContinuationExecutorV111|"
            r"ControlledStochasticChanceReplayV111)\s*\.\s*"
            r"(?:Capture\w*|Resolve\w*)\s*\("
        )),
        ("engine_step", re.compile(
            r"\b(?:PrototypeGameEngine|engine)\s*"
            r"(?:\([^)]*\))?\s*\.\s*Step\s*\("
        )),
    )
    methods = _chance_law_engine_methods(sources)
    query_methods = [
        method for method in methods if method["name"].startswith("Query")
    ]
    methods_by_name: dict[str, list[dict[str, Any]]] = {}
    methods_by_id: dict[tuple[str, int, str], dict[str, Any]] = {}
    for method in methods:
        method_id = (method["path"], method["line"], method["name"])
        methods_by_name.setdefault(method["name"], []).append(method)
        methods_by_id[method_id] = method

    call_pattern = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(")
    violations: list[dict[str, Any]] = []
    reachable_method_ids: set[tuple[str, int, str]] = set()
    for query in query_methods:
        query_id = (query["path"], query["line"], query["name"])
        pending = [(query_id, (query["name"],))]
        visited: set[tuple[str, int, str]] = set()
        while pending:
            method_id, call_chain = pending.pop()
            if method_id in visited:
                continue
            visited.add(method_id)
            reachable_method_ids.add(method_id)
            method = methods_by_id[method_id]
            for finding, pattern in patterns:
                for match in pattern.finditer(method["body"]):
                    line = method["body_line"] + method["body"].count(
                        "\n", 0, match.start()
                    )
                    excerpt = method["source"].splitlines()[line - 1].strip()
                    violations.append({
                        "path": method["path"],
                        "line": line,
                        "entry_method": query["name"],
                        "method": method["name"],
                        "call_chain": list(call_chain),
                        "finding": finding,
                        "source_excerpt": excerpt,
                    })

            called_names = list(dict.fromkeys(
                match.group(1)
                for match in call_pattern.finditer(method["body"])
                if match.group(1) in methods_by_name
            ))
            for called_name in reversed(called_names):
                for helper in reversed(methods_by_name[called_name]):
                    helper_id = (
                        helper["path"], helper["line"], helper["name"]
                    )
                    if helper_id not in visited:
                        pending.append((
                            helper_id, call_chain + (helper["name"],)
                        ))
    return {
        "query_method_count": len(query_methods),
        "engine_method_count": len(methods),
        "reachable_method_count": len(reachable_method_ids),
        "unparsed_query_methods": [
            {
                "path": method["path"],
                "line": method["line"],
                "method": method["name"],
            }
            for method in query_methods if not method["body"].strip()
        ],
        "violations": sorted(
            violations, key=lambda finding: (
                finding["entry_method"], finding["path"],
                finding["line"], finding["finding"]
            )
        ),
    }


def _typed_request_constructions(
    sources: dict[Path, str],
    global_constants: dict[str, set[str]],
    registry_site_ids: set[str],
    matrix_site_ids: set[str],
    site_id_forwarder_calls: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Inventory explicit typed random-request construction boundaries."""
    calls: list[dict[str, Any]] = []
    registry_source = sources.get(SITE_REGISTRY_PATH, "")
    legacy_caller_bound_operations = _dictionary_member_site_values(
        registry_source, "LegacyCallerBoundIndexQuerySites", 1,
        {}, global_constants,
    )
    for path, source in sources.items():
        for match in REQUEST_CONSTRUCTION_PATTERN.finditer(source):
            local_constants = _method_string_constants(
                source, match.start(), global_constants
            )
            arguments = _call_arguments(source, match.start())
            boss_law_site_values, boss_law_operation_values = (
                _simulation_boss_law_forward_values(
                    path, source, match.start(), global_constants,
                )
            )
            operation_expression = None
            for argument in arguments:
                named_operation = re.match(
                    r"OperationKind\s*:\s*(.+)$", argument, re.DOTALL
                )
                if named_operation:
                    operation_expression = named_operation.group(1).strip()
                    break
            if (match.group("request_type") == "RandomIndexRequestV111" and
                    operation_expression is None):
                operation_values = ["choose_index"]
                operation_resolution = "implicit_default"
            elif operation_expression is None:
                operation_values = []
                operation_resolution = "not_applicable"
            else:
                operation_values = _resolve_site_values(
                    operation_expression, local_constants, global_constants
                )
                operation_resolution = (
                    "unresolved" if not operation_values else
                    "expression" if len(operation_values) == 1 else
                    "finite_alternatives"
                )
            if (not operation_values and
                    re.sub(r"\s+", "", operation_expression or "") ==
                    "bossLaw.OperationKind" and
                    boss_law_operation_values):
                operation_values = boss_law_operation_values
                operation_resolution = "method_local_assignment"
            expression = None
            for argument in arguments:
                named_site = re.match(
                    r"SiteId\s*:\s*(.+)$", argument, re.DOTALL
                )
                if named_site:
                    expression = named_site.group(1).strip()
                    break
            else:
                expression = arguments[0] if arguments else None
            values = _resolve_member_site_values(
                source, match.start(), expression,
                local_constants, global_constants,
            )
            resolution_source = "expression" if values else None
            if not values:
                values = _method_local_site_values(
                    source, match.start(), expression,
                    local_constants, global_constants,
                    registry_site_ids | matrix_site_ids,
                )
                if values:
                    resolution_source = "method_local_assignment"
            if (not values and
                    re.sub(r"\s+", "", expression or "") ==
                    "bossLaw.SiteId" and boss_law_site_values):
                values = boss_law_site_values
                resolution_source = "method_local_assignment"
            if not values:
                member_source = _containing_member_source(
                    source, match.start()
                )
                normalized_member_source = re.sub(
                    r"\s+", "", member_source
                )
                normalized_site_expression = re.sub(
                    r"\s+", "", expression or ""
                )
                if (normalized_site_expression and
                        f"TryGetLegacyCallerBoundIndexQuerySite("
                        f"{normalized_site_expression}," in
                        normalized_member_source and
                        "thrownewNotSupportedException" in
                        normalized_member_source):
                    values = sorted(legacy_caller_bound_operations)
                    if values:
                        resolution_source = "registry_dictionary_guard"
            if not values:
                values = _guarded_site_values(
                    source, match.start(), expression,
                    local_constants, global_constants,
                )
                if values:
                    member_source = _containing_member_source(
                        source, match.start()
                    )
                    resolution_source = (
                        "legacy_random_choice_site_guard"
                        if "IsLegacyRandomChoiceSite" in member_source
                        else "method_fail_closed_validation"
                    )
            if not values:
                values = _validated_shared_shuffle_core_site_values(
                    path, source, match.start(), expression,
                    sources, global_constants,
                )
                if values:
                    resolution_source = (
                        "shared_shuffle_core_finite_registry"
                    )
            if not values:
                values = _validated_semantic_event_site_values(
                    path, source, match.start(), expression,
                    sources, global_constants,
                )
                if values:
                    resolution_source = (
                        "validated_semantic_event_site_domain"
                    )
            if not values and path.name == "PrototypeCombat.cs":
                member_source = _containing_member_source(
                    source, match.start()
                )
                normalized_member_source = re.sub(
                    r"\s+", "", member_source
                )
                if (re.sub(r"\s+", "", expression or "") ==
                        "siteId" and
                        "ResolveRandomCombatCardIndex(" in
                        normalized_member_source):
                    core_callers = [
                        call for call in site_id_forwarder_calls
                        if call["helper"] ==
                            "PrototypeGameEngine.ResolveRandomCombatCardIndex"
                    ]
                    if (core_callers and
                            all(call["registry_status"] == "mapped" and
                                call["matrix_status"] == "mapped" and
                                call["site_id_value"] is not None
                                for call in core_callers)):
                        values = sorted({
                            call["site_id_value"] for call in core_callers
                        })
                        resolution_source = (
                            "finite_core_helper_callsite_closure"
                        )
            if not values and path.name == "NativeRandomChoiceV111.cs":
                helper_names: tuple[str, ...] = ()
                request_type = match.group("request_type")
                operation_kind = operation_values[0] if len(
                    operation_values) == 1 else None
                if (request_type == "RandomIndexRequestV111" and
                        operation_kind == "choose_index"):
                    helper_names = (
                        "NativePlayerRngV111.ChooseIndex",
                        "NativeRandomChoiceV111.ChooseIndexRange",
                    )
                elif (request_type == "RandomIndexRequestV111" and
                        operation_kind == "bounded_integer"):
                    helper_names = (
                        "NativePlayerRngV111.ChooseBoundedInteger",
                        "NativeRandomChoiceV111.ChooseBoundedInteger",
                    )
                elif request_type == "RandomUnitFloatRequestV111":
                    helper_names = (
                        "NativePlayerRngV111.NextUnitFloat",
                        "NativeRandomChoiceV111.NextUnitFloat",
                    )
                core_callers = [
                    call for call in site_id_forwarder_calls
                    if call["helper"] in helper_names
                ]
                if (helper_names and core_callers and
                        all(call["registry_status"] == "mapped" and
                            call["matrix_status"] == "mapped" and
                            call["site_id_value"] is not None
                            for call in core_callers)):
                    values = sorted({
                        call["site_id_value"] for call in core_callers
                    })
                    resolution_source = (
                        "finite_core_helper_callsite_closure"
                    )
                if (not values and request_type ==
                        "RandomIndexRequestV111" and
                        operation_kind == "bounded_integer"):
                    member_source = _containing_member_source(
                        source, match.start()
                    )
                    if "request.ValidateForNativeProvider()" in member_source:
                        values, unresolved = _resolve_site_array_property(
                            "StochasticSiteRegistryV111.NativeBoundedIndexOperationSiteIds",
                            global_constants,
                        )
                        if (values and not unresolved and
                                all(item in registry_site_ids and
                                    item in matrix_site_ids
                                    for item in values)):
                            resolution_source = (
                                "native_operation_registry_validation"
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
            normalized_expression = re.sub(r"\s+", "", expression or "")
            if values:
                origin_kind = (
                    "guarded_legacy_random_choice_site_set"
                    if resolution_source == "legacy_random_choice_site_guard"
                    else "guarded_finite_site_id_alternatives"
                    if resolution_source in (
                        "method_fail_closed_validation",
                        "registry_dictionary_guard",
                        "shared_shuffle_core_finite_registry",
                        "validated_semantic_event_site_domain",
                        "native_operation_registry_validation",
                    )
                    else "finite_core_helper_callsite_sites"
                    if resolution_source ==
                        "finite_core_helper_callsite_closure"
                    else "static_site_id" if len(values) == 1
                    else "finite_site_id_alternatives"
                )
            elif expression is None:
                origin_kind = "missing_site_id"
            elif re.fullmatch(
                r"(?:this\.)?[A-Za-z_][A-Za-z0-9_]*",
                normalized_expression,
            ):
                origin_kind = "identifier_forwarded_or_local"
            elif re.fullmatch(
                r"(?:this\.)?[A-Za-z_][A-Za-z0-9_]*"
                r"(?:\.[A-Za-z_][A-Za-z0-9_]*)+",
                normalized_expression,
            ):
                origin_kind = "member_forwarded"
            else:
                origin_kind = "computed_expression"
            line_number = source.count("\n", 0, match.start()) + 1
            calls.append({
                "path": path.relative_to(ROOT).as_posix(),
                "line": line_number,
                "request_type": match.group("request_type"),
                "operation_kind_expression": operation_expression,
                "operation_kind_values": operation_values,
                "operation_kind_resolution": operation_resolution,
                "site_id_expression": expression,
                "site_id_value": value,
                "site_id_values": values,
                "site_id_origin_kind": origin_kind,
                "site_id_resolution_source": resolution_source,
                "registry_status": registry_status,
                "matrix_status": matrix_status,
                "operation_kind_by_site": {},
                "source_excerpt": source.splitlines()[
                    line_number - 1
                ].strip(),
            })
            call = calls[-1]
            normalized_operation = re.sub(
                r"\s+", "", operation_expression or ""
            )
            member_source = _containing_member_source(source, match.start())
            if (normalized_operation == "site.OperationKind" and
                    "TryGetLegacyCallerBoundIndexQuerySite" in member_source):
                operation_by_site = {
                    site_id: legacy_caller_bound_operations[site_id]
                    for site_id in values
                    if site_id in legacy_caller_bound_operations
                }
                if values and len(operation_by_site) == len(values):
                    call["operation_kind_by_site"] = operation_by_site
                    call["operation_kind_values"] = sorted({
                        operation
                        for operations in operation_by_site.values()
                        for operation in operations
                    })
                    call["operation_kind_resolution"] = (
                        "site_keyed_dictionary"
                    )
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
    global _SOURCE_CLASS_CONTENTS
    source_class_contents: dict[str, list[str]] = {}
    for source in sources.values():
        for type_name in {
            declaration.group("name")
            for declaration in TYPE_SOURCE_DECLARATION_PATTERN.finditer(source)
        }:
            source_class_contents.setdefault(type_name, []).append(source)
    _SOURCE_CLASS_CONTENTS = {
        type_name: tuple(class_sources)
        for type_name, class_sources in source_class_contents.items()
    }
    runtime_sources = {
        path: path.read_text(encoding="utf-8")
        for source_root in RUNTIME_SOURCE_ROOTS
        for path in sorted(source_root.rglob("*.cs"))
    }
    ambient_entropy_calls = _ambient_entropy_calls(runtime_sources)
    native_chance_law_dependency_references = (
        _native_chance_law_dependency_references(runtime_sources)
    )
    chance_law_outcome_blind_contract = (
        _chance_law_outcome_blind_violations(sources)
    )
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
        sources, string_constants, registry_site_ids, matrix_site_ids,
        site_id_forwarder_calls,
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
                    "NativePlayerRngV111": "native_player_rng_adapter",
                    "LegacyRandomChoiceV111": "legacy_random_choice_adapter",
                    "LegacyFisherYatesV111": "legacy_fisher_yates_adapter",
                    "NativeEventContextRngV111": "native_event_context_adapter",
                    "NativeRandomChoiceV111": "native_random_choice_adapter",
                }[api]
                filename = path.name
                if (api == "NativeRandomChoiceV111" and
                        filename == "NativePlayerRngV111.cs"):
                    continue
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
                elif api == "NativePlayerRngV111":
                    review_status = "native_player_rng_adapter_callsite"
                elif api == "LegacyRandomChoiceV111":
                    review_status = "legacy_random_choice_adapter_callsite"
                elif api == "NativeEventContextRngV111":
                    review_status = "native_event_context_adapter_callsite"
                elif api == "LegacyFisherYatesV111":
                    review_status = "legacy_fisher_yates_adapter_callsite"
                elif api == "NativeRandomChoiceV111":
                    review_status = "native_random_choice_adapter_callsite"
                else:
                    review_status = "native_primitive_callsite_needs_site_review"
                arguments = _call_arguments(
                    source, source_offset + match.start()
                )
                site_expression = _site_id_argument(
                    api, match.group("operation"), arguments
                )
                site_value = None
                site_values: list[str] = []
                guarded_shuffle_site_set = False
                if site_expression is not None:
                    site_values = _resolve_member_site_values(
                        source, source_offset + match.start(),
                        site_expression,
                        _method_string_constants(
                            source, source_offset + match.start(),
                            string_constants,
                        ),
                        string_constants,
                    )
                    if api == "LegacyRandomChoiceV111":
                        forwarded_values = _resolve_forwarded_site_expression(
                            sources, path, source,
                            source_offset + match.start(), site_expression,
                            string_constants,
                        )
                        if forwarded_values:
                            site_values = forwarded_values
                    if api == "LegacyFisherYatesV111" and not site_values:
                        forwarded_values = _resolve_forwarded_site_expression(
                            sources, path, source,
                            source_offset + match.start(), site_expression,
                            string_constants,
                        )
                        if forwarded_values:
                            site_values = forwarded_values
                    if not site_values:
                        site_values = _guarded_site_values(
                            source, source_offset + match.start(),
                            site_expression,
                            _method_string_constants(
                                source, source_offset + match.start(),
                                string_constants,
                            ),
                            string_constants,
                        )
                    if api == "NativeFisherYatesV111" and not site_values:
                        site_values = _validated_native_shuffle_site_values(
                            sources, site_expression, string_constants,
                        )
                        guarded_shuffle_site_set = bool(site_values)
                    site_value = (
                        site_values[0] if len(site_values) == 1 else None
                    )
                matrix_site_values = [
                    value[:-len(".component")]
                    if api == "LegacyRandomChoiceV111" and
                        value.endswith(".component") and
                        value[:-len(".component")] in matrix_site_ids
                    else value
                    for value in site_values
                ]
                gaussian_component_parent_mapping = (
                    matrix_site_values != site_values
                )
                if api in (
                    "NativeFisherYatesV111", "NativePlayerRngV111",
                    "LegacyRandomChoiceV111", "NativeEventContextRngV111",
                    "LegacyFisherYatesV111", "NativeRandomChoiceV111",
                ):
                    if site_expression is None:
                        review_status = "site_id_argument_missing"
                    elif site_values:
                        review_status = (
                            "shuffle_site_id_guarded_by_finite_registry_set"
                            if guarded_shuffle_site_set and
                                api == "NativeFisherYatesV111" else
                            "shuffle_site_id_resolved"
                            if api == "NativeFisherYatesV111" else
                            "native_player_rng_site_id_resolved"
                            if api == "NativePlayerRngV111" else
                            "native_event_context_site_id_resolved"
                            if api == "NativeEventContextRngV111" else
                            "legacy_fisher_yates_site_id_resolved"
                            if api == "LegacyFisherYatesV111" else
                            "native_random_choice_site_id_resolved"
                            if api == "NativeRandomChoiceV111" else
                            "legacy_gaussian_component_mapped_to_parent"
                            if gaussian_component_parent_mapping else
                            "legacy_random_choice_site_id_resolved"
                        )
                    else:
                        review_status = "site_id_is_dynamic_or_unresolved"
                    matrix_status = (
                        "mapped"
                        if site_values and all(
                            value in matrix_site_ids
                            for value in matrix_site_values
                        )
                        else "unmapped"
                        if site_values
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
                        "site_id_values": site_values,
                        "matrix_site_id_values": matrix_site_values,
                        "matrix_row_status": matrix_status,
                        "source_excerpt": line.strip(),
                    }
                )
            source_offset += len(line) + 1

    for path, source in sources.items():
        if path.name not in MAP_RANDOM_ADAPTER_FILES or re.search(
            r"\bprivate\s+sealed\s+class\s+MapRandom\b", source
        ) is None:
            continue
        relative = path.relative_to(ROOT).as_posix()
        for match in MAP_RANDOM_CALL_PATTERN.finditer(source):
            operation = match.group("operation")
            arguments = _call_arguments(source, match.start())
            site_expression = _map_random_site_id_argument(
                operation, arguments
            )
            line_number = source.count("\n", 0, match.start()) + 1
            site_values = (
                _resolve_member_site_values(
                    source, match.start(), site_expression,
                    _method_string_constants(
                        source, match.start(), string_constants
                    ),
                    string_constants,
                )
                if site_expression is not None else []
            )
            matrix_status = (
                "mapped"
                if site_values and all(
                    value in matrix_site_ids for value in site_values
                )
                else "unmapped"
                if site_values
                else "missing"
                if site_expression is None
                else "dynamic_expression"
            )
            entries.append(
                {
                    "path": relative,
                    "line": line_number,
                    "api": "MapRandom",
                    "operation": operation,
                    "category": "source_map_random_adapter",
                    "review_status": (
                        "source_map_random_adapter_site_" + matrix_status
                    ),
                    "site_id_argument": site_expression,
                    "site_id_value": (
                        site_values[0] if len(site_values) == 1 else None
                    ),
                    "site_id_values": site_values,
                    "matrix_row_status": matrix_status,
                    "source_excerpt": source.splitlines()[
                        line_number - 1
                    ].strip(),
                }
            )

    counts: dict[str, int] = {}
    for entry in entries:
        key = entry["category"]
        counts[key] = counts.get(key, 0) + 1

    map_adapter_calls = [
        entry for entry in entries
        if entry["category"] == "source_map_random_adapter"
    ]
    map_adapter_callsite_coverage = {
        "call_count": len(map_adapter_calls),
        "mapped_call_count": sum(
            entry["matrix_row_status"] == "mapped"
            for entry in map_adapter_calls
        ),
        "unmapped_calls": [
            {
                "path": entry["path"],
                "line": entry["line"],
                "operation": entry["operation"],
                "site_id_argument": entry["site_id_argument"],
                "matrix_row_status": entry["matrix_row_status"],
            }
            for entry in map_adapter_calls
            if entry["matrix_row_status"] != "mapped"
        ],
    }
    native_player_rng_calls = [
        entry for entry in entries
        if entry["category"] == "native_player_rng_adapter"
    ]
    native_player_rng_callsite_coverage = {
        "call_count": len(native_player_rng_calls),
        "mapped_call_count": sum(
            entry["matrix_row_status"] == "mapped"
            for entry in native_player_rng_calls
        ),
        "unmapped_calls": [
            {
                "path": entry["path"],
                "line": entry["line"],
                "operation": entry["operation"],
                "site_id_argument": entry["site_id_argument"],
                "matrix_row_status": entry["matrix_row_status"],
            }
            for entry in native_player_rng_calls
            if entry["matrix_row_status"] != "mapped"
        ],
    }
    legacy_random_choice_calls = [
        entry for entry in entries
        if entry["category"] == "legacy_random_choice_adapter"
    ]
    legacy_random_choice_callsite_coverage = {
        "call_count": len(legacy_random_choice_calls),
        "mapped_call_count": sum(
            entry["matrix_row_status"] == "mapped"
            for entry in legacy_random_choice_calls
        ),
        "unmapped_calls": [
            {
                "path": entry["path"],
                "line": entry["line"],
                "operation": entry["operation"],
                "site_id_argument": entry["site_id_argument"],
                "matrix_row_status": entry["matrix_row_status"],
            }
            for entry in legacy_random_choice_calls
            if entry["matrix_row_status"] != "mapped"
        ],
    }
    native_event_context_calls = [
        entry for entry in entries
        if entry["category"] == "native_event_context_adapter"
    ]
    native_event_context_callsite_coverage = {
        "call_count": len(native_event_context_calls),
        "mapped_call_count": sum(
            entry["matrix_row_status"] == "mapped"
            for entry in native_event_context_calls
        ),
        "unmapped_calls": [
            {
                "path": entry["path"],
                "line": entry["line"],
                "operation": entry["operation"],
                "site_id_argument": entry["site_id_argument"],
                "matrix_row_status": entry["matrix_row_status"],
            }
            for entry in native_event_context_calls
            if entry["matrix_row_status"] != "mapped"
        ],
    }
    legacy_fisher_yates_calls = [
        entry for entry in entries
        if entry["category"] == "legacy_fisher_yates_adapter"
    ]
    legacy_fisher_yates_callsite_coverage = {
        "call_count": len(legacy_fisher_yates_calls),
        "mapped_call_count": sum(
            entry["matrix_row_status"] == "mapped"
            for entry in legacy_fisher_yates_calls
        ),
        "unmapped_calls": [
            {
                "path": entry["path"],
                "line": entry["line"],
                "operation": entry["operation"],
                "site_id_argument": entry["site_id_argument"],
                "matrix_row_status": entry["matrix_row_status"],
            }
            for entry in legacy_fisher_yates_calls
            if entry["matrix_row_status"] != "mapped"
        ],
    }
    native_random_choice_calls = [
        entry for entry in entries
        if entry["category"] == "native_random_choice_adapter"
    ]
    native_random_choice_callsite_coverage = {
        "call_count": len(native_random_choice_calls),
        "mapped_call_count": sum(
            entry["matrix_row_status"] == "mapped"
            for entry in native_random_choice_calls
        ),
        "unmapped_calls": [
            {
                "path": entry["path"],
                "line": entry["line"],
                "operation": entry["operation"],
                "site_id_argument": entry["site_id_argument"],
                "matrix_row_status": entry["matrix_row_status"],
            }
            for entry in native_random_choice_calls
            if entry["matrix_row_status"] != "mapped"
        ],
    }

    return {
        "schema_id": SCHEMA_ID,
        "scope": (
            "Direct qualified calls to PrototypeRng, NativeRngV111, and "
            "NativeFisherYatesV111 in Sts2Emulator.Core, direct calls through "
            "NativePlayerRngV111, LegacyRandomChoiceV111, "
            "LegacyFisherYatesV111, NativeEventContextRngV111, direct "
            "NativeRandomChoiceV111 callers outside NativePlayerRngV111, "
            "source callsites through NativeCombatRngEntryV111 random "
            "boundaries, and source "
            "callsites "
            "to the private MapRandom "
            "adapters in the two modeled Native map generators. This does not "
            "include other indirect delegates, "
            "random provider interfaces, third-party/native source callsites, "
            "or prove that every source operation is mapped. "
            "The adapter callsite inventory is Core-scoped. "
            "A separate direct-API scan reports typed and target-typed "
            "System.Random construction, typed declarations and aliases, "
            "shared-instance use, and RandomNumberGenerator use across runtime "
            "C# sources under `src/` and `native/`, excluding tests; it cannot "
            "prove the absence of dynamically injected or otherwise indirect "
            "entropy sources. A separate direct-reference guard checks "
            "Chance Law symbols in Native RNG providers/adapters and native "
            "mod C# sources; it cannot establish the absence of indirect "
            "dependencies. A Chance Law query call-graph guard follows calls "
            "by method name through static methods declared on the "
            "ChanceLawEngineV111 partial type and reports Native sampling, "
            "provider-cursor, boundary-journal, replay-capture, or engine-step "
            "references reached from query methods. It does not resolve "
            "overloads or calls into other types, delegates, or reflection. "
            "Explicit typed random-request "
            "constructions are listed with their site ID, index operation-kind "
            "expression, and registry/matrix resolution status. The static "
            "operation locator does not resolve forwarded caller values on its "
            "own. Generic Native bounded-index requests resolve to the finite "
            "runtime-validated Native operation registry, while the other "
            "generic Native request constructors resolve through mapped "
            "player-choice and Native combat-entry helper callsites in Core; "
            "callers outside Core "
            "remain outside this "
            "census. Runtime request validation "
            "checks operation kinds against the "
            "32 legacy caller-bound declarations, the registry's finite legacy "
            "Fisher-Yates site set, and its Native "
            "bounded-index and Fisher-Yates site sets; the low-level Native "
            "shuffle call is resolved through its fail-closed registry set, "
            "and the shared Native/legacy Fisher-Yates core request resolves "
            "only when both wrapper guards and core callers are present; "
            "generic forwarded values outside those declarations remain unresolved. "
            "The site-matrix report shows registry-to-ledger and ledger-to-registry "
            "differences, unique ledger IDs, and explicit reference-law status. "
            "Unregistered ledger rows are informational because legacy and conditional "
            "sites may remain outside the active registry. The S0 locator reports "
            "row-local or site-tied linked contexts for private constraints, audience "
            "projections, resolvers/continuations, probability provenance, and "
            "unsupported handling; these matches require manual review and do not "
            "prove source completeness. The legacy random-choice forwarding row "
            "follows the source-owned provider allowlist; its four internal "
            "Gaussian `.component` requests map to their parent Gaussian site rows. "
            "Legacy Fisher-Yates callsite values follow source expressions or "
            "finite Core helper callers; event-context shuffles use their validated "
            "operation-specific site list."
        ),
        "total_direct_calls": len(entries),
        "ambient_entropy_calls": ambient_entropy_calls,
        "native_chance_law_dependency_references":
            native_chance_law_dependency_references,
        "chance_law_outcome_blind_contract":
            chance_law_outcome_blind_contract,
        "typed_request_constructions": typed_request_constructions,
        "source_map_random_adapter_calls": map_adapter_calls,
        "source_map_random_adapter_callsite_coverage":
            map_adapter_callsite_coverage,
        "native_player_rng_adapter_calls": native_player_rng_calls,
        "native_player_rng_adapter_callsite_coverage":
            native_player_rng_callsite_coverage,
        "legacy_random_choice_adapter_calls": legacy_random_choice_calls,
        "legacy_random_choice_adapter_callsite_coverage":
            legacy_random_choice_callsite_coverage,
        "native_event_context_adapter_calls": native_event_context_calls,
        "native_event_context_adapter_callsite_coverage":
            native_event_context_callsite_coverage,
        "legacy_fisher_yates_adapter_calls": legacy_fisher_yates_calls,
        "legacy_fisher_yates_adapter_callsite_coverage":
            legacy_fisher_yates_callsite_coverage,
        "native_random_choice_adapter_calls": native_random_choice_calls,
        "native_random_choice_adapter_callsite_coverage":
            native_random_choice_callsite_coverage,
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
        "Source-shaped map `MapRandom` callsites mapped to matrix rows: "
        f"**{report['source_map_random_adapter_callsite_coverage']['mapped_call_count']}/"
        f"{report['source_map_random_adapter_callsite_coverage']['call_count']}**. "
        "This inventories the high-level NextInt, NextGaussianInt, and Shuffle "
        "calls in the two modeled Native map generators; their adapters issue "
        "typed requests or typed Fisher–Yates swaps internally.",
        "",
        "Direct `NativePlayerRngV111` callsites mapped to matrix rows: "
        f"**{report['native_player_rng_adapter_callsite_coverage']['mapped_call_count']}/"
        f"{report['native_player_rng_adapter_callsite_coverage']['call_count']}**. "
        "These are the high-level Native index and unit-float request sites; "
        "stream and occurrence ownership remain with their callers.",
        "",
        "Direct `LegacyRandomChoiceV111` callsites mapped to matrix rows: "
        f"**{report['legacy_random_choice_adapter_callsite_coverage']['mapped_call_count']}/"
        f"{report['legacy_random_choice_adapter_callsite_coverage']['call_count']}**. "
        "These are Core legacy index and bounded-integer adapter calls; "
        "stream and occurrence ownership remain with their callers.",
        "",
        "Direct `NativeEventContextRngV111` callsites mapped to matrix rows: "
        f"**{report['native_event_context_adapter_callsite_coverage']['mapped_call_count']}/"
        f"{report['native_event_context_adapter_callsite_coverage']['call_count']}**. "
        "These are Core Native event index, bounded-integer, unit-float, and "
        "shuffle adapter calls; event stream and occurrence ownership remain "
        "with their callers.",
        "",
        "Direct `LegacyFisherYatesV111` callsites mapped to matrix rows: "
        f"**{report['legacy_fisher_yates_adapter_callsite_coverage']['mapped_call_count']}/"
        f"{report['legacy_fisher_yates_adapter_callsite_coverage']['call_count']}**. "
        "These are Core legacy shuffle adapter calls; finite map and event "
        "helper-site values are traced through their guarded call boundaries.",
        "",
        "Direct `NativeRandomChoiceV111` callsites outside its `NativePlayerRngV111` "
        "wrapper mapped to matrix rows: "
        f"**{report['native_random_choice_adapter_callsite_coverage']['mapped_call_count']}/"
        f"{report['native_random_choice_adapter_callsite_coverage']['call_count']}**. "
        "The NativePlayer wrapper's internal forwarding calls are represented "
        "by the higher-level NativePlayer callsite rows.",
        "",
        "Potential ad hoc entropy API/type references in runtime C# sources (`src/`, `native/`): "
        f"**{len(report['ambient_entropy_calls'])}**. The scan covers direct "
        "System.Random typed or target-typed construction, declarations, aliases, "
        "shared-instance use and RandomNumberGenerator APIs; it cannot prove the "
        "absence of dynamically injected or otherwise indirect entropy sources.",
        *[
            f"- {call['path']}:{call['line']} {call['finding']}"
            for call in report["ambient_entropy_calls"]
        ],
        "",
        "Direct Chance Law symbol references in Native RNG provider/adapter "
        "and native mod C# sources: "
        f"**{len(report['native_chance_law_dependency_references'])}**. "
        "The scan cannot establish the absence of indirect dependencies.",
        *[
            f"- {call['path']}:{call['line']} `{call['symbol']}`"
            for call in report[
                "native_chance_law_dependency_references"
            ]
        ],
        "Chance Law transitive outcome-blind query guard: "
        f"**{report['chance_law_outcome_blind_contract']['query_method_count']}** query methods, "
        f"**{report['chance_law_outcome_blind_contract']['reachable_method_count']}** reachable methods in "
        f"**{report['chance_law_outcome_blind_contract']['engine_method_count']}** indexed ChanceLawEngine methods, "
        f"**{len(report['chance_law_outcome_blind_contract']['unparsed_query_methods'])}** query bodies without parsed code, "
        f"**{len(report['chance_law_outcome_blind_contract']['violations'])}** forbidden sampling, cursor, or replay references. "
        "Calls are matched by method name; overload resolution, other types, delegates, and reflection are outside this source check.",
        *[
            f"- {finding['path']}:{finding['line']} "
            f"`{finding['entry_method']}` via "
            f"`{' -> '.join(finding['call_chain'])}` "
            f"{finding['finding']}"
            for finding in report[
                "chance_law_outcome_blind_contract"
            ]["violations"]
        ],
        "",
        "Explicit typed random-request constructions: "
        f"**{len(report['typed_request_constructions'])}**; site IDs resolved "
        f"to registry: **{sum(call['registry_status'] in ('mapped', 'conditional') for call in report['typed_request_constructions'])}**; "
        f"dynamic or missing IDs: **{sum(call['registry_status'] in ('dynamic_expression', 'missing') for call in report['typed_request_constructions'])}**; "
        f"static IDs absent from registry: **{sum(call['registry_status'] in ('not_registered', 'partially_unregistered') for call in report['typed_request_constructions'])}**.",
        "",
        "Index request operation-kind locators: " + ", ".join(
            f"`{kind}` **{count}**"
            for kind, count in sorted(Counter(
                "site_keyed_dictionary"
                if call["operation_kind_resolution"] ==
                    "site_keyed_dictionary"
                else "unresolved"
                if not call["operation_kind_values"]
                else "finite_alternatives"
                if len(call["operation_kind_values"]) > 1
                else call["operation_kind_values"][0]
                for call in report["typed_request_constructions"]
                if call["request_type"] == "RandomIndexRequestV111"
            ).items())
        ) + ". The table reports constructor expressions, defaults, and site-keyed dictionary mappings where traceable; generic forwarded operation values remain manual trace items.",
        "",
        "Expression-origin classes: " + ", ".join(
            f"`{kind}` **{count}**"
            for kind, count in sorted(Counter(
                call["site_id_origin_kind"]
                for call in report["typed_request_constructions"]
            ).items())
        ) + ". Bare forwarded identifiers and members remain manual trace items unless the row names a finite Core helper-callsite closure.",
        "",
        "| File:line | Request type | Operation kind | Site ID expression | Origin | Resolution | Resolved site ID | Registry | Matrix |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        *[
            f"| [{call['path']}:{call['line']}](../{call['path']}#L{call['line']}) "
            f"| {call['request_type']} | "
            f"{call['operation_kind_expression'] or ('default choose_index' if call['request_type'] == 'RandomIndexRequestV111' else '—')} "
            f"→ {', '.join(call['operation_kind_values']) or call['operation_kind_resolution']} | "
            f"{call['site_id_expression'] or '—'} | "
            f"{call['site_id_origin_kind']} | "
            f"{call['site_id_resolution_source'] or '—'} | "
            f"{', '.join(call['site_id_values']) or '—'} | {call['registry_status']} | "
            f"{call['matrix_status']} |"
            for call in report["typed_request_constructions"]
        ],
        "",
        "Site-keyed index operation kinds resolved from constructor dictionaries:",
        "",
        "| Site ID | Operation kind |",
        "| --- | --- |",
        *[
            f"| `{site_id}` | `{', '.join(operation_kinds)}` |"
            for site_id, operation_kinds in sorted({
                site_id: operation_kinds
                for call in report["typed_request_constructions"]
                for site_id, operation_kinds in
                    call["operation_kind_by_site"].items()
            }.items())
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
        report["site_matrix_contract_evidence"]["method"],
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
        "--check-no-ambient-entropy "
        "--check-no-native-chance-law-dependency "
        "--check-chance-law-outcome-blind --format markdown > "
        "docs/RNG_DIRECT_CALLSITE_CENSUS.md`.",
        "Rows distinguish legacy prototype calls, native primitive calls, "
        "generic RNG-helper calls, and recognized provider/fixture support. "
        "Shuffle rows include their `siteId` argument, resolved constant where "
        "available, and exact migration-matrix match for static IDs. The "
        "supplemental table traces site IDs passed through known event, "
        "combat-pool and map shuffle helpers, plus Native player-choice "
        "wrappers, Native combat HP/summon/hatch/monster-local entry points, "
        "and combat card-selection helpers at their source "
        "callsites. Generic constructors resolve through their mapped Core "
        "caller closures or the validated Native operation registry; this "
        "does not claim coverage of callers outside Core.",
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
        if entry["site_id_values"]:
            resolved_values = ", ".join(entry["site_id_values"])
            site_expression = (
                "`" + site_expression + "` → `" + resolved_values + "`"
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
        help="fail if a matrix row links to a missing local document or heading",
    )
    parser.add_argument(
        "--check-no-ambient-entropy", action="store_true",
        help="fail if Core directly constructs or consumes ad hoc entropy APIs",
    )
    parser.add_argument(
        "--check-no-native-chance-law-dependency", action="store_true",
        help=(
            "fail on direct Chance Law references in Native RNG providers, "
            "adapters, or native mod C# sources"
        ),
    )
    parser.add_argument(
        "--check-chance-law-outcome-blind", action="store_true",
        help=(
            "fail on provider sampling/cursor reads, boundary journals, replay "
            "captures, or engine steps reachable from Chance Law queries "
            "through methods declared on ChanceLawEngineV111"
        ),
    )
    args = parser.parse_args()
    report = collect()
    if args.check_site_matrix:
        coverage = report["registry_matrix_coverage"]
        map_callsite_coverage = report[
            "source_map_random_adapter_callsite_coverage"
        ]
        native_player_callsite_coverage = report[
            "native_player_rng_adapter_callsite_coverage"
        ]
        legacy_choice_callsite_coverage = report[
            "legacy_random_choice_adapter_callsite_coverage"
        ]
        native_event_context_callsite_coverage = report[
            "native_event_context_adapter_callsite_coverage"
        ]
        legacy_fisher_yates_callsite_coverage = report[
            "legacy_fisher_yates_adapter_callsite_coverage"
        ]
        native_random_choice_callsite_coverage = report[
            "native_random_choice_adapter_callsite_coverage"
        ]
        if (coverage["registered_site_count"] == 0 or
                coverage["site_ids_without_matrix_rows"] or
                coverage["duplicate_matrix_site_ids"] or
                coverage["unresolved_registry_references"] or
                coverage["reference_law_status"]["rows_without_explicit_status"] or
                report["site_id_forwarder_coverage"][
                    "calls_without_matrix_rows"] or
                map_callsite_coverage["call_count"] == 0 or
                map_callsite_coverage["unmapped_calls"] or
                native_player_callsite_coverage["call_count"] == 0 or
                native_player_callsite_coverage["unmapped_calls"] or
                legacy_choice_callsite_coverage["call_count"] == 0 or
                legacy_choice_callsite_coverage["unmapped_calls"] or
                native_event_context_callsite_coverage["call_count"] == 0 or
                native_event_context_callsite_coverage["unmapped_calls"] or
                legacy_fisher_yates_callsite_coverage["call_count"] == 0 or
                legacy_fisher_yates_callsite_coverage["unmapped_calls"] or
                native_random_choice_callsite_coverage["call_count"] == 0 or
                native_random_choice_callsite_coverage["unmapped_calls"]):
            print(json.dumps({
                "registry_matrix_coverage": coverage,
                "source_map_random_adapter_callsite_coverage":
                    map_callsite_coverage,
                "native_player_rng_adapter_callsite_coverage":
                    native_player_callsite_coverage,
                "legacy_random_choice_adapter_callsite_coverage":
                    legacy_choice_callsite_coverage,
                "native_event_context_adapter_callsite_coverage":
                    native_event_context_callsite_coverage,
                "legacy_fisher_yates_adapter_callsite_coverage":
                    legacy_fisher_yates_callsite_coverage,
                "native_random_choice_adapter_callsite_coverage":
                    native_random_choice_callsite_coverage,
            }, indent=2, sort_keys=True),
                  file=sys.stderr)
            return 1
    if args.check_s0_contract_links:
        broken_contract_links: set[str] = set()
        broken_contract_anchors: set[str] = set()
        for row in _migration_matrix_site_rows():
            for target in row["links"]:
                parsed = urlparse(target)
                if parsed.scheme or parsed.netloc:
                    continue
                path = (ROOT / "docs" / parsed.path).resolve()
                if not path.is_file():
                    broken_contract_links.add(target)
                    continue
                if parsed.fragment and path.suffix.lower() == ".md":
                    if _markdown_heading_context(
                        path.read_text(encoding="utf-8"),
                        parsed.fragment,
                    ) is None:
                        broken_contract_anchors.add(target)
        if broken_contract_links or broken_contract_anchors:
            print(json.dumps({
                "broken_local_contract_links":
                    sorted(broken_contract_links),
                "broken_local_contract_anchors":
                    sorted(broken_contract_anchors),
            }, indent=2, sort_keys=True), file=sys.stderr)
            return 1
    if args.check_no_ambient_entropy and report["ambient_entropy_calls"]:
        print(json.dumps({
            "ambient_entropy_calls": report["ambient_entropy_calls"],
        }, indent=2, sort_keys=True), file=sys.stderr)
        return 1
    if (args.check_no_native_chance_law_dependency and
            report["native_chance_law_dependency_references"]):
        print(json.dumps({
            "native_chance_law_dependency_references":
                report["native_chance_law_dependency_references"],
        }, indent=2, sort_keys=True), file=sys.stderr)
        return 1
    if args.check_chance_law_outcome_blind and (
            report["chance_law_outcome_blind_contract"][
                "query_method_count"] == 0 or
            report["chance_law_outcome_blind_contract"][
                "unparsed_query_methods"] or
            report["chance_law_outcome_blind_contract"]["violations"]):
        print(json.dumps({
            "chance_law_outcome_blind_contract":
                report["chance_law_outcome_blind_contract"],
        }, indent=2, sort_keys=True), file=sys.stderr)
        return 1
    if args.format == "markdown":
        print(render_markdown(report), end="")
    else:
        print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
