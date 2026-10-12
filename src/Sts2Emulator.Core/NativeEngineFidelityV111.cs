using System.Text.Json;

namespace Sts2Emulator.Core;

/// <summary>
/// A single observable field checked against the ACTUAL PrototypeGameEngine
/// continuation. This is distinct from the earlier special-purpose card rule
/// projection. Native post-state is never consulted while constructing the
/// engine input or executing Step.
/// </summary>
public sealed record NativeEngineFieldCheckV111(
    string Path, string ExpectedNative, string PredictedEmulator, bool Equal);

public sealed record NativeEngineFidelityReportV111(
    string Status,
    string NativeGameParity,
    bool ExecutedActualEngineStep,
    string? NativeBuild,
    string[] Blockers,
    string[] UnverifiedPreconditions,
    NativeEngineFieldCheckV111[] CheckedFields,
    string? FirstMismatchPath,
    string? EngineFailure = null)
{
    public static NativeEngineFidelityReportV111 Stop(
        string status, string? build, IEnumerable<string> blockers,
        IEnumerable<string>? assumptions = null, string? error = null) =>
        new(status, "NOT_TESTED", false, build, blockers.ToArray(),
            assumptions?.ToArray() ?? [], [], null, error);
}

/// <summary>
/// Strict, read-only conversion of a native Inspector fixture into a
/// disposable v111 engine COMBAT sandbox. Does not replay the original seed,
/// reconstruct unknown relic/potion/enchantment hooks, or verify event order.
/// Unknown native card/monster identities are hard blockers, not fake enemies.
/// </summary>
public static class NativeEngineFidelityV111
{
    private const string FixtureSchema = "native-expectation-v1";
    private static readonly string[] HiddenFields =
    [
        "$.combat.pending_choice",
        "$.combat.rng_state",
        "$.inventory.relics",
        "$.inventory.potions",
        "$.combat.move_history",
        "$.combat.hidden_card_state",
        "UNVERIFIED_COMBAT_ENTRY_AND_PRIOR_HOOKS",
        "UNVERIFIED_NATIVE_EVENT_ORDER"
    ];

