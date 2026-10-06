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
