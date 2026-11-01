using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

/// <summary>
/// One genuine original-game v111 passive read-only observation, reduced to
/// mechanics-only data: fixed public run seed, map node/edge labels and
/// content-local RNG cursor. No user identifiers or raw trace are included.
/// These are SOURCE observations, not derived emulator expectations.
/// </summary>
public sealed class NativeObservedMapV111Tests
{
    private const string CapturedSeed = "6JK8MD0VQQTA";
    private const string CapturedMap = """
        1:0:Mo:01 1:2:Mo:2 1:4:Mo:4 1:6:Mo:6
        2:0:Sh:0 2:1:Un:1 2:2:Mo:2 2:4:Mo:34 2:6:Mo:6
        3:0:Mo:0 3:1:Mo:0 3:2:Mo:2 3:3:Un:24 3:4:Mo:4 3:6:Un:6
        4:0:Un:01 4:2:Mo:3 4:4:Sh:34 4:6:Un:6
        5:0:Sh:0 5:1:Mo:1 5:3:Mo:3 5:4:Mo:5 5:6:Mo:6
        6:0:Re:0 6:1:Mo:1 6:3:Mo:34 6:5:Un:6 6:6:El:6
        7:0:Mo:0 7:1:Un:0 7:3:El:3 7:4:Re:3 7:6:Mo:6
        8:0:Re:01 8:3:Mo:3 8:6:Mo:56
        9:0:Tr:0 9:1:Tr:1 9:3:Tr:3 9:5:Tr:4 9:6:Tr:6
        10:0:Mo:0 10:1:Mo:1 10:3:El:34 10:4:Re:4 10:6:Un:6
        11:0:Mo:0 11:1:Re:2 11:3:Un:24 11:4:Mo:45 11:6:Mo:6
        12:0:El:0 12:2:Un:12 12:4:Re:45 12:5:Mo:5 12:6:Mo:6
        13:0:Un:0 13:1:Mo:1 13:2:El:1 13:4:Mo:4 13:5:Un:45 13:6:El:6
        14:0:El:0 14:1:Un:0 14:4:El:3 14:5:Mo:5 14:6:Mo:6
        15:0:Re:3 15:3:Re:3 15:5:Re:3 15:6:Re:3
        16:3:Bo:
        """;

    private static string Code(PrototypeRoomType room) => room switch
    {
        PrototypeRoomType.Combat => "Mo",
        PrototypeRoomType.Elite => "El",
        PrototypeRoomType.Shop => "Sh",
        PrototypeRoomType.Rest => "Re",
        PrototypeRoomType.Unknown => "Un",
        PrototypeRoomType.Treasure => "Tr",
        PrototypeRoomType.Boss => "Bo",
        _ => throw new InvalidDataException($"Unsupported observed map room type {room}.")
    };

    private static string CanonicalNode(MapNodeState n)
    {
        var parts = n.NodeId.Split(':');
        return $"{parts[1]}:{parts[2]}:{Code(n.RoomType)}:" +
            string.Concat((n.NextNodeIds ?? [])
                .Select(id => int.Parse(id.Split(':')[2]))
                .OrderBy(column => column));
    }

    [Fact]
    public void GenuineNativeMapAndLocalRngCursorMatchIndependentEmulator()
    {
        var predicted = NativeStandardActMapV111.Generate(CapturedSeed, 10);
        var observed = CapturedMap.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);
        var actual = predicted.Map.Nodes.Select(CanonicalNode).ToArray();
        var expectedRng = new NativeRngSnapshot(
            272, 16316752623071141400UL, 18394116595804591602UL,
            3801399490095464743UL, 11579647621899964746UL);
        var firstDifference = Enumerable.Range(0, Math.Max(observed.Length, actual.Length))
            .Select(index => new {
                Index = index,
                Expected = index < observed.Length ? observed[index] : "<missing>",
                Actual = index < actual.Length ? actual[index] : "<missing>"
            })
            .FirstOrDefault(value => value.Expected != value.Actual);