    public static NativeEngineFidelityReportV111 Compare(
        JsonElement fixture, ActIdentity region = ActIdentity.Overgrowth,
        int ascension = 0)
    {
        string? build = null;
        try
        {
            if (fixture.ValueKind != JsonValueKind.Object ||
                ReadString(fixture, "fixture_schema") != FixtureSchema ||
                ReadString(fixture, "status") != "RECORDED_NATIVE_EXPECTATION")
                return NativeEngineFidelityReportV111.Stop(
                    "INVALID_FIXTURE", null,
                    ["Requires a structurally complete native-expectation-v1 fixture."]);
            if (Get(fixture, "native_trace_header") is { } header)
                build = ReadString(header, "game_build");

            if (Get(fixture, "pre_state") is not { } before ||
                Get(fixture, "post_state") is not { } after ||
                Get(fixture, "selected_action") is not { } nativeAction)
                return NativeEngineFidelityReportV111.Stop(
                    "INVALID_FIXTURE", build, ["Missing pre_state/post_state/selected_action."]);
            if (ReadString(nativeAction, "kind") != "play_card")
                return NativeEngineFidelityReportV111.Stop(
                    "UNSUPPORTED_NATIVE_ACTION", build,
                    ["This first actual-engine import only supports play_card."]);
            if (Get(nativeAction, "payload") is not { } payload ||
                ReadLong(payload, "card_instance_id") is not long cardId)
                return NativeEngineFidelityReportV111.Stop(
                    "INVALID_FIXTURE", build, ["Invalid native card instance ID."]);

            if (region is not (ActIdentity.Overgrowth or ActIdentity.Underdocks) ||
                ascension is < 0 or > 10)
                return NativeEngineFidelityReportV111.Stop(
                    "INVALID_CONFIGURATION", build,
                    ["Only Act 1 Overgrowth/Underdocks, ascension 0–10 are supported."]);

            var originalPlayer = Require(before, "player");
            var originalCombat = Require(before, "combat");
            var observedPlayer = Require(after, "player");
            var observedCombat = Require(after, "combat");

            // The independent native coverage verdict is authoritative.
            // Hidden continuation is NOT inferred from observed equality.
            var guards = new List<string>(HiddenFields);
            if (Get(fixture, "coverage") is { } coverage &&
                Get(coverage, "missing_continuation_fields") is { } missing &&
                missing.ValueKind == JsonValueKind.Array)
                guards.AddRange(missing.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!).Distinct(StringComparer.Ordinal));
            guards = guards.Distinct(StringComparer.Ordinal).ToList();

            var blockers = new List<string>();
            if (Get(originalPlayer, "powers") is not { } playerPowers ||
                playerPowers.ValueKind != JsonValueKind.Array || playerPowers.GetArrayLength() != 0)
                blockers.Add("UNSUPPORTED_PLAYER_POWERS");
            var creatures = Get(originalCombat, "creatures");
            if (creatures is null || creatures.Value.ValueKind != JsonValueKind.Array)
                blockers.Add("MISSING_NATIVE_CREATURES");

            var coverageMap = V111Coverage.Create().Items;
            string Normalize(string s) => new(
                s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            string? Map(string nativeId, string kind)
            {
                // Require a unique ID in the pinned source inventory and
                // executable registry; name similarity alone is insufficient.
                var matches = coverageMap.Where(x => x.Kind == kind &&
                    Normalize(x.NativeId) == Normalize(nativeId) &&
                    x.EngineIds.Length == 1 && x.Registered &&
                    x.Scope == "single-player").ToArray();
                return matches.Length == 1 ? matches[0].EngineIds[0] : null;
            }

            var combatCards = new List<CombatCardInstance>();
            var ids = new HashSet<long>();
            var zoneCards = new Dictionary<string, long[]>(StringComparer.Ordinal);
            var originalCardModels = new Dictionary<long, (string Model, int Upgrades, int Cost)>();
            foreach (string zone in new[] { "hand", "draw_pile", "discard_pile", "exhaust_pile" })
            {
                if (Get(originalCombat, zone) is not { } list ||
                    list.ValueKind != JsonValueKind.Array)
                {
                    blockers.Add("MISSING_NATIVE_ZONE:" + zone);
                    continue;
                }
                var zoneIds = new List<long>();
                foreach (var nativeCard in list.EnumerateArray())
                {
                    long id = RequireLong(nativeCard, "instance_id");
                    string model = RequireString(nativeCard, "model_id");
                    int upgrade = RequireInt(nativeCard, "upgrades");
                    int cost = RequireInt(nativeCard, "cost");
                    if (!ids.Add(id)) blockers.Add("DUPLICATE_CARD_ID:" + id);
                    string? mapped = Map(model, "cards");
                    if (mapped is null)
                    {
                        blockers.Add("UNSUPPORTED_NATIVE_CARD:" + model);
                        continue;
                    }
                    var definition = PrototypeContent.Card(mapped);
                    if (!definition.MechanicsImplemented)
                        blockers.Add("UNIMPLEMENTED_CARD:" + model);
                    if (upgrade < 0 || upgrade > definition.MaxUpgradeLevel)
                        blockers.Add("UNSUPPORTED_UPGRADE:" + model);
                    // If the card's visible cost differs from the native
                    // base cost, we cannot reconstruct hidden modifiers.
                    if (definition.Cost.Kind != PrototypeCardCostKind.Fixed ||
                        cost != definition.Cost.Amount)
                        blockers.Add("UNVERIFIED_CARD_COST:" + model + ":" + id);
                    combatCards.Add(new CombatCardInstance(id, null, mapped, upgrade,
                        false, PrototypeJson.EmptyObject()));
                    originalCardModels[id] = (model, upgrade, cost);
                    zoneIds.Add(id);
                }
                zoneCards[zone] = zoneIds.ToArray();
            }

            var enemies = new List<EnemyCombatState>();
            var idsToEnemy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (creatures is { ValueKind: JsonValueKind.Array })
            {
                foreach (var nativeCreature in creatures.Value.EnumerateArray())
                {
                    string nativeId = RequireString(nativeCreature, "id");
                    string nativeModel = RequireString(nativeCreature, "model_id");
                    if (nativeModel == "SILENT" && nativeId == "creature:0")
                        continue;
                    if (!nativeId.StartsWith("creature:", StringComparison.Ordinal) ||
                        !int.TryParse(nativeId.AsSpan("creature:".Length),
                            System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out int enemyId) || enemyId <= 0 ||
                        !idsToEnemy.TryAdd(nativeId, enemyId))
                    {
                        blockers.Add("INVALID_NATIVE_CREATURE_ID:" + nativeId);
                        continue;
                    }
                    string? mapped = Map(nativeModel, "monsters");
                    if (mapped is null)
                    {
                        blockers.Add("UNSUPPORTED_NATIVE_MONSTER:" + nativeModel);
                        continue;
                    }
                    if (Get(nativeCreature, "powers") is not { } powers ||
                        powers.ValueKind != JsonValueKind.Array || powers.GetArrayLength() != 0)
                        blockers.Add("UNSUPPORTED_ENEMY_POWERS:" + nativeId);
                    enemies.Add(new EnemyCombatState(
                        InstanceId: enemyId, EnemyId: mapped,
                        Hp: RequireInt(nativeCreature, "hp"),
                        MaxHp: RequireInt(nativeCreature, "max_hp"),
                        Block: RequireInt(nativeCreature, "block"),
                        MoveIndex: 0, Statuses: new Dictionary<string, int>(),
                        Powers: []));
                }
            }
            if (Get(payload, "target_id") is { } targetEl &&
                targetEl.ValueKind == JsonValueKind.String &&
                !idsToEnemy.ContainsKey(targetEl.GetString()!))
                blockers.Add("UNKNOWN_ACTION_TARGET:" + targetEl.GetString());
            if (Get(originalCombat, "energy") is not { } energy ||
                Get(originalCombat, "turn") is not { } turn)
                blockers.Add("MISSING_ENERGY_OR_TURN");
            if (blockers.Count > 0)
                return NativeEngineFidelityReportV111.Stop(
                    "UNSUPPORTED_NATIVE_STATE", build, blockers.Distinct(), guards);

            // Create a genuine engine state with a legitimate configured
            // world/map; then inject ONLY observable combat fields. This
            // isolates the card rules without pretending to know native
            // RNG or the original full run history.
            var engine = new PrototypeGameEngine();
            var start = engine.Step(V111RunFactory.Create(
                    "native-fixture-isolated-engine-sandbox",
                    RunConfiguration.Create(ascension: ascension, firstAct: region)),
                GameAction.Empty("start_run")).State;
            var combat = new CombatState(
                Turn: RequireInt(originalCombat, "turn"),
                Energy: RequireInt(originalCombat, "energy"),
                PlayerBlock: RequireInt(originalPlayer, "block"),
                Hand: zoneCards["hand"],
                DrawPile: zoneCards["draw_pile"],
                DiscardPile: zoneCards["discard_pile"],
                ExhaustPile: zoneCards["exhaust_pile"],
                Enemies: enemies.ToArray(),
                NextCardInstanceId: ids.Max() + 1,
                Cards: combatCards.ToArray(),
                PlayerPowers: [],
                NextPowerApplicationOrder: 1);
            var state = start with
            {
                Phase = RunPhase.Combat,
                Player = start.Player with
                {
                    Hp = RequireInt(originalPlayer, "hp"),
                    MaxHp = RequireInt(originalPlayer, "max_hp"),
                    Gold = RequireInt(originalPlayer, "gold"),
                    Relics = [],
                    PotionSlots = Array.Empty<PotionInstance?>()
                },
                World = start.World! with
                {
                    ActiveRoom = PrototypeRoomType.Combat,
                    Combat = combat
                }
            };
            int? enemyTarget = Get(payload, "target_id") is { } rawTarget &&
                rawTarget.ValueKind == JsonValueKind.String
                ? idsToEnemy[rawTarget.GetString()!] : null;
            var action = GameAction.Create("play_card",
                new PlayCardPayload(cardId, enemyTarget));
            if (!engine.GetLegalActions(state).Any(legal =>
                legal.Kind == "play_card" &&
                legal.ReadPayload<PlayCardPayload>() == new PlayCardPayload(cardId, enemyTarget)))
                return NativeEngineFidelityReportV111.Stop(
                    "EMULATOR_REJECTS_NATIVE_ACTION", build,
                    ["The native selected action is not legal in this isolated emulator state."],
                    guards);

            // This is the actual, unchanged combat engine step, NOT an
            // independent Strike shortcut or a copied native post-state.
            var result = engine.Step(state, action).State;
            if (result.Phase != RunPhase.Combat || result.World?.Combat is null)
                return new NativeEngineFidelityReportV111(
                    "ENGINE_PHASE_DIVERGENCE", "NOT_TESTED", true, build,
                    ["Engine left combat before the next native checkpoint."],
                    guards.ToArray(), [], "$.phase");
            var next = result.World.Combat;
            var checks = new List<NativeEngineFieldCheckV111>();
            void Check(string path, object native, object emulated)
            {
                string expected = JsonSerializer.Serialize(native);
                string predicted = JsonSerializer.Serialize(emulated);
                checks.Add(new(path, expected, predicted, expected == predicted));
            }
            Check("$.player.hp", RequireInt(observedPlayer, "hp"), result.Player.Hp);
            Check("$.player.max_hp", RequireInt(observedPlayer, "max_hp"), result.Player.MaxHp);
            Check("$.player.gold", RequireInt(observedPlayer, "gold"), result.Player.Gold);
            Check("$.player.block", RequireInt(observedPlayer, "block"), next.PlayerBlock);
            Check("$.combat.turn", RequireInt(observedCombat, "turn"), next.Turn);
            Check("$.combat.energy", RequireInt(observedCombat, "energy"), next.Energy);

            var afterCreatures = Require(observedCombat, "creatures");
            foreach (var nativeCreature in afterCreatures.EnumerateArray())
            {
                string nativeId = RequireString(nativeCreature, "id");
                var enemy = idsToEnemy.TryGetValue(nativeId, out var id)
                    ? next.Enemies.Single(e => e.InstanceId == id)
                    : null;
                string path = "$.combat.creatures[" + nativeId + "]";
                int hp = enemy?.Hp ?? result.Player.Hp;
                int maxHp = enemy?.MaxHp ?? result.Player.MaxHp;
                int block = enemy?.Block ?? next.PlayerBlock;
                Check(path + ".hp", RequireInt(nativeCreature, "hp"), hp);
                Check(path + ".max_hp", RequireInt(nativeCreature, "max_hp"), maxHp);
                Check(path + ".block", RequireInt(nativeCreature, "block"), block);
                Check(path + ".alive", RequireBool(nativeCreature, "alive"), hp > 0);
                if (Require(nativeCreature, "powers").GetArrayLength() != 0 ||
                    (enemy?.Powers?.Length ?? next.PlayerPowers.Length) != 0)
                    guards.Add("POWER_SEMANTICS_UNVERIFIED:" + nativeId);
            }

            foreach (string zone in zoneCards.Keys)
            {
                var nativeZone = Require(observedCombat, zone);
                var nativeIds = nativeZone.EnumerateArray()
                    .Select(card => RequireLong(card, "instance_id")).ToArray();
                var engineIds = zone switch
                {
                    "hand" => next.Hand,
                    "draw_pile" => next.DrawPile,
                    "discard_pile" => next.DiscardPile,
                    _ => next.ExhaustPile
                };
                Check("$.combat." + zone + ".card_instances", nativeIds, engineIds);
                // Verify stable card identities/upgrades as well as zone order.
                foreach (var nativeCard in nativeZone.EnumerateArray())
                {
                    var id = RequireLong(nativeCard, "instance_id");
                    var runtime = next.Cards.SingleOrDefault(card => card.InstanceId == id);
                    string p = "$.combat." + zone + "[" + id + "]";
                    Check(p + ".model_id", RequireString(nativeCard, "model_id"),
                        runtime is null ? "NOT_IN_ENGINE" :
                        originalCardModels.TryGetValue(id, out var original) &&
                        runtime.CardId == Map(original.Model, "cards")
                            ? original.Model : "CHANGED_OR_UNKNOWN_MODEL");
                    Check(p + ".upgrades", RequireInt(nativeCard, "upgrades"),
                        runtime?.UpgradeLevel ?? -1);
                }
            }

            var differences = checks.Where(x => !x.Equal).ToArray();
            return new NativeEngineFidelityReportV111(
                differences.Length == 0
                    ? "CORE_ENGINE_CHECKPOINT_CONSISTENT"
                    : "CORE_ENGINE_CHECKPOINT_MISMATCH",
                "NOT_TESTED", true, build, [], guards.ToArray(),
                checks.ToArray(), differences.FirstOrDefault()?.Path);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException
                                  or ArgumentException or KeyNotFoundException)
        {
            return NativeEngineFidelityReportV111.Stop(
                "IMPORT_OR_ENGINE_ERROR", build,
                ["Fixture import or real engine continuation failed."],
                HiddenFields, e.GetType().Name + ": " + e.Message);
        }
    }

    private static JsonElement Require(JsonElement obj, string name) =>
        Get(obj, name) ?? throw new JsonException("Missing field: " + name);
    private static JsonElement? Get(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var prop) ? prop : null;
    private static string RequireString(JsonElement obj, string key) =>
        ReadString(obj, key) ?? throw new JsonException("Missing string: " + key);
    private static string? ReadString(JsonElement obj, string key)
    {
        var field = Get(obj, key);
        return field is { ValueKind: JsonValueKind.String } ? field.Value.GetString() : null;
    }
    private static long RequireLong(JsonElement obj, string key) =>
        ReadLong(obj, key) ??
        throw new JsonException("Missing integer: " + key);
    private static long? ReadLong(JsonElement obj, string key)
    {
        var value = Get(obj, key);
        return value is { ValueKind: JsonValueKind.Number } &&
            value.Value.TryGetInt64(out var result) ? result : null;
    }
    private static int RequireInt(JsonElement obj, string key) =>
        checked((int)RequireLong(obj, key));
    private static bool RequireBool(JsonElement obj, string key)
    {
        var value = Require(obj, key);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new JsonException("Expected boolean: " + key)
        };
    }
}
