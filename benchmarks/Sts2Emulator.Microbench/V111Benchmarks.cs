using System.Diagnostics;
using Sts2Emulator.Core;

internal static class V111Benchmarks
{
    public static void Run(int iterations)
    {
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
        var engine = new PrototypeGameEngine();
        var boundaries = new List<RunState>();
        foreach (var region in new[] { ActIdentity.Overgrowth, ActIdentity.Underdocks })
        {
            var state = V111RunFactory.Create("bench-" + region, RunConfiguration.Create(firstAct: region));
            var phases = new HashSet<RunPhase>();
            for (var step = 0; step < 30 && state.Phase != RunPhase.Terminal; step++)
            {
                if (phases.Add(state.Phase)) boundaries.Add(state);
                var legal = engine.GetLegalActions(state);
                state = engine.Step(state, Choose(state, legal)).State;
            }
        }
        var measurements = new List<object>();
        foreach (var state in boundaries)
        {
            var snapshot = RunSnapshot.Save(state);
            var action = Choose(state, engine.GetLegalActions(state));
            var label = state.Configuration!.Acts[0] + "/" + state.Phase;
            Measure(label, "fork", iterations, () => state.Fork());
            Measure(label, "hash", iterations, () => CanonicalJson.Sha256(state));
            Measure(label, "save_snapshot", iterations, () => RunSnapshot.Save(state));
            Measure(label, "load_snapshot", iterations, () => RunSnapshot.Load(snapshot));
            Measure(label, "step", iterations, () => engine.Step(state, action));
            var batch = Enumerable.Range(0, 8).Select(_ => new BatchStepRequest(state, action)).ToArray();
            Measure(label, "batch_8_sequential", Math.Max(1, iterations / 8), () => DeterministicBatch.Step(batch));
            measurements.Add(new { boundary = label, operation = "retained_fork_bytes",
                branches = 256, bytes_per_branch = MeasureRetainedForkBytes(state) });
        }
        var runs = Math.Max(4, iterations / 100);
        var decisions = 0L;
        var runTimer = Stopwatch.StartNew();
        for (var index = 0; index < runs; index++)
        {
            var state = PrototypeGameFactory.Create("whole-run-bench-" + index);
            while (state.Phase != RunPhase.Terminal)
            {
                if (state.DecisionIndex >= 5000) throw new InvalidOperationException("Benchmark policy exceeded decision limit.");
                state = engine.Step(state, Choose(state, engine.GetLegalActions(state))).State;
            }
            PrototypeStateInvariants.Validate(state);
            decisions += state.DecisionIndex;
        }
        runTimer.Stop();
        measurements.Add(new { boundary = "legacy-prototype", operation = "whole_terminal_runs", runs, decisions,
            elapsed_seconds = runTimer.Elapsed.TotalSeconds, runs_per_second = runs / runTimer.Elapsed.TotalSeconds });
        Console.WriteLine(CanonicalJson.Serialize(new { schema = "sts2-v111-benchmark-v1",
            fidelity_id = "v111-mechanics-prototype-rng-v1", runtime = Environment.Version.ToString(),
            processors = Environment.ProcessorCount, iterations,
            note = "Configured decision-boundary workloads plus explicitly legacy whole runs; no native full-route throughput claim.",
            measurements }));

        void Measure(string boundary, string operation, int count, Func<object> work)
        {
            for (var warm = 0; warm < 10; warm++) GC.KeepAlive(work());
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            for (var index = 0; index < count; index++) GC.KeepAlive(work());
            timer.Stop();
            measurements.Add(new { boundary, operation, iterations = count,
                elapsed_seconds = timer.Elapsed.TotalSeconds,
                operations_per_second = count / timer.Elapsed.TotalSeconds,
                calling_thread_allocated_bytes_per_operation = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / (double)count });
        }
    }

    // Isolate lifetimes: previous boundary branches must be unreachable before
    // taking the baseline, or their collection makes the next delta meaningless.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static double MeasureRetainedForkBytes(RunState state)
    {
        var before = GC.GetTotalMemory(true);
        var branches = Enumerable.Range(0, 256).Select(_ => state.Fork()).ToArray();
        var after = GC.GetTotalMemory(true);
        GC.KeepAlive(branches);
        if (after < before) throw new InvalidOperationException("Retained memory measurement was unstable; rerun in isolation.");
        return (after - before) / 256.0;
    }

    private static GameAction Choose(RunState state, IReadOnlyList<GameAction> legal)
    {
        if (legal.Count == 0) throw new InvalidOperationException("Nonterminal benchmark state has no action.");
        if (state.Phase == RunPhase.Combat)
            return legal.FirstOrDefault(action => action.Kind == "select_cards")
                ?? legal.FirstOrDefault(action => action.Kind == "play_card")
                ?? legal.FirstOrDefault(action => action.Kind == "end_turn") ?? legal[0];
        if (state.Phase == RunPhase.Shop) return legal.First(action => action.Kind == "leave_shop");
        if (state.Phase == RunPhase.Rest) return legal.FirstOrDefault(action => action.Kind == "rest_heal") ?? legal[0];
        return legal[0];
    }
}
