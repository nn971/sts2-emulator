using System.Diagnostics;
using System.Text.Json;
using Sts2Emulator.Core;

var iterations = 100_000;
if (args.Length >= 1 && int.TryParse(args[0], out var parsed) && parsed > 0)
{
    iterations = parsed;
}

using var doc = JsonDocument.Parse("{\"prototype\":true,\"nested\":{\"x\":1,\"y\":2}}");
var empty = JsonDocument.Parse("{}").RootElement.Clone();
var deck = Enumerable.Range(0, 20)
    .Select(i => new CardInstance(i, $"card-{i % 5}", i % 2, empty))
    .ToArray();

var state = new RunState(
    "unbound",
    "0.1",
    "bench",
    "42",
    10,
    RunPhase.Combat,
    new PlayerState(55, 80, 170, deck, Array.Empty<RelicInstance>(), new PotionInstance?[3]),
    new RngBundle(new[]
    {
        new RngStreamState("combat", "opaque-v0", Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(), 123),
        new RngStreamState("misc", "opaque-v0", Enumerable.Range(0, 32).Select(i => (byte)(31 - i)).ToArray(), 45)
    }),
    doc.RootElement.Clone());

// Warmup
for (var i = 0; i < 1_000; i++) _ = state.Fork();
for (var i = 0; i < 100; i++) _ = CanonicalJson.Sha256(state);

var sw = Stopwatch.StartNew();
for (var i = 0; i < iterations; i++) _ = state.Fork();
sw.Stop();
Console.WriteLine($"forks: {iterations:N0} in {sw.Elapsed.TotalSeconds:F3}s = {iterations / sw.Elapsed.TotalSeconds:N0}/s");

var hashIterations = Math.Max(1_000, iterations / 100);
sw.Restart();
string? last = null;
for (var i = 0; i < hashIterations; i++) last = CanonicalJson.Sha256(state);
sw.Stop();
Console.WriteLine($"hashes: {hashIterations:N0} in {sw.Elapsed.TotalSeconds:F3}s = {hashIterations / sw.Elapsed.TotalSeconds:N0}/s");
Console.WriteLine($"last hash: {last}");
Console.WriteLine("Note: scaffold canonical JSON hashing is a correctness tool, not a final hot-path hash implementation.");


var environment = new PrototypeAiEnvironment();
var aiState = environment.Reset("bench-ai");
var startFrame = environment.Observe(aiState);
aiState = environment.Step(aiState, startFrame.LegalActions.Single().ActionId).State;

for (var i = 0; i < 100; i++) _ = environment.Observe(aiState);
for (var i = 0; i < 25; i++) _ = environment.Expand(aiState);

var observeIterations = Math.Max(1_000, iterations / 100);
sw.Restart();
PrototypeAiFrame? lastFrame = null;
for (var i = 0; i < observeIterations; i++) lastFrame = environment.Observe(aiState);
sw.Stop();
Console.WriteLine(
    $"ai observe: {observeIterations:N0} in {sw.Elapsed.TotalSeconds:F3}s = " +
    $"{observeIterations / sw.Elapsed.TotalSeconds:N0}/s");

var expandIterations = Math.Max(250, iterations / 500);
sw.Restart();
PrototypeAiExpansion[]? lastExpansion = null;
for (var i = 0; i < expandIterations; i++) lastExpansion = environment.Expand(aiState);
sw.Stop();
Console.WriteLine(
    $"ai expand: {expandIterations:N0} in {sw.Elapsed.TotalSeconds:F3}s = " +
    $"{expandIterations / sw.Elapsed.TotalSeconds:N0}/s " +
    $"({lastExpansion?.Length ?? 0} successors/call)");
Console.WriteLine($"last observation hash: {lastFrame?.ObservationHash}");
