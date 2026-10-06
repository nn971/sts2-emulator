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
        return legal.FirstOrDefault(action => action.Kind == "use_potion")
            ?? legal.FirstOrDefault(action => action.Kind == "play_card")
            ?? legal.First(action => action.Kind == "end_turn");
    }

    return legal[0];
}
