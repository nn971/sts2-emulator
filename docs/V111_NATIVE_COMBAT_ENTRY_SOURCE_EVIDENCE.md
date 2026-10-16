# v0.111 Native Combat Entry Source Evidence

**Scope:** pinned Slay the Spire 2 single-player, isolated `integration/fidelity-rng-on-silent-all-act-v111` branch. The active Silent all-Act development branch is untouched.

## Implemented

### Initial draw-pile shuffle (pre-first-frame)

`NativeV111ShuffleHooks` source-binds `CardPile.RandomizeOrderInternal` and records the actual ordered source cards, every Fisher-Yates `run.shuffle` choice and RNG cursor, the resulting pre-hook permutation, and the post-`Hook.ModifyShuffleOrder` permutation. This is buffered until the Inspector's first combat sidecar is opened, because the initial draw-pile shuffle occurs before the first rendered combat frame.

The same observer covers `CardPileCmd.Shuffle` recycling with native stable model sorting before the swap loop; later asynchronous card movement and after-shuffle hooks are independently pending.

### Unique monster initial HP (pre-first-frame)

`NativeV111InitialHpHooks` source-binds `Creature.SetUniqueMonsterHpValue(IReadOnlyList<Creature>, Rng)`. The pinned source forms a HashSet of `MinInitialHp..MaxInitialHp`, excludes existing monsters' max HP, and chooses from the ordered remaining set by `Rng.NextItem`; if the set is empty, it chooses a uniform index from the full interval.

The observer reconstructs this native ordered eligible pool *before* the call, preserves the original one-draw `run.niche` RNG result and pre/post xoshiro cursor, and reads the real chosen creature HP *after* the call. The event `native_initial_hp_roll` has:

- `creation_ordinal`, `monster_model_id`, `ordered_eligible_hp`
- `selected_index`, `selected_hp`, `source_eligible_values_exhausted`
- `cursor_before`, `cursor_after`
- source-callsite and before-first-frame provenance

**Creature identity is deliberately unresolved:** the source call occurs before the creature is fully attached to `CombatState`, so the event carries `native_creature_combat_id_status=PRE_ATTACH_CREATURE_ID_UNVERIFIED`, not an inferred `creature:N`. Midcombat summoned monsters can also invoke the same native method; these live records are marked `recorded_before_first_frame=false`.

Early source evidence is buffered and emitted after the first sidecar header; **the relative source-hook chronology of HP and shuffle buffered independently is not yet validated**. A separate unified preframe event clock will be needed for that stronger claim.

### Verification

- `NativeInitialHpRollEvidenceV111.Validate` checks source eligibility, uniqueness, selected HP and primitive cursor advancement.
- `tools/native_initial_hp_verify.py` additionally checks the exact xoshiro256** transformation and native bounded-int result. `tools/native_shuffle_operation_verify.py` performs the analogous swap-and-hook validation.
- `tools/native_action_envelope_batch.py` reports: `native_initial_hp_scope_installed_sidecars`, `native_initial_hp_source_rolls`, `native_initial_hp_validated_rolls`, `native_initial_hp_errors`, and the parallel native shuffle counters.
- The unified gate returns `NATIVE_INITIAL_HP_SOURCE_MISMATCH` or `NATIVE_SHUFFLE_SOURCE_MISMATCH` on contradictions, rather than labeling unsupported emulator checkpoints as passed.
- `NativeFisherYatesV111` is a source-authentic common **native/scripted/controlled** emulator RNG primitive with test-confirmed identical swaps and support under a recorded decision transcript.

```fish
# Run from an isolated checkout of this integration branch, not
# ~/projects/sts2-emulator.
set trace /mnt/d/SteamLibrary/sts2-inspector-traces
python tools/native_action_envelope_batch.py "$trace" \\
    --out "$trace/native-source-entry-audit.json"
jq '{
  native_initial_hp_scope_installed_sidecars,
  native_initial_hp_source_rolls,
  native_initial_hp_validated_rolls,
  native_initial_hp_errors,
  native_shuffle_scope_installed_sidecars,
  native_shuffle_source_operations,
  native_shuffle_verified_operations,
  native_shuffle_errors
}' "$trace/native-source-entry-audit.json"
```

## Acceptance gates remaining

1. **Actual native-only build/install** against pinned installed `sts2.dll` and `0Harmony.dll`, with `STS2_FIDELITY_CAPTURE=1` and `STS2_FIDELITY_RNG_HOOK=1`. Both new source hook groups are opt-in and synthetic CI does not compile `#if STS2_NATIVE`.
2. Source-to-assembly verification of hook signatures and source-callback ordering; check `shuffle_source_scope=INSTALLED` and `initial_hp_source_scope=INSTALLED` in the mod log. If a hook is absent, the corpus must explicitly label that coverage gap.
3. Native creature attachment ID mapping, native card-instance ID mapping and a **single interleaved preframe event clock** for the separately buffered HP/shuffle source events.
4. Import complete independently observed native combat start state into the actual `PrototypeGameEngine`; align persistent `run.shuffle` and `run.niche` cursors. Integrate the shared shuffle primitive into `Step` at audited sites, including source-native card sorting, `Hook.ModifyShuffleOrder` and after-shuffle mechanics.
5. Capture committed enemy intents from the true native source and turn the existing isolated HP→intent Nature continuation into arbitrary ordinary-game suspension.
6. Expand existing native card-play checkpoint support to power semantics (Weak/Shriek), skill counters/Pinpoint cost and automatic Defend replay; compare first divergence in state and hook order.

**Current conclusion:** real native source-observation hooks and offline diagnostic gates are implemented; **complete native↔emulator combat fidelity and native hook runtime installation are not yet certified**. No new manual capture is required during repository development, but a future installed-game session is needed to certify these new native-only hooks.
