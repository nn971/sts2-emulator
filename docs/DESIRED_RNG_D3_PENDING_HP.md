# Desired RNG D3: pending Nature HP boundaries

**Scope:** D3's first serializable, counterfactual combat-entry slice. Stacked
on PR #123 (working v111 emulator), independent of native Inspector PR #121.
This is **not yet a full Nature-controlled combat run**.

## What is implemented

`ControlledInitialHpV111.Begin(combat, formation, referenceStreams)`
constructs a controlled-mode continuation for initial monster HP rolls,
using source-shaped formation identities and a deep-forked source state.
It does not attach a fake `Combat.NativeRng` to the controlled result.

`ControlledInitialHpStateV111.Pending` is a typed choice request
containing mechanic, stream, stable creature CombatId, creation ordinal, and
ordered eligible HP pool. The pool is recomputed from exactly the same helper
used by native HP resolution.

`Resolve(state, expectedFingerprint, selectedIndex)` validates the snapshot
fingerprint, source-legal choice, exact transcript/owner/eligibility prefix,
and selected outcome **before mutating** any mechanics state. It returns
a new immutable logical continuation: selected HP is applied to the
appropriate creature, the controlled transcript is extended, and the next
pending HP choice is generated from the just-updated mechanical state.

`Save` and `Restore` serialize/deserialize the entire decision boundary:
combat, encounter formation, reference stream snapshot, current position,
applied outcomes, pending request and controlled-mode ID. `Fork` deep-copies
all arrays, encounter streams and individual RNG state byte buffers; a child
resumes separately. A completed state exposes no pending choice.

**Important:** the controlled continuation never draws the native stream and
must not be represented as a seeded-native combat snapshot. The separate
`ReferenceStreams` field is input/provenance state, not a fabricated advancing
shadow cursor; it stays untouched. Original `AttachAndRollInitialHp` still
uses the same native source RNG and produces unchanged seeded cursors and
combat state hashes.

## Test gate

- Replay captured native choice *indices* through controlled boundaries and
  compare all selected HP outcomes and formation identity (while separately
  checking native exact seeded hash/cursor through D1/D2 tests).
- Fork in the middle of a multi-enemy creation sequence and resolve
  different legal outcomes independently.
- Save/restore pending and after a first chosen outcome, then resume.
- Reject stale fingerprint, illegal index, post-completion extra selection,
  swapped creature ownership, modified eligibility, and malformed transcript.
- Confirm no hidden native cursor advances in controlled mode and no mutation
  of the parent state on rejected submissions.

## Explicitly not implemented

- This isolated D3 initial-HP continuation is not incorporated into general
  `RunState` / `IDeterministicEngine.Step` yet.
- Enemy intent chance boundaries, nested power hooks, reward RNG, draws,
  shuffles, continuous/weighted non-index random operations and player-choice
  interleaving remain unmigrated in controlled mode.
- No JSONL/Python bridge, information-policy schema or live Nature training
  in `sts2-ai` exists yet.
- No probability calibration is claimed and no full native-vs-emulator parity
  follows from a correct scripted HP replay.

Next: extend the persisted continuation pattern to committed enemy intents
and subsequent effects, then compose these slices into the versioned
advance/resolve/fork engine API without changing normal native `Step`.
