using Sts2Emulator.Trace;

namespace Sts2Emulator.Core.Tests;

public sealed class ReferenceProbeAnalyzerTests
{
    [Fact]
    public void SummarizesProbeWithoutDependingOnGameAssemblies()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sts2-reference-probe-{Guid.NewGuid():N}.jsonl");

        try
        {
            File.WriteAllLines(
                path,
                [
                    """{"type":"session","schema":"sts2-reference-probe-v1","build_fingerprint":"abc"}""",
                    """{"type":"diagnostic","schema":"sts2-reference-probe-v1","sequence":1,"code":"recorder_attached"}""",
                    """{"type":"type_catalog","schema":"sts2-reference-probe-v1","sequence":3,"runtime_type":"Example"}""",
                    """{"type":"boundary","schema":"sts2-reference-probe-v1","sequence":2,"boundary":"combat_manager.CombatSetUp"}""",
                    """{"type":"boundary","schema":"sts2-reference-probe-v1","sequence":2,"boundary":"combat_history.Changed"}""",
                    """not-json"""
                ]);

            var summary = ReferenceProbeAnalyzer.Analyze(path);

            Assert.Equal(6, summary.Lines);
            Assert.Equal(5, summary.ValidRecords);
            Assert.Equal(1, summary.InvalidRecords);
            Assert.Equal("sts2-reference-probe-v1", summary.Schema);
            Assert.Equal("abc", summary.BuildFingerprint);
            Assert.Equal(1, summary.CombatCount);
            Assert.Equal(1, summary.TypeCatalogCount);
            Assert.Equal(4, summary.SequencedRecordCount);
            Assert.Equal(1, summary.SequenceRegressionCount);
            Assert.Equal(1, summary.DuplicateSequenceCount);
            Assert.Equal(2, summary.RecordTypes["boundary"]);
            Assert.Equal(1, summary.Diagnostics["recorder_attached"]);
            Assert.Equal(1, summary.Boundaries["combat_history.Changed"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
