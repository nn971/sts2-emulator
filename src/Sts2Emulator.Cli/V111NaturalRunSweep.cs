using System.Text.Json;
using Sts2Emulator.Core;

/// <summary>
/// Fixed, deliberately simple and fully legal Silent baseline policy.
/// No test-only combat finishers, HP edits, injected act boundaries, map
/// rewrites, RNG replacement, or forced encounter/boss selection.
/// This is a run-integrity canary, not an intelligent playing policy or
/// native-game differential test.
/// </summary>
internal static class V111NaturalRunSweep
{
    public sealed record ActCheckpoint(int Act, string File, string ExactHash);

    public sealed record RunResult(
        string Seed,
        string FirstAct,
        string Outcome,
        int TerminalAct,
        int TerminalFloor,
        int HighestAct,
        long Decisions,
        string FinalHash,
        string? Blocker,
        string? LastAction,
        string? LastEncounter,
        string[] RecentActions,
        int[] ActsEntered,
        ActCheckpoint[] Checkpoints);

    public sealed record Report(
        string Mode, string Policy, int Ascension, int RunsPerAct,
        int Completed, int Defeats, int Victories, int Blocked,
        int ReachedHive, int ReachedGlory, RunResult[] Runs);

    public static Report Run(int count, int ascension, int offset = 0,
        string? checkpointDirectory = null)
    {
        if (count is < 1 or > 1000 || offset < 0
            || ascension is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(count),
                "Count must be 1..1000; offset >= 0; ascension 0..10.");
        if (checkpointDirectory is not null)
            Directory.CreateDirectory(checkpointDirectory);
        var runs = new List<RunResult>(2 * count);
        foreach (var first in new[] { ActIdentity.Overgrowth, ActIdentity.Underdocks })
        {
            for (var i = offset; i < count + offset; i++)
            {
                var seed = $"natural-v111-{first.ToString().ToLowerInvariant()}-{i}";
                runs.Add(RunOne(seed, first, ascension,
                    checkpointDirectory));
            }
        }
        var result = runs.ToArray();
        return new Report(
            V111Build.ExperimentalSilentThreeActRouteFidelityId,
            "legal-actions-heuristic-v1", ascension, count,
            result.Count(run => run.Outcome is "victory" or "defeat"),
            result.Count(run => run.Outcome == "defeat"),
            result.Count(run => run.Outcome == "victory"),
            result.Count(run => run.Blocker is not null),
            result.Count(run => run.HighestAct >= 2),
            result.Count(run => run.HighestAct >= 3),
            result);
    }

    public static void Write(Report report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Console.WriteLine(JsonSerializer.Serialize(report, options));
    }

    public static void Write(RunResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }

    public static RunResult Resume(string snapshotPath)
    {
        var state = RunSnapshot.Load(File.ReadAllText(snapshotPath));
        if (state.Configuration?.UsesExperimentalSilentThreeActRoute != true
            || state.Configuration.CharacterId != "silent"
            || state.Phase == RunPhase.Terminal)
            throw new NotSupportedException(
                "Natural resume requires a nonterminal experimental v111 Silent checkpoint.");
        return Drive(state, state.RunSeed,
            state.Configuration.Acts[0], checkpointDirectory: null);
    }

    private static RunResult RunOne(
        string seed, ActIdentity firstAct, int ascension,
        string? checkpointDirectory)
    {
        var config = RunConfiguration.Create(
            ascension: ascension, firstAct: firstAct) with
        {
            FidelityId = V111Build.ExperimentalSilentThreeActRouteFidelityId
        };
        return Drive(V111RunFactory.Create(seed, config),
            seed, firstAct, checkpointDirectory);
    }

