using System.Text.Json;
using Sts2Emulator.Core;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine("STS2 Emulator developer CLI");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  doctor                 Print runtime and implementation status");
    Console.WriteLine("  hash-demo              Build a tiny synthetic canonical state and hash it");
    Console.WriteLine("  prototype-native-overgrowth-map [seed] [ascension]");
    Console.WriteLine("                          Print source-shaped 15-row Act 1 map (not native RNG-exact)");
    Console.WriteLine("  prototype-native-overgrowth-sweep [n] [ascension]");
    Console.WriteLine("                          Validate native-shaped Act 1 generation across seeds");
    Console.WriteLine("  prototype-native-overgrowth-run [seed] [ascension]");
    Console.WriteLine("                          Run the opt-in native-shaped Act 1 in the prototype engine");
    Console.WriteLine("  prototype-run [seed] [ascension]");
    Console.WriteLine("                          Drive the restrictive Silent prototype to terminal state");
    Console.WriteLine("  prototype-sweep [n] [ascension]");
    Console.WriteLine("                          Run a deterministic smoke policy over many seeds");
    Console.WriteLine("  prototype-overgrowth-audit [n] [ascension]");
    Console.WriteLine("                          Audit seeded Overgrowth encounter/boss coverage");
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
    Console.WriteLine("  reference-probe-actions <probe.jsonl>");
    Console.WriteLine("                          Extract evidence-backed native action envelopes");
    Console.WriteLine("  reference-knight-gang-audit <probe.jsonl> [--json]");
    Console.WriteLine("                          Extract Knight Gang moves/state/RNG evidence");
    Console.WriteLine("  reference-corpus-summary <eng-dir>");
    Console.WriteLine("                          Summarize a local version-pinned reference corpus");
    Console.WriteLine("  reference-corpus-get <eng-dir> <kind> <id-or-name>");
    Console.WriteLine("                          Print one reference corpus entity");
    Console.WriteLine("  reference-mechanics-gap <eng-dir> [--json]");
    Console.WriteLine("                          Compare native Silent mechanics with the prototype");
    Console.WriteLine("  reference-source-search <source-dir> <query> [max] [context]");
    Console.WriteLine("                          Search a local decompiled sts2.dll source tree");
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
            Console.WriteLine(
                $"    events: {string.Join(", ", catalog.Events)}");
        }

        break;
    }

    case "reference-probe-actions":
    {
        if (args.Length != 2)
        {
            throw new ArgumentException(
                "reference-probe-actions requires exactly one JSONL path.");
        }

        var report = Sts2Emulator.Trace.ReferenceProbeActionExtractor.Analyze(args[1]);
        Console.WriteLine(
            $"Actions: cards={report.CardPlayCount}, potions={report.PotionUseCount}, " +
            $"end_turn={report.EndTurnCount}");
        Console.WriteLine(
            $"Candidate boundaries: before={report.ActionsWithCandidateBefore}/{report.Actions.Count}, " +
            $"after={report.ActionsWithCandidateAfter}/{report.Actions.Count}");

        foreach (var action in report.Actions)
        {
            Console.WriteLine(
                $"[{action.ActionIndex}] {action.Action.Kind} observed={action.ObservedSequence} " +
                $"before={action.CandidateBefore?.Sequence.ToString() ?? "-"} " +
                $"finish={action.LifecycleFinish?.Sequence.ToString() ?? "-"} " +
                $"after={action.CandidateAfter?.Sequence.ToString() ?? "-"}");
            Console.WriteLine(
                $"  payload: {action.Action.Payload.GetRawText()}");

            if (action.CandidateBefore is not null)
            {
                Console.WriteLine(
                    $"  before evidence: {action.CandidateBefore.Evidence}");
            }

            if (action.CandidateAfter is not null)
            {
                Console.WriteLine(
                    $"  after evidence: {action.CandidateAfter.Evidence}");
            }

            foreach (var diagnostic in action.Diagnostics)
            {
                Console.WriteLine($"  diagnostic: {diagnostic}");
            }
        }

        break;
    }

    case "reference-knight-gang-audit":
    {
        if (args.Length is < 2 or > 3
            || (args.Length == 3
                && !StringComparer.Ordinal.Equals(args[2], "--json")))
        {
            throw new ArgumentException(
                "reference-knight-gang-audit requires <probe.jsonl> and optionally --json.");
        }

        var audit =
            Sts2Emulator.Trace.ReferenceKnightGangProbeAnalyzer.Analyze(
                args[1]);

        if (args.Length == 3)
        {
            Console.WriteLine(CanonicalJson.Serialize(audit));
            break;
        }

        Console.WriteLine($"Schema: {audit.Schema ?? "-"}");
        Console.WriteLine(
            $"Build fingerprint: {audit.BuildFingerprint ?? "-"}");
        Console.WriteLine(
            $"Knight Gang: combats={audit.MatchingCombatCount}, checkpoints={audit.MatchingCheckpointCount}");

        foreach (var combat in audit.Combats)
        {
            Console.WriteLine(
                $"Combat {combat.CombatIndex}: seq={combat.FirstSequence}-{combat.LastSequence}, " +
                $"A={combat.Ascension?.ToString() ?? "-"}, checkpoints={combat.CheckpointCount}");
            Console.WriteLine(
                $"  evidence: moves={combat.CheckpointsWithMoveEvidence}, " +
                $"intents={combat.CheckpointsWithIntentEvidence}, " +
                $"monster_ai_rng={combat.CheckpointsWithMonsterAiRng}");
            Console.WriteLine(
                $"  enemies: {string.Join(", ", combat.NativeEnemyIdentities)}");

            foreach (var mismatch in combat.StaticMismatches)
            {
                Console.WriteLine($"  static mismatch: {mismatch}");
            }

            foreach (var checkpoint in combat.Checkpoints)
            {
                Console.WriteLine(
                    $"  [{checkpoint.Sequence}] {checkpoint.Boundary} " +
                    $"round={checkpoint.RoundNumber?.ToString() ?? "-"} " +
                    $"side={checkpoint.CurrentSide ?? "-"} " +
                    $"player={checkpoint.PlayerHp?.ToString() ?? "-"}/" +
                    $"{checkpoint.PlayerMaxHp?.ToString() ?? "-"} " +
                    $"block={checkpoint.PlayerBlock?.ToString() ?? "-"} " +
                    $"cards={checkpoint.Cards.HandCount}/" +
                    $"{checkpoint.Cards.DrawCount}/" +
                    $"{checkpoint.Cards.DiscardCount}/" +
                    $"{checkpoint.Cards.ExhaustCount}/" +
                    $"{checkpoint.Cards.PlayCount} " +
                    $"upgraded={checkpoint.Cards.UpgradedCardCount} " +
                    $"hexed={checkpoint.Cards.HexedCardCount}");

                if (checkpoint.HistoryRuntimeType is not null)
                {
                    Console.WriteLine(
                        $"    history={checkpoint.HistoryRuntimeType} " +
                        $"amount={checkpoint.HistoryAmount?.ToString() ?? "-"} " +
                        $"source={checkpoint.HistorySourceCombatId?.ToString() ?? "-"} " +
                        $"target={checkpoint.HistoryTargetCombatId?.ToString() ?? "-"}");
                }

                foreach (var enemy in checkpoint.Enemies)
                {
                    Console.WriteLine(
                        $"    {enemy.Role ?? "?"} id={enemy.NativeIdentity ?? "-"} " +
                        $"hp={enemy.CurrentHp?.ToString() ?? "-"}/" +
                        $"{enemy.MaxHp?.ToString() ?? "-"} " +
                        $"block={enemy.Block?.ToString() ?? "-"} " +
                        $"move={enemy.CurrentMove ?? enemy.LastMove ?? "-"} " +
                        $"next={enemy.NextMove ?? "-"} intent={enemy.Intent ?? "-"}");
                }
            }
        }

        foreach (var diagnostic in audit.Diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }

        break;
    }

    case "reference-corpus-summary":
    {
        if (args.Length != 2)
        {
            throw new ArgumentException(
                "reference-corpus-summary requires exactly one corpus eng directory.");
        }

        var corpus = Sts2Emulator.Trace.ReferenceCorpus.Open(args[1]);
        var summary = corpus.Summarize();
        Console.WriteLine($"Root: {summary.RootDirectory}");
        foreach (var table in summary.Tables)
        {
            Console.WriteLine(
                $"{table.Kind}: {table.Count} entries; fields={string.Join(",", table.Fields)}");
        }

        break;
    }

    case "reference-corpus-get":
    {
        if (args.Length != 4)
        {
            throw new ArgumentException(
                "reference-corpus-get requires <eng-dir> <kind> <id-or-name>.");
        }

        var corpus = Sts2Emulator.Trace.ReferenceCorpus.Open(args[1]);
        var entity = corpus.Resolve(args[2], args[3]);
        Console.WriteLine(JsonSerializer.Serialize(
            entity.Data,
            new JsonSerializerOptions { WriteIndented = true }));
        break;
    }

    case "reference-mechanics-gap":
    {
        if (args.Length is < 2 or > 3
            || (args.Length == 3
                && !StringComparer.Ordinal.Equals(args[2], "--json")))
        {
            throw new ArgumentException(
                "reference-mechanics-gap requires <eng-dir> and optionally --json.");
        }

        var corpus = Sts2Emulator.Trace.ReferenceCorpus.Open(args[1]);
        var report = Sts2Emulator.Trace.ReferenceMechanicsGapAnalyzer.Analyze(corpus);

        if (args.Length == 3)
        {
            Console.WriteLine(CanonicalJson.Serialize(report));
            break;
        }

        Console.WriteLine($"Native Silent cards: {report.NativeSilentCardCount}");
        Console.WriteLine($"Prototype cards: {report.PrototypeCardCount}");
        Console.WriteLine($"Name-matched cards: {report.NameMatchedCardCount}");
        Console.WriteLine(
            $"Structured-field matches: {report.StructuredFieldMatchCount}/{report.NameMatchedCardCount}");
        Console.WriteLine($"Missing native card definitions: {report.MissingNativeCardCount}");
        Console.WriteLine(
            $"Native cards requiring unsupported features: {report.CardsWithUnsupportedFeatures}");
        Console.WriteLine(
            $"Prototype cards outside Silent pool: " +
            $"{string.Join(", ", report.PrototypeCardsOutsideSilentPool)}");
        Console.WriteLine(
            $"Prototype cards absent from pinned reference: " +
            $"{string.Join(", ", report.PrototypeCardsAbsentFromReference)}");

        Console.WriteLine("Starting-run gaps:");
        if (report.StartingRunMismatches.Length == 0)
        {
            Console.WriteLine("  none");
        }
        else
        {
            foreach (var mismatch in report.StartingRunMismatches)
            {
                Console.WriteLine($"  {mismatch}");
            }
        }

        Console.WriteLine("Mechanic coverage:");
        foreach (var feature in report.FeatureCoverage)
        {
            Console.WriteLine(
                $"  {(feature.PrototypeEngineHasPrimitive ? "covered" : "GAP"),-7} " +
                $"{feature.Feature}: {feature.NativeSilentCards} cards");
        }

        Console.WriteLine("Matched cards with structured mismatches:");
        foreach (var card in report.Cards.Where(
                     card => card.PrototypeId is not null
                         && card.StructuredMismatches.Length > 0))
        {
            Console.WriteLine(
                $"  {card.NativeId} -> {card.PrototypeId}: " +
                string.Join("; ", card.StructuredMismatches));
        }

        Console.WriteLine("Native cards missing from prototype:");
        foreach (var card in report.Cards.Where(card => card.PrototypeId is null))
        {
            var unsupported = card.UnsupportedFeatures.Length == 0
                ? string.Empty
                : $" [gaps: {string.Join(",", card.UnsupportedFeatures)}]";
            Console.WriteLine(
                $"  {card.NativeId} ({card.NativeName}){unsupported}");
        }

        Console.WriteLine("Reference-data warnings:");
        foreach (var card in report.Cards.Where(card => card.SourceWarnings.Length > 0))
        {
            foreach (var warning in card.SourceWarnings)
            {
                Console.WriteLine(
                    $"  {card.NativeId}: {warning}");
            }
        }

        break;
    }

    case "reference-source-search":
    {
        if (args.Length is < 3 or > 5)
        {
            throw new ArgumentException(
                "reference-source-search requires <source-dir> <query> and optionally [max] [context].");
        }

        var maxMatches = 200;
        if (args.Length >= 4
            && (!int.TryParse(args[3], out maxMatches) || maxMatches <= 0))
        {
            throw new ArgumentException(
                "reference-source-search max must be a positive integer.");
        }

        var contextLines = 0;
        if (args.Length == 5
            && (!int.TryParse(args[4], out contextLines) || contextLines < 0))
        {
            throw new ArgumentException(
                "reference-source-search context must be a non-negative integer.");
        }

        var result = Sts2Emulator.Trace.ReferenceSourceSearch.Search(
            args[1],
            args[2],
            maxMatches,
            contextLines);

        Console.WriteLine($"Root: {result.RootDirectory}");
        Console.WriteLine($"Query: {result.Query}");
        Console.WriteLine($"Files scanned: {result.FilesScanned}");
        Console.WriteLine($"Matches: {result.Matches.Count}");
        foreach (var match in result.Matches)
        {
            if (match.Context is null || match.Context.Count == 0)
            {
                Console.WriteLine(
                    $"{match.RelativePath}:{match.LineNumber}: {match.Line}");
                continue;
            }

            Console.WriteLine(
                $"--- {match.RelativePath}:{match.LineNumber} ---");
            foreach (var line in match.Context)
            {
                Console.WriteLine(
                    $"{(line.IsMatch ? ">" : " ")} {line.LineNumber,6} | {line.Line}");
            }
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

    case "prototype-native-overgrowth-map":
    {
        var seed = args.Length >= 2 ? args[1] : "demo";
        var ascension = args.Length >= 3
            ? int.Parse(args[2])
            : 0;
        if (ascension < 0)
        {
            throw new ArgumentException("Ascension must not be negative.");
        }

        var state = PrototypeNativeOvergrowthRunFactory.Create(seed, ascension);
        PrototypeStateInvariants.Validate(state);
        var map = state.World!.Map;
        Console.WriteLine($"Seed: {seed} / Ascension: {ascension}");
        Console.WriteLine($"Map profile: {map.GenerationProfileId}");
        for (var floor = 1; floor <= PrototypeNativeOvergrowthMap.BossFloor; floor++)
        {
            var layer = map.Nodes
                .Where(node => node.Floor == floor)
                .OrderBy(node => node.NodeId, StringComparer.Ordinal);
            Console.WriteLine($"Floor {floor,2}: {string.Join(" | ", layer.Select(node =>
                $"{node.NodeId} {node.RoomType} -> {string.Join(",", node.NextNodeIds ?? [])}"))}");
        }
        break;
    }

    case "prototype-native-overgrowth-sweep":
    {
        var count = args.Length >= 2 ? int.Parse(args[1]) : 64;
        var ascension = args.Length >= 3 ? int.Parse(args[2]) : 0;
        if (count < 1 || ascension < 0)
        {
            throw new ArgumentException(
                "Native Overgrowth sweep requires a positive count and nonnegative ascension.");
        }

        var engine = new PrototypeGameEngine();
        var completed = 0;
        var defeated = 0;
        var encounteredRooms = new Dictionary<PrototypeRoomType, int>();
        var reachedFloors = new Dictionary<int, int>();
        var bossIds = new HashSet<string>(StringComparer.Ordinal);
        for (var run = 0; run < count; run++)
        {
            var state = PrototypeNativeOvergrowthRunFactory.Create(
                $"native-generation-sweep-{ascension}-{run}", ascension);
            PrototypeStateInvariants.Validate(state);
            var map = state.World!.Map;
            if (map.GenerationProfileId
                    != PrototypeNativeOvergrowthMap.GenerationProfileId
                || map.Nodes.Select(node => node.Floor).Max()
                    != PrototypeNativeOvergrowthMap.BossFloor)
            {
                throw new InvalidOperationException(
                    $"Native Overgrowth generated invalid floor structure for run {run}.");
            }

            if (state.World.ActOneEncounterPool?.BossEncounterId is { } boss)
            {
                bossIds.Add(boss);
            }

            var settled = false;
            for (var step = 0; step < 5000; step++)
            {
                if (state.Phase == RunPhase.Terminal
                    || state.Phase == RunPhase.ActTransition
                        && state.World?.Act == 1)
                {
                    settled = true;
                    break;
                }

                var actions = engine.GetLegalActions(state);
                if (actions.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Native Overgrowth run {run} stuck in phase {state.Phase}.");
                }

                state = engine.Step(
                    state,
                    ChoosePrototypeAction(state, actions)).State;
                PrototypeStateInvariants.Validate(state);
            }

            if (!settled)
            {
                throw new InvalidOperationException(
                    $"Native Overgrowth run {run} exceeded 5000 decisions.");
            }

            if (state.Phase == RunPhase.ActTransition)
            {
                completed++;
            }
            else
            {
                defeated++;
            }

            foreach (var room in state.World!.CompletedRooms)
            {
                encounteredRooms[room.RoomType] =
                    encounteredRooms.GetValueOrDefault(room.RoomType) + 1;
                reachedFloors[room.Floor] =
                    reachedFloors.GetValueOrDefault(room.Floor) + 1;
            }
        }

        Console.WriteLine($"Native-shaped Overgrowth sweep: {count} seeds / A{ascension}");
        Console.WriteLine($"Act 1 cleared: {completed}; defeated: {defeated}");
        Console.WriteLine($"Boss IDs observed: {string.Join(", ", bossIds.Order(StringComparer.Ordinal))}");
        Console.WriteLine($"Completed rooms: {string.Join(", ",
            encounteredRooms.OrderBy(pair => pair.Key).Select(pair =>
                $"{pair.Key}={pair.Value}"))}");
        Console.WriteLine($"Floors with completed rooms: {string.Join(", ",
            reachedFloors.Keys.Order())}");
        break;
    }

    case "prototype-native-overgrowth-run":
    {
        var seed = args.Length >= 2 ? args[1] : "demo";
        var ascension = args.Length >= 3 ? int.Parse(args[2]) : 0;
        if (ascension < 0)
        {
            throw new ArgumentException("Ascension must not be negative.");
        }

        var engine = new PrototypeGameEngine();
        var state = PrototypeNativeOvergrowthRunFactory.Create(seed, ascension);
        var reachedFloors = new HashSet<int>();
        var phases = new HashSet<RunPhase> { state.Phase };
        for (var step = 0; step < 5_000
             && state.Phase != RunPhase.Terminal
             && !(state.Phase == RunPhase.ActTransition
                  && state.World?.Act == 1); step++)
        {
            var legal = engine.GetLegalActions(state);
            if (legal.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Native-structure run is stuck in {state.Phase}.");
            }
            state = engine.Step(
                state, ChoosePrototypeAction(state, legal)).State;
            PrototypeStateInvariants.Validate(state);
            phases.Add(state.Phase);
            if (state.World?.Act == 1)
            {
                reachedFloors.Add(state.World.Floor);
            }
        }

        Console.WriteLine($"Seed: {seed}");
        Console.WriteLine($"Mode: native-shaped Overgrowth map + prototype economy");
        Console.WriteLine($"Act 1 outcome: {(state.Phase == RunPhase.ActTransition
            ? "cleared"
            : state.World?.TerminalOutcome ?? "incomplete")}");
        Console.WriteLine($"Act/Floor: {state.World?.Act}/{state.World?.Floor}");
        Console.WriteLine($"Decisions: {state.DecisionIndex}");
        Console.WriteLine($"Act 1 floors seen: {string.Join(", ", reachedFloors.Order())}");
        Console.WriteLine($"Phases: {string.Join(", ", phases.Order())}");
        break;
    }

    case "prototype-run":
    {
        var seed = args.Length >= 2 ? args[1] : "demo";
        var ascension = 0;
        if (args.Length >= 3
            && (!int.TryParse(args[2], out ascension)
                || ascension < 0))
        {
            throw new ArgumentException(
                "prototype-run ascension must be a non-negative integer.");
        }

        var engine = new PrototypeGameEngine();
        var state = PrototypeGameFactory.Create(
            seed,
            ascension);
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
        Console.WriteLine($"Ascension: {state.Ascension}");
        Console.WriteLine($"Outcome: {state.World?.TerminalOutcome}");
        Console.WriteLine($"Decisions: {state.DecisionIndex}");
        Console.WriteLine($"Act/Floor: {state.World?.Act}/{state.World?.Floor}");
        Console.WriteLine($"HP: {state.Player.Hp}/{state.Player.MaxHp}");
        Console.WriteLine($"Deck: {state.Player.Deck.Length} cards");
        Console.WriteLine($"Phases seen: {string.Join(", ", phases.OrderBy(phase => phase))}");
        break;
    }

    case "prototype-overgrowth-audit":
    {
        var runCount = 100;
        if (args.Length >= 2
            && (!int.TryParse(args[1], out runCount)
                || runCount <= 0))
        {
            throw new ArgumentException(
                "prototype-overgrowth-audit count must be a positive integer.");
        }

        var ascension = 0;
        if (args.Length >= 3
            && (!int.TryParse(args[2], out ascension)
                || ascension < 0))
        {
            throw new ArgumentException(
                "prototype-overgrowth-audit ascension must be a non-negative integer.");
        }

        var overgrowthEncounterIds =
            PrototypeContent.OvergrowthWeakEncounterPool
                .Concat(PrototypeContent.OvergrowthNormalEncounterPool)
                .Concat(PrototypeContent.OvergrowthEliteEncounterPool)
                .Concat(PrototypeContent.OvergrowthBossEncounterPool)
                .ToHashSet(StringComparer.Ordinal);
        var encounterCounts =
            overgrowthEncounterIds.ToDictionary(
                id => id,
                _ => 0,
                StringComparer.Ordinal);
        var bossCounts =
            PrototypeContent.OvergrowthBossEncounterPool
                .ToDictionary(
                    id => id,
                    _ => 0,
                    StringComparer.Ordinal);
        var weakSequenceViolations = 0;
        var normalBagViolations = 0;
        var eliteBagViolations = 0;

        for (var runIndex = 0;
             runIndex < runCount;
             runIndex++)
        {
            var seed = $"overgrowth-audit-{runIndex}";
            var engine = new PrototypeGameEngine();
            var state = PrototypeGameFactory.Create(
                seed,
                ascension);

            state = engine.Step(
                state,
                AssertSingleAuditAction(
                    engine.GetLegalActions(state),
                    "run start")).State;
            PrototypeStateInvariants.Validate(state);

            var pool = state.World?.ActOneEncounterPool
                ?? throw new InvalidOperationException(
                    $"Overgrowth audit seed {seed} did not initialize an Act 1 encounter pool.");
            if (pool.Region
                != PrototypeActOneRegion.Overgrowth)
            {
                throw new InvalidOperationException(
                    $"Overgrowth audit seed {seed} initialized region {pool.Region}.");
            }

            var selectedBoss = pool.BossEncounterId
                ?? throw new InvalidOperationException(
                    $"Overgrowth audit seed {seed} did not preselect a boss.");
            bossCounts[selectedBoss] =
                bossCounts.GetValueOrDefault(selectedBoss) + 1;

            var weak = new List<string>();
            for (var index = 0; index < 3; index++)
            {
                state = StartPrototypeAuditRoom(
                    engine,
                    state,
                    PrototypeRoomType.Combat);
                var id = state.World!.EncounterIds[^1];
                weak.Add(id);
                encounterCounts[id]++;
                PrototypeStateInvariants.Validate(state);
            }

            if (weak.Count
                != weak.Distinct(StringComparer.Ordinal).Count()
                || weak.Any(id =>
                    !PrototypeContent.OvergrowthWeakEncounterPool
                        .Contains(id, StringComparer.Ordinal)))
            {
                weakSequenceViolations++;
            }

            var normal = new List<string>();
            for (var index = 0;
                 index < PrototypeContent
                     .OvergrowthNormalEncounterPool.Length;
                 index++)
            {
                state = StartPrototypeAuditRoom(
                    engine,
                    state,
                    PrototypeRoomType.Combat);
                var id = state.World!.EncounterIds[^1];
                normal.Add(id);
                encounterCounts[id]++;
                PrototypeStateInvariants.Validate(state);
            }

            if (normal.Count
                != normal.Distinct(StringComparer.Ordinal).Count()
                || normal.Any(id =>
                    !PrototypeContent.OvergrowthNormalEncounterPool
                        .Contains(id, StringComparer.Ordinal)))
            {
                normalBagViolations++;
            }

            var elites = new List<string>();
            for (var index = 0;
                 index < PrototypeContent
                     .OvergrowthEliteEncounterPool.Length;
                 index++)
            {
                state = StartPrototypeAuditRoom(
                    engine,
                    state,
                    PrototypeRoomType.Elite);
                var id = state.World!.EncounterIds[^1];
                elites.Add(id);
                encounterCounts[id]++;
                PrototypeStateInvariants.Validate(state);
            }

            if (elites.Count
                != elites.Distinct(StringComparer.Ordinal).Count()
                || elites.Any(id =>
                    !PrototypeContent.OvergrowthEliteEncounterPool
                        .Contains(id, StringComparer.Ordinal)))
            {
                eliteBagViolations++;
            }

            state = StartPrototypeAuditRoom(
                engine,
                state,
                PrototypeRoomType.Boss);
            var bossEncounter = state.World!.EncounterIds[^1];
            if (!StringComparer.Ordinal.Equals(
                    bossEncounter,
                    selectedBoss))
            {
                throw new InvalidOperationException(
                    $"Overgrowth audit seed {seed} entered boss '{bossEncounter}' " +
                    $"instead of preselected boss '{selectedBoss}'.");
            }

            encounterCounts[bossEncounter]++;
            PrototypeStateInvariants.Validate(state);
        }

        var covered = encounterCounts
            .Count(item => item.Value > 0);
        var missing = encounterCounts
            .Where(item => item.Value == 0)
            .Select(item => item.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine($"Selector samples: {runCount}");
        Console.WriteLine($"Ascension: {ascension}");
        Console.WriteLine(
            $"Overgrowth encounter coverage: {covered}/{overgrowthEncounterIds.Count}");
        Console.WriteLine(
            $"Weak no-replacement violations: {weakSequenceViolations}");
        Console.WriteLine(
            $"Normal-bag violations: {normalBagViolations}");
        Console.WriteLine(
            $"Elite-bag violations: {eliteBagViolations}");
        Console.WriteLine("Boss selections:");
        foreach (var item in bossCounts.OrderBy(item => item.Key))
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine("Encounter counts:");
        foreach (var item in encounterCounts
                     .OrderByDescending(item => item.Value)
                     .ThenBy(item => item.Key))
        {
            Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        Console.WriteLine(
            missing.Length == 0
                ? "Missing encounters: none"
                : $"Missing encounters: {string.Join(", ", missing)}");
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

        var ascension = 0;
        if (args.Length >= 3
            && (!int.TryParse(args[2], out ascension)
                || ascension < 0))
        {
            throw new ArgumentException(
                "prototype-sweep ascension must be a non-negative integer.");
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
            var state = PrototypeGameFactory.Create(
                seed,
                ascension);

            for (var step = 0; step < 5_000 && state.Phase != RunPhase.Terminal; step++)
            {
                var legal = engine.GetLegalActions(state);
                if (legal.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Prototype sweep stuck on seed {seed} in {state.Phase}.");
                }

                var chosen = ChoosePrototypeAction(state, legal);
                state = engine.Step(state, chosen).State;
                try
                {
                    PrototypeStateInvariants.Validate(state);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Prototype sweep invariant failure on seed {seed}, " +
                        $"step {step}, phase {state.Phase}, action {chosen.Kind}.",
                        ex);
                }
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

static GameAction AssertSingleAuditAction(
    IReadOnlyList<GameAction> legal,
    string boundary)
{
    if (legal.Count != 1)
    {
        throw new InvalidOperationException(
            $"Overgrowth audit expected exactly one legal action at {boundary}, found {legal.Count}.");
    }

    return legal[0];
}

static RunState StartPrototypeAuditRoom(
    PrototypeGameEngine engine,
    RunState state,
    PrototypeRoomType roomType)
{
    var world = state.World
        ?? throw new InvalidOperationException(
            "Overgrowth audit state has no world.");

    var targetFloor = roomType switch
    {
        PrototypeRoomType.Combat => 2,
        PrototypeRoomType.Elite => 3,
        PrototypeRoomType.Boss =>
            PrototypeContent.Rules.FloorsPerAct,
        _ => throw new InvalidOperationException(
            $"Overgrowth audit does not support room type {roomType}.")
    };

    var targetRule = PrototypeContent.Rules.MapFloorRules
        .Single(rule =>
            targetFloor >= rule.MinFloor
            && targetFloor <= rule.MaxFloor);
    if (!targetRule.RoomPool.Contains(roomType))
    {
        throw new InvalidOperationException(
            $"Overgrowth audit chose illegal {roomType} floor {targetFloor}.");
    }

    var entryId =
        $"overgrowth-audit:entry:{state.DecisionIndex}";
    var targetId =
        $"overgrowth-audit:target:{roomType}:{state.DecisionIndex}";
    var bossId =
        $"overgrowth-audit:boss:{state.DecisionIndex}";

    var entry = new MapNodeState(
        entryId,
        Act: 1,
        Floor: 1,
        RoomType: PrototypeRoomType.Combat,
        NextNodeIds: []);

    MapNodeState[] nodes;
    string currentNodeId;
    int currentFloor;

    if (targetFloor == 2)
    {
        entry = entry with
        {
            NextNodeIds = [targetId]
        };
        var target = new MapNodeState(
            targetId,
            Act: 1,
            Floor: targetFloor,
            RoomType: roomType,
            NextNodeIds: []);

        var boss = new MapNodeState(
            bossId,
            Act: 1,
            Floor: PrototypeContent.Rules.FloorsPerAct,
            RoomType: PrototypeRoomType.Boss,
            NextNodeIds: []);

        nodes = [entry, target, boss];
        currentNodeId = entryId;
        currentFloor = 1;
    }
    else
    {
        var previousFloor = targetFloor - 1;
        var previousRule =
            PrototypeContent.Rules.MapFloorRules.Single(
                rule =>
                    previousFloor >= rule.MinFloor
                    && previousFloor <= rule.MaxFloor);
        var previousRoomType =
            previousRule.RoomPool[0];
        var previousId =
            $"overgrowth-audit:previous:{state.DecisionIndex}";

        var previous = new MapNodeState(
            previousId,
            Act: 1,
            Floor: previousFloor,
            RoomType: previousRoomType,
            NextNodeIds: [targetId]);
        var target = new MapNodeState(
            targetId,
            Act: 1,
            Floor: targetFloor,
            RoomType: roomType,
            NextNodeIds: []);

        if (targetFloor
            == PrototypeContent.Rules.FloorsPerAct)
        {
            nodes = [entry, previous, target];
        }
        else
        {
            var boss = new MapNodeState(
                bossId,
                Act: 1,
                Floor: PrototypeContent.Rules.FloorsPerAct,
                RoomType: PrototypeRoomType.Boss,
                NextNodeIds: []);
            nodes = [entry, previous, target, boss];
        }

        currentNodeId = previousId;
        currentFloor = previousFloor;
    }

    state = state with
    {
        Phase = RunPhase.MapChoice,
        World = world with
        {
            Act = 1,
            Floor = currentFloor,
            ActiveRoom = null,
            Map = new MapState(
                nodes,
                CurrentNodeId: currentNodeId,
                EntryNodeIds: [entryId]),
            Combat = null,
            Reward = null,
            Shop = null,
            Event = null
        }
    };

    return engine.Step(
        state,
        AssertSingleAuditAction(
            engine.GetLegalActions(state),
            $"{roomType} selector audit")).State;
}

static GameAction ChoosePrototypeAction(
    RunState state,
    IReadOnlyList<GameAction> legal)
{
    if (state.Phase == RunPhase.Rest)
    {
        if (state.Player.Hp * 2 < state.Player.MaxHp)
        {
            var heal =
                legal.FirstOrDefault(action =>
                    action.Kind == "rest_heal");
            if (heal is not null)
            {
                return heal;
            }
        }

        return legal.FirstOrDefault(action =>
                action.Kind == "rest_upgrade")
            ?? legal.FirstOrDefault(action =>
                action.Kind == "rest_train")
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
