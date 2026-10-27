using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Core.Tests;

public sealed class SemanticDrawStageProfilerV111Tests
{
    [Fact]
    public void ProbeReportsOnlyWithinExplicitScopeAndRestoresNestedSink()
    {
        var outer = new List<string>();
        var inner = new List<string>();
        SemanticDrawStageProfilerV111.Measure("sub.semantic_draw_unobserved", () => 1);
        using (SemanticDrawStageProfilerV111.Begin(
            (name, started, allocation) =>
            {
                Assert.True(started > 0);
                Assert.True(allocation >= 0);
                outer.Add(name);
            }))
        {
            Assert.Equal(5, SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_outer", () => 5));
            using (SemanticDrawStageProfilerV111.Begin(
                (name, _, _) => inner.Add(name)))
            {
                SemanticDrawStageProfilerV111.Measure(
                    "sub.semantic_draw_inner", () => { });
            }
            SemanticDrawStageProfilerV111.Measure(
                "sub.semantic_draw_after_nested", () => 0);
        }
        SemanticDrawStageProfilerV111.Measure(
            "sub.semantic_draw_after_scope", () => 0);
        Assert.Equal(
            ["sub.semantic_draw_outer", "sub.semantic_draw_after_nested"], outer);
        Assert.Equal(["sub.semantic_draw_inner"], inner);
    }

    [Fact]
    public void FailingMeasuredOperationRestoresScopeAndReportsOneSample()
    {
        var names = new List<string>();
        using (SemanticDrawStageProfilerV111.Begin(
            (name, _, _) => names.Add(name)))
        {
            Assert.Throws<InvalidOperationException>(() =>
                SemanticDrawStageProfilerV111.Measure(
                    "sub.semantic_draw_failing",
                    () => throw new InvalidOperationException("test")));
        }
        Assert.Equal(["sub.semantic_draw_failing"], names);
    }
}
