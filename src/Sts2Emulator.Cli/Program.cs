using System.Text.Json;
using Sts2Emulator.Core;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine("STS2 Emulator developer CLI");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  doctor                 Print runtime and implementation status");
    Console.WriteLine("  hash-demo              Build a tiny synthetic canonical state and hash it");
    Console.WriteLine("  prototype-run [seed]   Drive the restrictive Silent prototype to terminal state");
    Console.WriteLine("  prototype-sweep [n]    Run a deterministic smoke policy over many seeds");
    Console.WriteLine("  prototype-manifest     Print the machine-readable prototype capability manifest");
    Console.WriteLine("  prototype-ai-jsonl     Run the long-lived prototype AI JSONL bridge on stdin/stdout");
    Console.WriteLine("  reference-preflight <game-dir> [data-dir]");
    Console.WriteLine("                          Fingerprint the exact installed STS2 build");
    Console.WriteLine("  reference-inspect <game-dir> <pattern> [data-dir]");
    Console.WriteLine("                          Search managed type/method metadata in installed sts2.dll");
    Console.WriteLine("  reference-probe-summary <probe.jsonl>");
    Console.WriteLine("                          Summarize a passive native reference probe");
    Console.WriteLine("  reference-probe-audit <probe.jsonl>");
    Console.WriteLine("                          Audit history/state/RNG shapes for parity work");
    return;
}

