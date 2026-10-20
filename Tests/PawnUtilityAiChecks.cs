using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class PawnUtilityAiChecks
{
    public static void Run()
    {
        var needs = new[]
        {
            new PawnNeed(PawnNeedKind.Hunger, 0.12f),
            new PawnNeed(PawnNeedKind.Sleep, 0.35f)
        };
        var offers = new[]
        {
            new SmartObjectOffer(1, "bed-1", "sleep", PawnNeedKind.Sleep, new CellPosition(1, 0), 0.8f),
            new SmartObjectOffer(2, "meal-1", "eat", PawnNeedKind.Hunger, new CellPosition(1, 0), 1f),
            new SmartObjectOffer(3, "meal-closed", "eat", PawnNeedKind.Hunger, new CellPosition(0, 0), 1f, IsAvailable: false)
        };

        var choice = PawnUtilityPlanner.Choose(new CellPosition(0, 0), needs, offers);
        Check(choice is not null && choice.Offer.Id == 2 && choice.Need == PawnNeedKind.Hunger,
            "utility planner must prioritize a desperate need while accounting for distance and action availability");

        var personality = new PawnPersonality();
        personality.SetWeight(PawnNeedKind.Sleep, 3f);
        var personalityChoice = PawnUtilityPlanner.Choose(new CellPosition(0, 0), needs, offers, personality);
        Check(personalityChoice is not null && personalityChoice.Offer.Id == 1,
            "resident personality weights must be able to change the best offered action");

        var satisfied = PawnUtilityPlanner.Choose(new CellPosition(0, 0),
            [new PawnNeed(PawnNeedKind.Hunger, 1f)],
            [new SmartObjectOffer(4, "meal", "eat", PawnNeedKind.Hunger, new CellPosition(0, 0), 1f)]);
        Check(satisfied is null,
            "a fully satisfied need must not cause a pointless smart-object action");

        var invalidAndMissing = PawnUtilityPlanner.Choose(new CellPosition(0, 0),
            [new PawnNeed(PawnNeedKind.Hunger, float.NaN)],
            [
                new SmartObjectOffer(5, "invalid", "eat", PawnNeedKind.Hunger, new CellPosition(0, 0), float.NaN),
                new SmartObjectOffer(6, "meal", "eat", PawnNeedKind.Hunger, new CellPosition(0, 0), 0.5f)
            ]);
        Check(invalidAndMissing is not null && invalidAndMissing.Offer.Id == 6,
            "planner must ignore invalid gains and normalize invalid need values safely");

        Console.WriteLine("Pawn utility AI checks passed: urgency, distance, availability, personality and invalid data.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
