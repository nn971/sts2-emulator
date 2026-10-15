# Native StS2 Inspector: live agent spectator interface

**Status:** draft PR #158, review-only native side, local game compilation
and live HTTP validation pending. **Purpose:** the agent can subscribe to
stable real-game human-play combat observations as an independent
reference/trajectory oracle. This is intentionally **not** an action
injector or a full state-import protocol.

## Context and relation to Nature #143

The native game records original 12 run + 3 player RNG streams, source
primitives and action envelopes. The `NextFloat` source hook from
draft PR #153 closes an observed primitive-coverage gap on
`run.monster_ai` when it installs and captures calls successfully.

Issue [#143](https://github.com/nn971/sts2-emulator/issues/143) implements
**emulator-side** playable controlled Nature and a distinct semantic deck
state protocol. This native agent feed does not attempt to resolve Nature
choices in the original executable. A native stochastic source call is
not the same as an externally chosen semantic Nature outcome.
Native cursor fidelity, mechanical equivalence and conditional chance-law
calibration remain three separate gates.

## Enable locally (opt-in, read-only)

Clone `feature/v111-native-agent-spectator-bridge` into a temporary
worktree; use the [game-local build instructions](V111_NATIVE_GAME_WIDE_RNG_MECHANIC_TEST.md)
against your actual installed `sts2.dll`, with its **exact full SHA-256**
set in `STS2_FIDELITY_EXPECT_SHA256`. Continue using:
`STS2_FIDELITY_CAPTURE=1`,
`STS2_FIDELITY_RNG_HOOK=1`, and
`STS2_FIDELITY_RNG_STREAM_CHECKPOINTS=1`.

Add these Windows **game-process** environment variables:

| Variable | Value |
| --- | --- |
| `STS2_INSPECTOR_AGENT_BRIDGE` | `1` |
| `STS2_INSPECTOR_AGENT_TOKEN` | A locally generated unpredictable 24+ character secret |
| `STS2_INSPECTOR_AGENT_PORT` | `39221` (optional; default) |

Keep the secret out of source control, output traces, screenshots and bug
reports. Steam launch options are not a secure secret store; where
possible, set the environment securely before starting Steam. Use the
same secret in the spectator client's environment. The server binds
**only to 127.0.0.1** and requires `Authorization: Bearer ...` on
every request, including status.

The native log must report:

```text
AGENT_BRIDGE_STATUS=SPECTATOR_READY; schema=sts2-native-agent-spectator-v1; address=127.0.0.1:39221; actions=DISABLED; read_only=true
```

An invalid token, occupied port, failed DLL pin or scene-loop failure
prevents startup and is reported explicitly. The passive capture itself
continues if only the spectator server fails.

With the game running, invoke from a **Windows terminal** with a Python
interpreter and the same token (WSL localhost forwarding may vary):

```fish
# fish shell on the client, if fish is available
set -gx STS2_INSPECTOR_AGENT_TOKEN "<same-secret-kept-locally>"
python tools/native_agent_spectator.py --status
python tools/native_agent_spectator.py --follow
```

Or query with an authenticated local HTTP client:

```text
GET http://127.0.0.1:39221/v1/agent/status
GET http://127.0.0.1:39221/v1/agent/boundary
Authorization: Bearer <same-local-token>
```

Each response uses snake_case JSON keys. The boundary response has
`{ "status": ..., "boundary": null | { ... } }`. Stable decision records
contain `schema_id`, `observation_schema_id`, per-combat
`session_id`, `decision_id`, `revision`, *public-only*
`public_hash`, `observation`, `legal_actions`,
`legal_menu_coverage`, `control_mode=HUMAN_ONLY`, and
`native_game_parity=NOT_TESTED`.

### Observation coverage and fail-closed rules

The view includes native player HP/block/gold/powers; combat energy,
turn, visible creature identity/HP/block/powers and the visible hand,
discard/exhaust cards with instance IDs and current costs.
Only the **draw-pile count** is included, never private ordering.
The original native game state, RNG seed, RNG stream/cursor, original
privileged state hash, raw trace path, event history and private continuation
are excluded, including from the public hash and action payload.
Visible intent, relic/potion lists, map and nested prompt data are
explicitly marked **unobserved** until audited public projections are
available. The legal menu is currently only the *captured* play-card and
end-turn subset. It is **not a complete action menu**.

The available boundary disappears at every native executor action start,
including automatic actions; on a nested unsupported selection; when
the strict recording becomes incomplete; and at combat end. A new
stable player input boundary is necessary before an agent can observe
another action menu. Missing data and unsupported new native action types
produce explicit withheld boundaries. The feed never reconstructs
missing observations from a hidden state hash.

All POST endpoints, including `/v1/actions`, `/v1/step`,
`/v1/pause`, `/v1/resume` and `/v1/stop`, return
`403 NATIVE_SPECTATOR_READ_ONLY`. The privileged legacy
`/v1/events` route is also inaccessible. Neural inference can
consume the observations for diagnostics/teacher data. Real native
action injection awaits audited game-thread dispatch and
separate explicit control opt-in.

## Validation and next gates

- Check the **native game DLL build** and actual Harmony/source-hook
  attachment, plus bearer-token behavior, no unsolicited process
  connections and human-play no-perturbation.
- Start Silent single-player combat, request status and boundary at a
  stable hand, play a card, then immediately query status again. The
  previous boundary must be absent during resolution.
- During multi-enemy turns and nested choices, only explicit unavailable
  state may appear; when a new player hand becomes stable a *new*
  decision identity should be offered.
- Run primitive and checkpoint audits on the ordinary `*.actions.jsonl`
  trace. The spectator feed does not replace disk fidelity evidence.
- Verify deck-order privacy with an adversarial controlled fixture
  independently; do not promote `PARTIAL_NATIVE_COMBAT_OBSERVATION`
  to complete fair player observation.
- Subsequent P3 milestone: source-audit game action submission, target
  instance resolution, human/agent arbitration, idempotent step/pause
  and nested selections on the **game main thread**, with a separate
  switch from spectator-only mode.
- Subsequent fidelity milestone: add visible intent and status labels,
  per-mechanic first-difference diagnostics and an audited
  native/emulator comparison overlay.

No Steam DLLs, game assets, credentials or private captures are committed.
