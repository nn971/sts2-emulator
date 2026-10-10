#!/usr/bin/env python3
"""Exercise actual JSONL/binding behavior, including failed batch atomicity."""
import argparse
import json
import pathlib
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "bindings/python"))
from sts2_emulator import EmulatorError, Session


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    command = [args.dotnet, str(ROOT / "src/Sts2Emulator.Cli/bin/Release/net9.0/Sts2Emulator.Cli.dll"), "prototype-ai-jsonl"]
    with Session(command) as session:
        assert session.capabilities["v111FidelityId"] == "v111-mechanics-prototype-rng-v1"
        for region in ["Overgrowth", "Underdocks"]:
            root = session.reset("binding-" + region, first_act=region)
            parent_hash = root.exact_hash()
            action = root.actions()[0]["actionId"]
            children = session.step_batch([(root, action), (root, action)], parallelism=2)
            assert children[0].exact_hash() == children[1].exact_hash()
            assert root.exact_hash() == parent_hash
            snapshot = children[0].save_snapshot()
            restored = session.load_snapshot(snapshot)
            assert restored.exact_hash() == children[0].exact_hash()
            assert restored.observe() == children[0].observe()
            assert restored.fork().exact_hash() == restored.exact_hash()
            fair = json.dumps(restored.observe())
            assert "state_bytes" not in fair and "run_seed" not in fair
            try:
                session.step_batch([(root, action), (root, "invalid-action")])
            except EmulatorError:
                pass
            else:
                raise AssertionError("Invalid batch succeeded")
            assert root.exact_hash() == parent_hash
            children[0].release()
        # Capture normalized synthetic engine transitions through the public
        # transport, then exercise the actual CLI reader and failure exit code.
        state = session.reset("canonical-replay", first_act="Underdocks")
        initial = json.loads(state.save_snapshot())["state"]
        header = {"type": "header", "trace_schema": "0.1", "game_build": initial["game_build"],
                  "bridge_version": "synthetic-jsonl-integration", "emulator_version": "test",
                  "run_id": initial["run_id"], "run_seed": initial["run_seed"],
                  "captured_at_utc": "1970-01-01T00:00:00Z"}
        transitions = []
        def normalized(actions):
            return [{"kind": action["kind"], "payload": json.loads(action["payloadJson"])} for action in actions]
        for _ in range(8):
            before = json.loads(state.save_snapshot())
            actions = state.actions()
            after_state = state.step(actions[0]["actionId"])
            after = json.loads(after_state.save_snapshot())
            transitions.append({"type": "transition", "decision_index": after["state"]["decision_index"],
                "phase": before["state"]["phase"], "action": normalized(actions)[0],
                "before": {"hash": before["state_hash"], "state": before["state"]},
                "after": {"hash": after["state_hash"], "state": after["state"]},
                "rng_before": before["state"]["rng"], "rng_after": after["state"]["rng"],
                "diagnostics": [], "legal_actions_before": normalized(actions),
                "legal_actions_after": normalized(after_state.actions())})
            state = after_state
        with tempfile.TemporaryDirectory() as temporary:
            trace = pathlib.Path(temporary) / "synthetic.jsonl"
            def replay():
                trace.write_text("\n".join(json.dumps(record) for record in [header] + transitions) + "\n")
                return subprocess.run(command[:2] + ["replay", str(trace)], capture_output=True, text=True)
            result = replay()
            assert result.returncode == 0, result.stderr
            assert json.loads(result.stdout)["transitions_checked"] == 8
            transitions[0]["legal_actions_before"] = []
            mismatch = replay()
            assert mismatch.returncode == 1, mismatch.stderr
            assert json.loads(mismatch.stdout)["divergence"]["dimension"] == "legal-actions-before"
        old = session.reset("legacy", mode="prototype")
        assert old.observe()["ruleset_id"] == "prototype-silent-v0"
        coverage = session.coverage()
        assert not coverage["releaseReady"]
        assert next(item for item in coverage["summary"] if item["kind"] == "cards")["eligible"] == 558
    print("v111 binding, snapshot, batch, canonical replay and legacy compatibility checks passed")


if __name__ == "__main__":
    main()
