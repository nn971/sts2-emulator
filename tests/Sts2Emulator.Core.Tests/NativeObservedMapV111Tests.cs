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
    }

}
