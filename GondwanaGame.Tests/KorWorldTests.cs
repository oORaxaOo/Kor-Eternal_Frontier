using KorFrontier.Gondwana.Simulation;
using Xunit;

namespace KorFrontier.Tests;

public sealed class KorWorldTests
{
    [Fact]
    public void SameSeedProducesSameTerrain()
    {
        var first = new KorWorld(12345);
        var second = new KorWorld(12345);

        for (int y = 0; y < KorWorld.Height; y++)
        for (int x = 0; x < KorWorld.Width; x++)
            Assert.Equal(first.GroundAt(x, y), second.GroundAt(x, y));
    }

    [Fact]
    public void SuccessfulBuildSpendsResourcesAndAddsStructure()
    {
        var world = new KorWorld(7);
        int startingSupplies = world.Supplies;
        int startingBuildings = world.Buildings.Count;

        Assert.True(world.TryBuild(KorBuildKind.Solar, 10, 10));

        Assert.Equal(startingBuildings + 1, world.Buildings.Count);
        Assert.Equal(startingSupplies - 90, world.Supplies);
    }

    [Fact]
    public void OverlappingBuildIsRejectedWithoutSpendingResources()
    {
        var world = new KorWorld(7);
        int startingSupplies = world.Supplies;
        int startingBuildings = world.Buildings.Count;

        Assert.False(world.TryBuild(KorBuildKind.Solar, 20, 20));

        Assert.Equal(startingBuildings, world.Buildings.Count);
        Assert.Equal(startingSupplies, world.Supplies);
    }

    [Fact]
    public void SelectedUnitReceivesAndFollowsMoveOrder()
    {
        var world = new KorWorld(7);
        var unit = Assert.Single(world.Units.Where(u => u.Kind == KorUnitKind.Pioneer && u.X == 19f));
        world.SelectAt(19f, 20f);
        world.OrderSelected(25f, 24f);

        for (int i = 0; i < 60; i++)
            world.Update(0.05f);

        Assert.True(unit.X > 19f);
        Assert.True(unit.Y > 20f);
        Assert.InRange(unit.X, 24.0f, 25.6f);
        Assert.InRange(unit.Y, 23.0f, 25.0f);
    }
}