    private static RunResult Drive(
        RunState state, string seed, ActIdentity firstAct,
        string? checkpointDirectory)
    {
        var engine = new PrototypeGameEngine();
        var recent = new Queue<string>();
        string? lastAction = null;
        string? blocker = null;
        var maxAct = state.World?.Act ?? 1;
        var acts = new List<int> { maxAct };
        var checkpoints = new List<ActCheckpoint>();

        for (var step = 0; step < 5000
            && state.Phase != RunPhase.Terminal; step++)
        {
            try
            {
                PrototypeStateInvariants.Validate(state);
                var legal = engine.GetLegalActions(state);
                if (legal.Count == 0)
                    throw new InvalidOperationException(
                        "There are no legal actions at this nonterminal decision.");
                var chosen = PickAction(state, legal);
                lastAction = chosen.Kind;
                recent.Enqueue(
                    $"{state.World?.Act ?? 0}/{state.World?.Floor ?? 0}" +
                    $":{state.Phase}:{chosen.Kind}");
                if (recent.Count > 12)
                    recent.Dequeue();
                state = engine.Step(state, chosen).State;
                PrototypeStateInvariants.Validate(state);
                if (state.World is { Act: var act } && act > maxAct)
                {
                    maxAct = act;
                    acts.Add(act);
                    if (checkpointDirectory is not null)
                    {
                        var file = $"{seed}-act{act}-d{state.DecisionIndex}.snapshot.json";
                        var checkpoint = RunSnapshot.Save(state);
                        File.WriteAllText(Path.Combine(checkpointDirectory, file),
                            checkpoint);
                        var restored = RunSnapshot.Load(checkpoint);
                        checkpoints.Add(new ActCheckpoint(
                            act, file, CanonicalJson.Sha256(restored)));
                    }
                }
            }
            catch (Exception ex)
            {
                blocker = $"{ex.GetType().Name}: {ex.Message}";
                break;
            }
        }

        if (blocker is null && state.Phase != RunPhase.Terminal)
            blocker = "DecisionLimit: 5000 decisions exhausted without terminal state.";
        var world = state.World;
        return new RunResult(
            seed, firstAct.ToString(),
            blocker is null ? world?.TerminalOutcome ?? "unknown" : "blocked",
            world?.Act ?? 1, world?.Floor ?? 0, maxAct,
            state.DecisionIndex, CanonicalJson.Sha256(state),
            blocker, lastAction, world?.EncounterIds.LastOrDefault(),
            recent.ToArray(), acts.ToArray(), checkpoints.ToArray());
    }

    private static GameAction PickAction(
        RunState state, IReadOnlyList<GameAction> legal)
    {
        GameAction? Kind(string kind) =>
            legal.FirstOrDefault(a => a.Kind == kind);
        if (state.Phase == RunPhase.MapChoice)
        {
            // Prefer the safest legal room, never inventing a route:
            // combats and elite fights remain mandatory on some paths.
            var nodes = state.World!.Map.Nodes.ToDictionary(
                n => n.NodeId, StringComparer.Ordinal);
            static int Priority(PrototypeRoomType room) => room switch
            {
                PrototypeRoomType.Rest => 0,
                PrototypeRoomType.Treasure => 1,
                PrototypeRoomType.Shop => 2,
                PrototypeRoomType.Event => 3,
                PrototypeRoomType.Unknown => 4,
                PrototypeRoomType.Combat => 5,
                PrototypeRoomType.Elite => 6,
                PrototypeRoomType.Boss => 7,
                _ => 8
            };
            return legal.Where(a => a.Kind == "choose_map_node")
                .OrderBy(a => Priority(nodes[
                    a.ReadPayload<ChooseMapNodePayload>().NodeId].RoomType))
                .First();
        }

        if (state.Phase == RunPhase.Combat)
        {
            // Explicit enemy-owned choices and ordinary card-selection
            // continuations must be resolved before another card/turn.
            return Kind("choose_knowledge_curse")
                ?? Kind("select_cards")
                ?? Kind("play_card")
                ?? Kind("use_potion")
                ?? Kind("end_turn")
                ?? legal[0];
        }

        if (state.Phase == RunPhase.Shop)
            return Kind("leave_shop") ?? legal[0];

        if (state.Phase == RunPhase.Rest)
        {
            if (state.Player.Hp * 5 < state.Player.MaxHp * 4)
                return Kind("rest_heal") ?? legal[0];
            return Kind("rest_upgrade") ?? Kind("rest_heal") ?? legal[0];
        }

        if (state.Phase == RunPhase.Reward)
        {
            return Kind("take_reward_gold")
                ?? Kind("take_reward_relic")
                ?? Kind("take_reward_potion")
                ?? Kind("take_reward_card")
                ?? Kind("leave_reward")
                ?? legal[0];
        }

        return legal[0];
    }
}
