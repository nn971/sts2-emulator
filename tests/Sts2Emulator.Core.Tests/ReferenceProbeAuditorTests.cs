using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceProbeAuditorTests
{
    [Fact]
    public void AuditsHistoryCreatureAndRngShapesWithoutNativeAssemblies()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sts2-reference-probe-audit-{Guid.NewGuid():N}.jsonl");

        try
        {
            File.WriteAllLines(
                path,
                [
                    """{"type":"session","schema":"sts2-reference-probe-v1"}""",
                    """{"type":"type_catalog","runtime_type":"Example.CardPlayStarted","properties":[{"name":"Card"},{"name":"Target"}],"fields":[{"name":"_sequence"}]}""",
                    """{"type":"boundary","boundary":"combat_manager.CombatSetUp","state_hash":"h0","state":{"run_rng":{"type":"Example.SerializableRunRng","shuffle":{"type":"Example.SerializableRng","state":11}},"players":[{"creature":{"current_hp":70},"player_rng":{"type":"Example.SerializablePlayerRng","card_rng":{"type":"Example.SerializableRng","state":22}}}]}}""",
                    """{"type":"boundary","boundary":"combat_history.Changed","state_hash":"h1","history_entry":{"type":"Example.CardPlayStarted","card":{"type":"Example.Strike","combat_id":7},"target":null},"state":{"run_rng":{"type":"Example.SerializableRunRng","shuffle":{"type":"Example.SerializableRng","state":12}},"players":[{"creature":{"current_hp":70},"player_rng":{"type":"Example.SerializablePlayerRng","card_rng":{"type":"Example.SerializableRng","state":22}}}]}}""",
                    """{"type":"boundary","boundary":"combat_history.Changed","state_hash":"h1","history_entry":{"type":"Example.CardPlayStarted","card":{"type":"Example.Strike","combat_id":7},"target":null},"state":{"run_rng":{"type":"Example.SerializableRunRng","shuffle":{"type":"Example.SerializableRng","state":12}},"players":[{"creature":{"current_hp":70},"player_rng":{"type":"Example.SerializablePlayerRng","card_rng":{"type":"Example.SerializableRng","state":22}}}]}}""",
                    """not-json"""
                ]);

            var audit = ReferenceProbeAuditor.Analyze(path);

            Assert.Equal(3, audit.BoundaryCount);
            Assert.Equal(2, audit.HistoryBoundaryCount);
            Assert.Equal(2, audit.HistoryPayloadCount);
            Assert.Equal(3, audit.PlayerSnapshotCount);
            Assert.Equal(3, audit.PlayerCreatureSnapshotCount);
            Assert.Equal(3, audit.RunRngSnapshotCount);
            Assert.Equal(3, audit.PlayerRngSnapshotCount);

            var history = Assert.Single(audit.HistoryEntryShapes);
            Assert.Equal("Example.CardPlayStarted", history.RuntimeType);
            Assert.Equal(2, history.Count);
            Assert.Equal(1, history.StateChangedCount);
            Assert.Equal(1, history.StatePreservedCount);
            Assert.Equal(1, history.DistinctStateHashes);
            Assert.Contains("$.card.combat_id", history.PayloadPaths);
            Assert.Contains("$.target", history.PayloadPaths);

            Assert.Equal(2, audit.RngShapes.Count);
            var runRng = Assert.Single(
                audit.RngShapes.Where(shape => shape.Scope == "run"));
            Assert.Equal("Example.SerializableRunRng", runRng.RuntimeType);
            Assert.Equal(3, runRng.Count);
            Assert.Contains("$.shuffle.state", runRng.PayloadPaths);

            var playerRng = Assert.Single(
                audit.RngShapes.Where(shape => shape.Scope == "player"));
            Assert.Contains("$.card_rng.state", playerRng.PayloadPaths);

            var catalog = Assert.Single(audit.TypeCatalogs);
            Assert.Equal("Example.CardPlayStarted", catalog.RuntimeType);
            Assert.Equal(new[] { "Card", "Target" }, catalog.Properties);
            Assert.Equal(new[] { "_sequence" }, catalog.Fields);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
