#!/usr/bin/env python3
"""Check local Markdown links in documentation entry points and archives.

Use --all to inspect the entire Markdown corpus, including older reference
notes. The default checks active navigation plus historical archives.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
LINK = re.compile(r"(?<!!)\[[^\]\n]*\]\(([^)\n]+)\)")
EXTERNAL = re.compile(r"^[a-z][a-z0-9+.-]*:", re.IGNORECASE)


def sources(all_documents: bool) -> list[Path]:
    if all_documents:
        return sorted(p for p in ROOT.rglob("*.md") if ".git" not in p.parts)
    paths = [
        ROOT / "README.md",
        ROOT / "PROJECT_STATUS.md",
        ROOT / "CONTRIBUTING.md",
        *(ROOT / "docs").glob("*.md"),
        *(ROOT / "docs" / "archive").rglob("*.md"),
        ROOT / "docs" / "reference-builds" / "INDEX.md",
        ROOT / "docs" / "adr" / "INDEX.md",
    ]
    return sorted(set(p for p in paths if p.is_file()))


def visible_markdown(text: str) -> str:
    lines = []
    in_fence = False
    marker = ""
    for line in text.splitlines():
        fence = re.match(r"^\s*([\x60]{3,}|~{3,})", line)
        if fence:
            found = fence.group(1)
            if not in_fence:
                in_fence, marker = True, found[0]
            elif found[0] == marker:
                in_fence, marker = False, ""
            continue
        if not in_fence:
            lines.append(line)
    return "\n".join(lines)


def local_target(source: Path, raw: str) -> Path | None:
    uri = raw.strip()
    if uri.startswith("<") and ">" in uri:
        uri = uri[1 : uri.index(">")]
    else:
        uri = uri.split(" ", 1)[0]
    if not uri or uri.startswith(("#", "//")) or EXTERNAL.match(uri):
        return None
    uri = unquote(uri.split("#", 1)[0].split("?", 1)[0])
    if not uri:
        return None
    target = (source.parent / uri).resolve()
    if target.is_dir():
        target /= "INDEX.md"
    return target


def check(all_documents: bool) -> int:
    errors: list[str] = []
    paths = sources(all_documents)
    checked = 0
    for path in paths:
        text = visible_markdown(path.read_text(encoding="utf-8"))
        for match in LINK.finditer(text):
            target = local_target(path, match.group(1))
            if target is None:
                continue
            checked += 1
            if not target.is_file():
                errors.append(
                    f"{path.relative_to(ROOT)}: {match.group(1)} (resolved: {target})"
                )
    print(f"Checked {checked} local links in {len(paths)} Markdown files.")
    for error in errors:
        print("BROKEN: " + error)
    return 1 if errors else 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--all", action="store_true", help="check all reference notes")
    raise SystemExit(check(parser.parse_args().all))