        var snapshot = predicted.FinalRng;
        var diagnostic = $"native_nodes={observed.Length}, emulator_nodes={actual.Length}; " +
            $"native_map_rng={expectedRng}, emulator_map_rng={snapshot}; " +
            $"first_map_difference={firstDifference}; " +
            $"rng_checkpoints={predicted.AfterQuotas.Counter}," +
            $"{predicted.AfterPaths.Counter},{predicted.AfterTypes.Counter}," +
            $"{predicted.AfterPruning.Counter}; " +
            $"quotas=rest{predicted.RestQuota},unknown{predicted.UnknownQuota}," +
            $"elite{predicted.EliteQuota}";

        Assert.True(firstDifference is null && expectedRng == snapshot, diagnostic);
    }

    [Fact]
    public void GenuineNativeNeowOfferAndEventLocalCursorMatchOneExplicitUnlockProfile()
    {
        var observed = new[] {"PHIAL_HOLSTER", "LOST_COFFER", "SILVER_CRUCIBLE"};
        var expectedRng = new NativeRngSnapshot(
            19, 7953574168331048626UL, 3395371163224860218UL,
            16899011179517407643UL, 6951990959699619831UL);
        var predicted = NativeNeowOfferV111.PredictAll(CapturedSeed);
        // The native mod reports public relic model identifiers, while the
        // emulator uses internal proto.native.neow.* identifiers.
        // Compare canonical alphanumeric model-name tokens, preserving order.
        static string ModelToken(string id) => string.Concat(id.Split('.').Last()
            .Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant));
        var matches = predicted.Where(p =>
            p.OfferedRelicIds.Select(ModelToken)
                .SequenceEqual(observed.Select(ModelToken))
            && p.EventRng == expectedRng).ToArray();
        Assert.True(matches.Length > 0,
            "Native Neow option/cursor mismatch. Emulator variants: " +
            string.Join(" | ", predicted.Select(p =>
                $"k={p.KaleidoscopeAllowed},s={p.ScrollBoxesAllowed}:" +
                $"{string.Join(",", p.OfferedRelicIds)} rng={p.EventRng}")));
    }

    [Fact]
    public void GenuineNativeFirstCombatEntryShowsFirstVisibleDifference()
    {
        var configuration = RunConfiguration.Create("silent", 10, ActIdentity.Overgrowth)
            with { FidelityId = V111Build.NativeStandardMapOpeningFidelityId };
        var engine = new PrototypeGameEngine();
        var state = V111RunFactory.Create(CapturedSeed, configuration);
        state = engine.Step(state, engine.GetLegalActions(state).Single()).State;
        var firstNeowOptions = engine.GetLegalActions(state);
        var neow = firstNeowOptions.SingleOrDefault(a =>
            a.Kind == "event_choice"
            && a.Payload.GetRawText().Contains("take_proto.native.neow.phialholster",
                StringComparison.Ordinal));
        Assert.True(neow is not null,
            "Pinned native Neow selection is missing: " +
            string.Join(", ", firstNeowOptions.Select(a => a.Payload.GetRawText())));
        state = engine.Step(state, neow!).State;
        var acknowledgement = engine.GetLegalActions(state);
        if (acknowledgement.Count(a => a.Kind == "continue_event") == 1)
        {
            state = engine.Step(state,
                acknowledgement.Single(a => a.Kind == "continue_event")).State;
        }
        var mapOptions = engine.GetLegalActions(state);
        var leftmost = mapOptions.SingleOrDefault(a =>
            a.Kind == "choose_map_node"
            && a.Payload.GetRawText().Contains("1:1:0", StringComparison.Ordinal));
        Assert.True(leftmost is not null,
            "Pinned native visited node 1:1:0 not in emulator legal actions: " +
            string.Join(", ", mapOptions.Select(a => a.Payload.GetRawText())));
        state = engine.Step(state, leftmost!).State;
        var combat = state.World?.Combat;
        var encounter = state.World?.EncounterIds.LastOrDefault() ?? "<none>";
        var enemies = combat?.Enemies ?? [];
        var report = $"phase={state.Phase}; player_hp={state.Player.Hp}; " +
            $"gold={state.Player.Gold}; encounter={encounter}; enemies=" +
            string.Join(",", enemies.Select(e =>
                $"{e.EnemyId}(hp={e.Hp},max={e.MaxHp})")) +
            $"; native_persistent_run_rng=" +
            string.Join(",", state.NativeRunRng?.Streams.Select(s =>
                $"{s.StreamId}:{s.CallCount}") ?? []);
        static string Token(string id) => string.Concat(
            id.Split('.').Last().Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant));
        Assert.True(combat is not null && state.Phase == RunPhase.Combat
            && state.Player.Hp == 56 && state.Player.Gold == 99
            && Token(encounter) == "FUZZYWURMCRAWLERWEAK"
            && enemies.Length == 1
            && Token(enemies[0].EnemyId) == "FUZZYWURMCRAWLER"
            && enemies[0].Hp == 59,
            "Native observed first-combat boundary differs from emulator: " + report);

        // Native passive decision 1, *after* the first combat's opening draw.
        // Identifiers are normalized across game and emulator namespaces.
        var nativeHand = new[] {
            "STRIKE_SILENT", "NEUTRALIZE", "ASCENDERS_BANE",
            "DEFEND_SILENT", "STRIKE_SILENT", "DEFEND_SILENT",
            "STRIKE_SILENT" };
        var nativeDraw = new[] {
            "STRIKE_SILENT", "STRIKE_SILENT", "SURVIVOR",
            "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT" };
        // The prototype's shared starter IDs omit the character suffix.
        static string HandToken(string id) => Token(id) switch
        {
            "STRIKE" => "STRIKESILENT",
            "DEFEND" => "DEFENDSILENT",
            var other => other
        };
        var byInstance = combat!.Cards.ToDictionary(card => card.InstanceId);
        var actualHand = combat.Hand.Select(id => HandToken(byInstance[id].CardId)).ToArray();
        var actualDraw = combat.DrawPile.Select(id => HandToken(byInstance[id].CardId)).ToArray();

        // The original-game opening shuffle consumed twelve run.shuffle
        // calls, one native niche roll set Fuzzy Wurm's 59 HP, and the
        // resulting first seven drawn cards are visible in source decision 1.
        // The native draw pile is top-first, whereas the emulator's internal
        // pile is stored bottom-first.
        var shuffle = state.NativeRunRng!.Streams.Single(
            stream => stream.StreamId == "run.shuffle");
        var niche = state.NativeRunRng.Streams.Single(
            stream => stream.StreamId == "run.niche");
        Assert.Equal(new NativeRngSnapshot(
            12, 15433798454644501259UL, 6094479871964560181UL,
            5896905681693763820UL, 3755776457819266454UL),
            NativeRngV111.ToSnapshot(shuffle));
        Assert.Equal(new NativeRngSnapshot(
            1, 8812873255413119488UL, 9847610160209053945UL,
            13064119398137688314UL, 6715802103202815613UL),
            NativeRngV111.ToSnapshot(niche));
        Assert.True(actualHand.SequenceEqual(nativeHand.Select(HandToken)),
            "Native first hand differs; source=" +
            string.Join(",", nativeHand) + "; emulator=" +
            string.Join(",", actualHand) + "; " + report);
        Assert.True(actualDraw.Reverse()
            .SequenceEqual(nativeDraw.Select(HandToken)),
            "Native draw pile differs in top-first order; source=" +
            string.Join(",", nativeDraw) + "; emulator=" +
            string.Join(",", actualDraw.Reverse()) + "; " + report);

        // Run initialization still lacks the source relic-grab-bag
        // population (up_front=412 in this trace). Phial Holster's
        // pre-combat potion generation also consumed four calls in the
        // original game and is an independent pending semantic gate.
        Assert.Equal(0UL, state.NativeRunRng.Streams.Single(
            stream => stream.StreamId == "run.up_front").CallCount);
        Assert.Equal(new NativeRngSnapshot(
            4, 247483884377393130UL, 5718769650147259872UL,
            2262233218431618893UL, 7728349850348698365UL),
            NativeRngV111.ToSnapshot(state.NativeRunRng.Streams.Single(
                stream => stream.StreamId == "run.combat_potion_generation")));
        // Native passive snapshot does not expose potion identities.
        // These two IDs are independently derived from the pinned source
        // PotionFactory pool/rarity algorithm, so only their RNG cursor is
        // source-observed parity in this regression.
        Assert.Equal(
            ["proto.potion.fortifier", "proto.potion.vulnerable"],
            state.Player.PotionSlots.Where(potion => potion is not null)
                .Select(potion => potion!.PotionId).ToArray());
        Assert.Equal(3, state.Player.PotionSlots.Length);
    }

}
