#!/usr/bin/env python3
"""Compile identifier-only inventory from the two pinned, local reference caches.

No descriptions, assets, or source text are emitted. The source audit resolves
single-player exclusions; the structured parser alone cannot establish scope.
"""
import argparse
import hashlib
import json
import pathlib
import re
import subprocess

CORPUS_COMMIT = "b59c6f96043051afee8683237d04f943309ced35"
SOURCE_COMMIT = "706ef1c9fa2219220065849fd5265328607ece20"
ROOT = pathlib.Path(__file__).resolve().parents[1]
KINDS = {
    "acts": "Acts", "afflictions": "Afflictions", "ascensions": None,
    "cards": "Cards", "characters": "Characters", "enchantments": "Enchantments",
    "encounters": "Encounters", "epochs": "Epochs", "events": "Events",
    "monsters": "Monsters", "orbs": "Orbs", "potions": "Potions",
    "powers": "Powers", "relics": "Relics", "rest_site_options": None,
}


def normalize(value):
    return re.sub(r"[^A-Z0-9]", "", value.upper())


def compile_inventory(corpus, source):
    revision = subprocess.check_output(
        ["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if revision != SOURCE_COMMIT:
        raise ValueError("Audited source must be checked out at the pinned commit.")
    if subprocess.check_output(["git", "-C", str(source), "status", "--porcelain"], text=True):
        raise ValueError("Audited source checkout has local changes.")
    models = source / "src/Core/Models"
    tables, blobs = {}, {}
    for line in (ROOT / "tools/reference_source/spire_codex_v0.111.0_git_blobs.tsv").read_text().splitlines():
        if not line or line.startswith("#"):
            continue
        name, expected = line.split()
        raw = (corpus / name).read_bytes()
        actual = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
        if actual != expected:
            raise ValueError(f"Changed reference table: {name}")
        blobs[name] = actual
        tables[name.removesuffix(".json")] = json.loads(raw)

    source_files = {}
    for kind, folder in KINDS.items():
        if folder:
            source_files[kind] = {normalize(p.stem.removesuffix("Power") if kind == "powers" else p.stem): p
                                  for p in (models / folder).glob("*.cs")}
    source_files["rest_site_options"] = {
        normalize(p.stem.removesuffix("RestSiteOption")): p
        for p in (source / "src/Core/Entities/RestSite").glob("*RestSiteOption.cs")}

    multiplayer_files = set()
    for p in (models / "Cards").glob("*.cs"):
        if re.search(r"MultiplayerConstraint\s*=>\s*CardMultiplayerConstraint.MultiplayerOnly", p.read_text()):
            multiplayer_files.add(p)
    multiplayer_files.add(models / "Relics/MassiveScroll.cs")
    multiplayer_files.add(source / "src/Core/Entities/RestSite/MendRestSiteOption.cs")

    # A power reachable only through excluded cards is excluded as well. Scan
    # executable content owners, not ModelDb's registration list or UI code.
    content_sources = [p for folder in ["Cards", "Relics", "Potions", "Monsters", "Events", "Powers", "Enchantments", "Afflictions"]
                       for p in (models / folder).glob("*.cs")]
    texts = {p: p.read_text() for p in content_sources}
    exclusions = set(multiplayer_files)
    changed = True
    while changed:
        changed = False
        for p in (models / "Powers").glob("*.cs"):
            if p in exclusions:
                continue
            references = [q for q, text in texts.items() if q != p and re.search(r"\b" + re.escape(p.stem) + r"\b", text)]
            if references and all(q in exclusions for q in references):
                exclusions.add(p)
                changed = True

    items = []
    def add(kind, entity, parent=None, suffix=None):
        key = entity["id"] if suffix is None else f"{entity['id']}/{suffix}"
        path = source_files.get(kind, {}).get(normalize(entity["id"]))
        # Compound suffixes and native ID spelling are normalized separately
        # from presentation names. Names are never bundled in this artifact.
        if path is None and entity.get("name"):
            path = source_files.get(kind, {}).get(normalize(entity["name"]))
        excluded = path in exclusions
        item = {"kind": kind, "id": key, "parent": parent,
                "scope": "multiplayer-only" if excluded else "single-player",
                "pool": entity.get("color", entity.get("pool")),
                "act": entity.get("act"),
                "source": str(path.relative_to(source)) if path else None,
                "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest() if path else None}
        if kind not in ["ascensions", "epochs"] and path is None:
            item["scope"] = "review-required"
        if excluded:
            item["scope_reason"] = "source-owned multiplayer-only content"
        items.append(item)
        return item

    for kind in KINDS:
        for entity in tables[kind]:
            item = add(kind, entity)
            if kind == "acts":
                item["encounters"] = sorted(entity.get("encounters", []))
                item["events"] = sorted(entity.get("events", []))
                item["ancients"] = sorted(entity.get("ancients", []))
            if kind == "cards":
                item["variants"] = ["base", "upgraded"] if entity.get("upgrade") or entity.get("upgrade_description") else ["base"]
                item["generated"] = entity.get("rarity_key") in ["Token", "Status", "Quest"]
            if kind == "events":
                branches = set()
                for option in entity.get("options") or []:
                    if option.get("id"):
                        branches.add("initial/" + option["id"])
                for page in entity.get("pages") or []:
                    if not page.get("id"):
                        continue
                    branches.add("page/" + page["id"])
                    for option in page.get("options") or []:
                        if option.get("id"):
                            branches.add("page/" + page["id"] + "/" + option["id"])
                relic_scope = {x["id"]: x for x in tables["relics"]}
                for relic in entity.get("relics") or []:
                    relic_path = source_files["relics"].get(normalize(relic))
                    if relic in relic_scope and relic_path not in exclusions:
                        branches.add("relic/" + relic)
                item["branches"] = sorted(branches)
                item["branch_inventory_complete"] = False  # dynamic/source-only branches require auditing

    return {"schema": "sts2-v111-inventory-v1", "game_version": "v0.111.0",
            "game_commit": "41cef1ea", "build_fingerprint": "3bb5598a35f7763c9de22078643ac2777190994b2be85ebd4ebf5c9d154aa45e",
            "corpus_commit": CORPUS_COMMIT, "source_commit": SOURCE_COMMIT,
            "corpus_blobs": blobs, "items": sorted(items, key=lambda x: (x["kind"], x["id"]))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("corpus", type=pathlib.Path)
    parser.add_argument("source", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / "data/coverage/v111-inventory.json")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = json.dumps(compile_inventory(args.corpus, args.source), indent=2, ensure_ascii=True) + "\n"
    if args.check:
        if not args.output.exists() or args.output.read_text() != result:
            raise SystemExit("Inventory differs from its pinned sources. Regenerate and review scope changes.")
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result)
    data = json.loads(result)
    for scope in ["single-player", "multiplayer-only", "review-required"]:
        print(scope, sum(x["scope"] == scope for x in data["items"]))


if __name__ == "__main__":
    main()
