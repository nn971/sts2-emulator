# Native v0.111 capture forensics: 7 user-local combat recordings (2026-10-12)

## Evidence from archive

A user-supplied Inspector trace archive, collected with PR #130 at native
`sts2.dll` SHA-256
`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`,
contained seven encounters, 264 observed executor action envelopes (177
`PlayCardAction`, 42 `EndPlayerTurnAction`, 42
`ReadyToBeginEnemyTurnAction`, 3 `UsePotionAction`), 26 nested-choice
pauses, and complete start/end callbacks for all 264 observed actions.

The legacy sidecar recorder marked all seven terminals
`INCOMPLETE_CAPTURE`. Each trace has one final `CombatHistory` reset
immediately before terminal on `Combat ended`. All **26** recorded
`duplicate_action_start` anomalies occur *immediately* after a
`nested_choice_pause` on the same action and native type; there are no
other anomaly categories in this corpus. These are evidence of executor
resume/re-entry, not of 26 additional independent card plays. The new
auditor checks these exact conditions rather than applying a blanket
exception for duplicate starts or history resets.

The original coverage report declared **zero** player HP changes because
it inspected per-action start/end deltas; independent *consecutive frame
snapshots* record **25** changes across the seven combats. Frame changes
are observations, not necessarily a single atomic damage/death effect or
proof of its source hooks. 23 of 26 choice pauses provide a single
possible discard instance ID when comparing hand/discard zones at pause
and action completion, without proving choice identity.

The strict decision tracer still stops at nested choices, a potion action
outside its declared legal menu, or a second user action occurring before
the next main-thread ready frame. Full strict traces: 0/7 valid. However,
**10 decision transitions** have a completed action/transition and an
*independent next decision snapshot*, with matching recorded hash links
and event cursors: four in the Sludge Spinner capture and six in the
Terror Eel capture. Eight are card plays (Sucker Punch ×2, Defend ×2,
Strike, Neutralize, Backflip, Pinpoint); two are end-turn actions.
`tools/native_prefix_checkpoints.py` extracts only these closed
prefixes, never inferring a post-state from a terminal following an
unsupported nested choice. These are opportunities for later real-engine
comparison, **not established emulator parity**.

## Fixes

1. Sidecar recorder now recognizes `BeforeActionExecuted` re-entry on
   an object that previously emitted `BeforePausedForPlayerChoice`,
   retaining the same action identity and emitting `action_resumed`.
   Duplicate starts without a pending choice remain fatal anomalies.
2. A history clear after the last action can be part of combat cleanup.
   The recorder does not mark a no-active-action clear incomplete.
   The offline auditor considers it benign only if it occurs directly
   before a `Combat ended` terminal, to zero entries, with no live
   actions. All other reset cases remain flagged.
3. Auditor recognizes *legacy* resume anomalies only if directly
   preceded by a matching native choice pause on a running action. A
   legacy raw `INCOMPLETE_CAPTURE` terminal remains unchanged, but may
   receive the distinct qualified audit status
   `LEGACY_CALLBACK_SEQUENCE_RECOVERABLE` when its other callbacks
   reconcile. This is a review of observer artifacts, not an assertion
   that every native mechanic or hook was observed.
4. The coverage auditor examines frame-to-frame deltas in addition to
   action envelopes, so player HP changes between executor actions are
   no longer falsely declared unobserved.
5. The prefix extractor checks action menu membership, monotone native
   event cursor, transition-before/after hash linkage, and independently
   recorded next decision. It refuses unclosed last decisions, missing
   events, rejected actions, and orphaned/unknown records.

## User-local procedure (no new top-level worktrees)

Use the existing Inspector worktree `~/projects/sts2-inspector` to
check out the branch from its current clean state; do not make another
directory under `~/projects`.

```fish
cd ~/projects/sts2-inspector
git fetch origin feature/v111-native-resume-cleanup-recovery
git switch --track -c feature/v111-native-resume-cleanup-recovery \\
    origin/feature/v111-native-resume-cleanup-recovery
set trace /mnt/d/SteamLibrary/sts2-inspector-traces

python tools/native_action_envelope_batch.py "$trace" \\
    --out "$trace/corpus-audit-reviewed.json"

python tools/native_prefix_checkpoints.py "$trace" \\
    --out "$trace/native-closed-prefix-report.json" \\
    --fixture-dir "$trace/fixtures"
```

(The `git switch -c` command is for first checkout of this local branch.
If it already exists, use `git switch feature/v111-native-resume-cleanup-recovery`
and `git pull --ff-only` instead.)

Native build/load confirmation against the user's pinned installed DLL
is still required for the recorder revision. Exact RNG stream parity,
native source-hook order, and Nature training remain deferred.
