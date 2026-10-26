using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

internal static class PrototypeEventPageTestHelperV111
{
    public static string[] ScriptedNeowOffers(string selectedName)
    {
        if (PrototypeNativeOvergrowthEvents.NeowCursedRelicNames.Contains(
                selectedName, StringComparer.Ordinal))
        {
            var second = selectedName == "CursedPearl"
                ? "ArcaneScroll" : "GoldenPearl";
            return
            [
                OptionId("BoomingConch"), OptionId(second),
                OptionId(selectedName)
            ];
        }

        return
        [
            OptionId(selectedName),
            OptionId(selectedName == "ArcaneScroll"
                ? "BoomingConch" : "ArcaneScroll"),
            OptionId("DowsingRod")
        ];

        static string OptionId(string name) =>
            "take_" + PrototypeNativeOvergrowthEvents.NeowRelicId(name);
    }

    public static RunState ContinueIfFinished(RunState state)
    {
        var engine = new PrototypeGameEngine();
        for (var page = 0; page < 8 && state.Phase == RunPhase.Event; page++)
        {
            var continuation = engine.GetLegalActions(state)
                .FirstOrDefault(action => action.Kind == "continue_event");
            if (continuation is null)
                return state;
            state = engine.Step(state, continuation).State;
        }
        return state;
    }
}
