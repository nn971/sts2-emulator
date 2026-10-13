#!/usr/bin/env python3
"""JSONL contract smoke for isolated hypothetical draw-order injection.

This deliberately does not inspect the hidden source draw pile. It derives the
candidate future card variants from the *public* observation only.
"""
from __future__ import annotations

import collections
import json
import pathlib
import subprocess


def main() -> None:
    executable = (
        pathlib.Path("src/Sts2Emulator.Cli/bin/Release/net9.0")
        / "Sts2Emulator.Cli.dll"
    )
    with subprocess.Popen(
        ["dotnet", str(executable), "prototype-ai-jsonl"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        text=True,
    ) as process:
        assert process.stdin is not None
        assert process.stdout is not None

        def call(payload: dict[str, object], *, fail: bool = False) -> dict:
            process.stdin.write(json.dumps(payload) + "\n")
            process.stdin.flush()
            line = process.stdout.readline()
            assert line, f"Bridge stopped responding for {payload.get('op')}"
            response = json.loads(line)
            assert response["ok"] != fail, response
            return response

        hello = call({"op": "hello"})
        assert hello["hypotheticalDrawOrderId"] == "prototype-hypothetical-draw-order-v1"

        live = call({"op": "reset", "seed": "secret-live-game-seed"})["stateHandle"]
        live_acts = call({"op": "legal_actions", "state_handle": live})["actions"]
        live_started = call({
            "op": "step", "state_handle": live,
            "action_id": live_acts[0]["actionId"],
        })["child"]
        call({
            "op": "hypothetical_draw_order",
            "state_handle": live_started,
            "ordered_cards": [],
        }, fail=True)

        inspected = 0
        to_release = [live, live_started]
        for index in range(12):
            search_seed = f"{index + 1:032x}"
            hypothetical = call({
                "op": "reset_hypothetical", "search_seed": search_seed,
            })
            assert hypothetical["hypothetical"] is True
            root = hypothetical["stateHandle"]
            to_release.append(root)
            start = call({"op": "legal_actions", "state_handle": root})["actions"]
            mapped = call({
                "op": "step", "state_handle": root,
                "action_id": start[0]["actionId"],
            })["child"]
            to_release.append(mapped)
            actions = call({"op": "legal_actions", "state_handle": mapped})["actions"]
            for action in actions:
                if action["kind"] != "choose_map_node":
                    continue
                entered = call({
                    "op": "step", "state_handle": mapped,
                    "action_id": action["actionId"],
                })["child"]
                to_release.append(entered)
                before = call({
                    "op": "observe", "state_handle": entered,
                    "policy_id": "prototype-fair-v0",
                })
                obs = json.loads(before["payloadJson"])
                combat = obs.get("combat")
                if combat is None:
                    continue
                if combat["turn"] != 1 or combat["discard_pile"] or combat["exhaust_pile"]:
                    continue

                def variant(card: dict) -> tuple[str, int]:
                    return card["card_id"], card["upgrade_level"]

                candidates = collections.Counter(map(variant, obs["deck"]))
                candidates.subtract(map(variant, combat["hand"]))
                assert min(candidates.values()) >= 0
                variants = sorted(candidates.elements())
                if len(variants) != combat["draw_pile_count"]:
                    continue
                ordered = [
                    {"card_id": name, "upgrade_level": upgrade}
                    for name, upgrade in variants
                ]
                response = call({
                    "op": "hypothetical_draw_order",
                    "state_handle": entered,
                    "ordered_cards": ordered,
                })
                assert response["schemaId"] == hello["hypotheticalDrawOrderId"]
                assert response["observationHash"] == before["observationHash"]
                assert "exactHash" not in response
                child = response["child"]
                to_release.append(child)
                after = call({
                    "op": "observe", "state_handle": child,
                    "policy_id": "prototype-fair-v0",
                })
                assert after["payloadJson"] == before["payloadJson"]
                assert call({
                    "op": "legal_actions", "state_handle": child,
                })["actions"] == call({
                    "op": "legal_actions", "state_handle": entered,
                })["actions"]

                invalid = ordered[:]
                invalid[0] = {"card_id": "NO_SUCH_CARD", "upgrade_level": 0}
                call({
                    "op": "hypothetical_draw_order",
                    "state_handle": entered,
                    "ordered_cards": invalid,
                }, fail=True)

                # After a combat action, original opening handle eligibility
                # must not propagate into that descendant.
                end = next(
                    action for action in call({
                        "op": "legal_actions", "state_handle": child,
                    })["actions"] if action["kind"] == "end_turn"
                )
                advanced = call({
                    "op": "step", "state_handle": child,
                    "action_id": end["actionId"],
                })["child"]
                to_release.append(advanced)
                call({
                    "op": "hypothetical_draw_order",
                    "state_handle": advanced,
                    "ordered_cards": ordered,
                }, fail=True)

                inspected += 1
                break
            if inspected >= 3:
                break

        assert inspected >= 3, f"Only {inspected} certified opening combats reached"
        release = call({
            "op": "release_many", "state_handles": to_release,
        })
        assert release["released"] == len(to_release)
        call({"op": "close"})
        assert process.wait(timeout=10) == 0
        print(f"Hypothetical draw-order bridge: {inspected} opening combats passed")


if __name__ == "__main__":
    main()
