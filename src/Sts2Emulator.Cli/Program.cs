using System.Text.Json;
using Sts2Emulator.Core;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine("STS2 Emulator developer CLI");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  doctor   Print scaffold/runtime information");
    Console.WriteLine("  hash-demo  Build a tiny synthetic canonical state and hash it");
    Console.WriteLine("  native-basic-check <fixture.json> [--out <report.json>]");
    Console.WriteLine("             Provisional native Strike/Defend rule-level check (NOT parity)");
    return;
}

switch (args[0])
{
    case "doctor":
        Console.WriteLine($"Runtime: {Environment.Version}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine("Engine status: scaffold only; no STS2 rules are claimed implemented.");
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
                Player: new PlayerState(80, 80, 99, Array.Empty<CardInstance>(), Array.Empty<RelicInstance>(), Array.Empty<PotionInstance?>()),
                Rng: RngBundle.Empty,
                ExtensionState: empty);

            Console.WriteLine(CanonicalJson.Sha256(state));
        }
        break;

    case "native-basic-check":
        if (args.Length is not (2 or 4) ||
            args.Length == 4 && args[2] != "--out")
        {
            Console.Error.WriteLine(
                "Usage: native-basic-check <native-expectation.json> [--out <report.json>]");
            Environment.ExitCode = 2;
            break;
        }
        try
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(args[1]));
            var comparison = NativeBasicRuleComparer.Check(fixture.RootElement);
            var report = JsonSerializer.Serialize(comparison,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
            Console.WriteLine(report);
            if (args.Length == 4)
            {
                var output = Path.GetFullPath(args[3]);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, report + Environment.NewLine);
            }
            Environment.ExitCode = comparison.Status switch
            {
                "RULE_PROJECTION_CONSISTENT" => 0,
                "RULE_PROJECTION_MISMATCH" => 1,
                _ => 2
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                  or JsonException or ArgumentException)
        {
            Console.Error.WriteLine("Native basic-card rule check failed: " + e.Message);
            Environment.ExitCode = 2;
        }
        break;

    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        Environment.ExitCode = 2;
        break;
}