switch (args[0])
{
    case "doctor":
        Console.WriteLine($"Runtime: {Environment.Version}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine("Engine status: restrictive Silent whole-run prototype is available.");
        Console.WriteLine("Fidelity status: prototype rules/content/RNG; no native parity claim.");
        break;

    case "hash-demo":
        using (var doc = JsonDocument.Parse("{}"))
        {
            var empty = doc.RootElement.Clone();
            var state = new RunState(
                GameBuild: "unbound",
                EmulatorSchema: "0.1",
                RunId: "demo",
                RunSeed: "0",
                DecisionIndex: 0,
                Phase: RunPhase.RunStart,
                Player: new PlayerState(
                    80,
                    80,
                    99,
                    Array.Empty<CardInstance>(),
                    Array.Empty<RelicInstance>(),
                    Array.Empty<PotionInstance?>()),
                Rng: RngBundle.Empty,
                ExtensionState: empty);

            Console.WriteLine(CanonicalJson.Sha256(state));
        }
        break;

    case "reference-probe-summary":
    {
        if (args.Length != 2)
        {
            throw new ArgumentException(
                "reference-probe-summary requires exactly one JSONL path.");
        }

        var summary = Sts2Emulator.Trace.ReferenceProbeAnalyzer.Analyze(args[1]);
        Console.WriteLine($"Schema: {summary.Schema}");
        Console.WriteLine($"Build fingerprint: {summary.BuildFingerprint}");
        Console.WriteLine(
            $"Records: {summary.ValidRecords} valid, {summary.InvalidRecords} invalid, {summary.Lines} lines");
        Console.WriteLine($"Combats: {summary.CombatCount}");
        Console.WriteLine($"Type catalogs: {summary.TypeCatalogCount}");
        Console.WriteLine(
            $"Sequence health: {summary.SequencedRecordCount} sequenced, " +
            $"{summary.SequenceRegressionCount} regressions, " +
            $"{summary.DuplicateSequenceCount} duplicates");

        Console.WriteLine("Record types:");
        foreach (var item in summary.RecordTypes)
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine("Boundaries:");
        foreach (var item in summary.Boundaries)
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine("Diagnostics:");
        foreach (var item in summary.Diagnostics)
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        break;
    }

    case "reference-probe-audit":
    {
        if (args.Length != 2)
        {
            throw new ArgumentException(
                "reference-probe-audit requires exactly one JSONL path.");
        }

        var audit = Sts2Emulator.Trace.ReferenceProbeAuditor.Analyze(args[1]);
        Console.WriteLine($"Boundaries: {audit.BoundaryCount}");
        Console.WriteLine(
            $"History payloads: {audit.HistoryPayloadCount}/{audit.HistoryBoundaryCount}");
        Console.WriteLine(
            $"Player creature coverage: {audit.PlayerCreatureSnapshotCount}/{audit.PlayerSnapshotCount}");
        Console.WriteLine(
            $"RNG snapshots: run={audit.RunRngSnapshotCount}, player={audit.PlayerRngSnapshotCount}");

        Console.WriteLine("History entry shapes:");
        foreach (var shape in audit.HistoryEntryShapes)
        {
            Console.WriteLine(
                $"  {shape.RuntimeType}: count={shape.Count}, " +
                $"state_changed={shape.StateChangedCount}, " +
                $"state_preserved={shape.StatePreservedCount}, " +
                $"distinct_hashes={shape.DistinctStateHashes}");
            Console.WriteLine(
                $"    payload_paths: {string.Join(", ", shape.PayloadPaths)}");
        }

        Console.WriteLine("RNG payload shapes:");
        foreach (var shape in audit.RngShapes)
        {
            Console.WriteLine(
                $"  {shape.Scope} {shape.RuntimeType}: count={shape.Count}");
            Console.WriteLine(
                $"    payload_paths: {string.Join(", ", shape.PayloadPaths)}");
        }

        Console.WriteLine("Runtime type catalogs:");
        foreach (var catalog in audit.TypeCatalogs)
        {
            Console.WriteLine($"  {catalog.RuntimeType}");
            Console.WriteLine(
                $"    properties: {string.Join(", ", catalog.Properties)}");
            Console.WriteLine(
                $"    fields: {string.Join(", ", catalog.Fields)}");
        }

        break;
    }

    case "reference-inspect":
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "reference-inspect requires <game-dir> <pattern> and optionally [data-dir].");
        }

        var manifest = Sts2Emulator.Trace.ReferenceBuildFingerprint.Capture(
            args[1],
            args.Length >= 4 ? args[3] : null);
        var assemblyPath = Path.Combine(manifest.DataDirectory, "sts2.dll");
        var matches = Sts2Emulator.Trace.ReferenceAssemblyInspector.Search(
            assemblyPath,
            args[2]);

        Console.WriteLine($"Build fingerprint: {manifest.BuildFingerprint}");
        Console.WriteLine($"Matches: {matches.Length}");
        foreach (var match in matches)
        {
            Console.WriteLine(
                match.MethodName is null
                    ? $"type\t{match.TypeName}"
                    : $"method\t{match.TypeName}\t{match.MethodName}\tparams={match.ParameterCount}");
        }

        break;
    }

    case "reference-preflight":
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "reference-preflight requires <game-dir> and optionally [data-dir].");
        }

        var manifest = Sts2Emulator.Trace.ReferenceBuildFingerprint.Capture(
            args[1],
            args.Length >= 3 ? args[2] : null);
        Console.WriteLine(CanonicalJson.Serialize(manifest));
        break;
    }

    case "prototype-manifest":
        Console.WriteLine(CanonicalJson.Serialize(PrototypeCapabilities.Create()));
        break;

    case "prototype-ai-jsonl":
        Sts2Emulator.Cli.PrototypeAiJsonlServer.Run(Console.In, Console.Out);
        break;

    case "prototype-run":
    {
        var seed = args.Length >= 2 ? args[1] : "demo";
        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(seed);
        var phases = new HashSet<RunPhase> { state.Phase };

        for (var step = 0; step < 5_000 && state.Phase != RunPhase.Terminal; step++)
        {
            var legal = engine.GetLegalActions(state);
            if (legal.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Prototype is stuck in {state.Phase} with no legal actions.");
            }

            var action = ChoosePrototypeAction(state, legal);
            state = engine.Step(state, action).State;
            phases.Add(state.Phase);
        }

        if (state.Phase != RunPhase.Terminal)
        {
            throw new InvalidOperationException("Prototype run exceeded the 5,000-step smoke limit.");
        }

        Console.WriteLine($"Seed: {seed}");
        Console.WriteLine($"Outcome: {state.World?.TerminalOutcome}");
        Console.WriteLine($"Decisions: {state.DecisionIndex}");
        Console.WriteLine($"Act/Floor: {state.World?.Act}/{state.World?.Floor}");
        Console.WriteLine($"HP: {state.Player.Hp}/{state.Player.MaxHp}");
        Console.WriteLine($"Deck: {state.Player.Deck.Length} cards");
        Console.WriteLine($"Phases seen: {string.Join(", ", phases.OrderBy(phase => phase))}");
        break;
    }

    case "prototype-sweep":
    {
        var runCount = 100;
        if (args.Length >= 2
            && (!int.TryParse(args[1], out runCount) || runCount <= 0))
        {
            throw new ArgumentException("prototype-sweep count must be a positive integer.");
        }

        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        var encounterCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var eventCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var cardCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var relicCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var terminalActs = new Dictionary<int, int>();
        long totalDecisions = 0;
        var maxDecisions = 0L;
        var totalDeckSize = 0L;
        var totalMaxHp = 0L;

        for (var runIndex = 0; runIndex < runCount; runIndex++)
        {
            var seed = $"sweep-{runIndex}";
            var engine = new PrototypeGameEngine();
            var state = PrototypeGameFactory.Create(seed);

            for (var step = 0; step < 5_000 && state.Phase != RunPhase.Terminal; step++)
            {
                var legal = engine.GetLegalActions(state);
                if (legal.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Prototype sweep stuck on seed {seed} in {state.Phase}.");
                }

                state = engine.Step(state, ChoosePrototypeAction(state, legal)).State;
                PrototypeStateInvariants.Validate(state);
            }

            if (state.Phase != RunPhase.Terminal)
            {
                throw new InvalidOperationException(
                    $"Prototype sweep seed {seed} exceeded 5,000 decisions.");
            }

            var world = state.World
                ?? throw new InvalidOperationException("Terminal prototype run has no world state.");
            var outcome = world.TerminalOutcome ?? "unknown";
            outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
            terminalActs[world.Act] = terminalActs.GetValueOrDefault(world.Act) + 1;

            foreach (var encounterId in world.EncounterIds)
            {
                encounterCounts[encounterId] = encounterCounts.GetValueOrDefault(encounterId) + 1;
            }

            foreach (var eventId in world.EventIds)
            {
                eventCounts[eventId] = eventCounts.GetValueOrDefault(eventId) + 1;
            }

            foreach (var card in state.Player.Deck)
            {
                if (PrototypeContent.Card(card.CardId).Rarity == PrototypeCardRarity.Basic)
                {
                    continue;
                }

                cardCounts[card.CardId] = cardCounts.GetValueOrDefault(card.CardId) + 1;
            }

            foreach (var relic in state.Player.Relics)
            {
                relicCounts[relic.RelicId] = relicCounts.GetValueOrDefault(relic.RelicId) + 1;
            }

            totalDecisions += state.DecisionIndex;
            maxDecisions = Math.Max(maxDecisions, state.DecisionIndex);
            totalDeckSize += state.Player.Deck.Length;
            totalMaxHp += state.Player.MaxHp;
        }

        Console.WriteLine($"Runs: {runCount}");
        Console.WriteLine(
            $"Outcomes: {string.Join(", ", outcomes.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}");
        Console.WriteLine($"Average decisions: {(double)totalDecisions / runCount:F1}");
        Console.WriteLine($"Max decisions: {maxDecisions}");
        Console.WriteLine($"Average terminal deck size: {(double)totalDeckSize / runCount:F1}");
        Console.WriteLine($"Average terminal max HP: {(double)totalMaxHp / runCount:F1}");
        Console.WriteLine(
            $"Terminal acts: {string.Join(", ", terminalActs.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}");
        Console.WriteLine("Encounter counts:");
        foreach (var item in encounterCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine("Event counts:");
        foreach (var item in eventCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine("Terminal non-basic card counts:");
        foreach (var item in cardCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine("Terminal relic counts:");
        foreach (var item in relicCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine(
            $"Distinct terminal non-basic cards: {cardCounts.Count}/{PrototypeContent.RewardCardPool.Length}");
        Console.WriteLine(
            $"Distinct terminal relics: {relicCounts.Count}/{PrototypeContent.RelicPool.Length + 1}");

        break;
    }

    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        Environment.ExitCode = 2;
        break;
}

static GameAction ChoosePrototypeAction(
    RunState state,
    IReadOnlyList<GameAction> legal)
{
    if (state.Phase == RunPhase.Rest)
    {
        if (state.Player.Hp * 2 < state.Player.MaxHp)
        {
            return legal.First(action => action.Kind == "rest_heal");
        }

        return legal.FirstOrDefault(action => action.Kind == "rest_upgrade")
            ?? legal[0];
    }

    if (state.Phase == RunPhase.Shop)
    {
        return legal.FirstOrDefault(action => action.Kind == "buy_relic")
            ?? legal.FirstOrDefault(action => action.Kind == "buy_card")
            ?? legal.FirstOrDefault(action => action.Kind == "buy_potion")
            ?? legal.First(action => action.Kind == "leave_shop");
    }

    if (state.Phase == RunPhase.Combat)
    {
        return legal.FirstOrDefault(action => action.Kind == "select_cards")
            ?? legal.FirstOrDefault(action => action.Kind == "use_potion")
            ?? legal.FirstOrDefault(action => action.Kind == "play_card")
            ?? legal.FirstOrDefault(action => action.Kind == "end_turn")
            ?? legal[0];
    }

    return legal[0];
}
