using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class ColonyStorytellerChecks
{
    public static void Run()
    {
        var rules = new[]
        {
            new StorytellerRule("food_crisis", StorytellerMetric.FoodPerResident,
                StorytellerComparison.Below, 2f, 1, 5, 3, "Food is running low."),
            new StorytellerRule("stress", StorytellerMetric.AverageStress,
                StorytellerComparison.Above, 60f, 2, 10, 2, "Stress is high.")
        };
        var storyteller = new ColonyStoryteller(rules);
        var crisis = new StorytellerColonyState(3, 3, 0, 10, 20);
        var emitted = storyteller.Step(10, crisis);
        Require(emitted.Count == 1 && emitted[0].Code == "food_crisis" && emitted[0].Severity == 3,
            "a qualifying colony condition should emit its configured event");
        Require(storyteller.Step(11, crisis).Count == 0,
            "a continuously active condition must not spam duplicate events");

        var recovered = crisis with { Food = 30 };
        storyteller.Step(12, recovered);
        Require(storyteller.Step(13, crisis).Count == 0,
            "a cleared condition must still respect its cooldown");
        Require(storyteller.Step(15, crisis).Count == 1,
            "an active condition should trigger when its cooldown expires");

        var snapshot = storyteller.CaptureSnapshot();
        var restored = ColonyStoryteller.Restore(snapshot);
        Require(restored.Events.Select(x => (x.Id, x.Tick, x.Code)).SequenceEqual(
            storyteller.Events.Select(x => (x.Id, x.Tick, x.Code))),
            "storyteller events must round-trip through save snapshots");
        Require(restored.Step(16, crisis).Count == 0,
            "active rules must remain active after restore without duplicate alerts");

        var badRulesRejected = false;
        try { _ = new ColonyStoryteller([rules[0], rules[0]]); }
        catch (ArgumentException) { badRulesRejected = true; }
        Require(badRulesRejected, "duplicate rule codes must be rejected");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Colony storyteller check failed: {message}");
    }
}
