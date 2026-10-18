using KorTheBlight.Campaign;

namespace KorTheBlight.Tests;

internal static class WorldMapChecks
{
    public static void Run()
    {
        var world = new WorldMapSimulation(70241);
        Check(world.Territories.Count == WorldMapSimulation.Width * WorldMapSimulation.Height,
            "the strategic world must contain a large, fixed territory grid");
        Check(world.Territories.Select(t => (t.X, t.Y)).Distinct().Count() == world.Territories.Count,
            "every world territory must occupy a unique coordinate");
        Check(world.Territories.Any(t => t.Owner == WorldFactionKind.Kor && t.HasSettlement),
            "the Kor must begin with a strategic capital");
        Check(world.Territories.Any(t => t.Owner == WorldFactionKind.Blight),
            "the world must begin with a visible Blight-controlled region");
        Check(world.Territories.Select(t => t.Owner).Distinct().Count() >= 5,
            "multiple independent powers must make territorial diplomacy meaningful");
        var sample = world.Territories.First(t => t.Owner == WorldFactionKind.AshenCompact);
        world.SelectAt(sample.X, sample.Y);
        Check(world.Selected.Id == sample.Id, "world-map selection must resolve the clicked territory");
        Check(world.RelationTo(WorldFactionKind.AshenCompact) == DiplomaticStatus.Neutral,
            "initial Kor relations with the Ashen Compact should be neutral");
        world.SelectAt(4, 8);
        Check(world.CanClaimSelected, "an adjacent unclaimed frontier territory should be claimable with the starting treasury");
        Check(world.State.EmpireSupplies >= 40 && world.State.EmpireAlloy >= 25,
            "world expansion should be backed by explicit empire-level resource balances");
        Check(world.Neighbours(sample.X, sample.Y).All(t =>
                Math.Abs(t.X - sample.X) + Math.Abs(t.Y - sample.Y) == 1),
            "world territory adjacency must remain orthogonal and deterministic");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
