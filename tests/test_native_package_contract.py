"""Guard against regressing the installed-game assembly resolution failure.

These are *packaging contract* checks only: real .NET native compilation,
Godot PCK export, and game startup require a local installed game.
"""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
CSPROJ = ROOT / "native" / "Sts2ReferenceMod" / "Sts2ReferenceMod.csproj"
BUILDER = ROOT / "scripts" / "build-native-inspector.fish"


class NativePackageContractTests(unittest.TestCase):
    def test_project_references_are_standalone_only(self):
        project = ET.parse(CSPROJ).getroot()
        refs = [
            (parent.attrib.get("Condition", ""), ref.attrib["Include"])
            for parent in project.findall("ItemGroup")
            for ref in parent.findall("ProjectReference")
        ]
        self.assertEqual(
            [("'$(Sts2DataDir)' == ''",
              "../../src/Sts2Emulator.Trace/Sts2Emulator.Trace.csproj")],
            refs,
        )

    def test_native_protocol_source_is_embedded(self):
        project = ET.parse(CSPROJ).getroot()
        native_compiles = [
            item.attrib["Include"]
            for group in project.findall("ItemGroup")
            if group.attrib.get("Condition") == "'$(Sts2DataDir)' != ''"
            for item in group.findall("Compile")
        ]
        self.assertIn("../../src/Sts2Emulator.Core/*.cs", native_compiles)
        self.assertIn("../../src/Sts2Emulator.Trace/*.cs", native_compiles)

    def test_package_includes_single_mod_dll_and_pck(self):
        script = BUILDER.read_text(encoding="utf-8")
        self.assertIn('cp "$project/bin/Release/net9.0/Sts2ReferenceMod.dll" "$dest/"', script)
        self.assertIn('"$dest/Sts2ReferenceMod.pck"', script)
        self.assertIn('rm -f "$dest/Sts2Emulator.Trace.dll" "$dest/Sts2Emulator.Core.dll"', script)
        self.assertNotIn("for dll in Sts2ReferenceMod.dll Sts2Emulator.Trace.dll", script)

    def test_godot_export_uses_temporary_source_free_project(self):
        script = BUILDER.read_text(encoding="utf-8")
        self.assertIn('mktemp -d "$root/.inspector-pack-XXXXXXXX"', script)
        self.assertIn('cp "$project/mod_manifest.json" "$pack_project/mod_manifest.json"', script)
        self.assertIn('cp "$project/export_presets.cfg" "$pack_project/export_presets.cfg"', script)
        self.assertIn('"$godot" --headless --path "$pack_project" --import', script)
        self.assertNotIn('pushd "$project"', script)
        self.assertNotIn('"$godot" --headless --path . --export-pack', script)

    def test_manifest_pck_name_is_consistent(self):
        import json
        manifest = json.loads(
            (ROOT / "native" / "Sts2ReferenceMod" / "mod_manifest.json").read_text()
        )
        self.assertEqual("Sts2ReferenceMod", manifest["pck_name"])


if __name__ == "__main__":
    unittest.main()
