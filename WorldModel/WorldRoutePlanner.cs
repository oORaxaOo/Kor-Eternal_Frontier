namespace KorTheBlight.WorldModel;

internal readonly record struct WorldRouteNode(int X, int Y);
internal sealed record WorldRouteResult(IReadOnlyList<WorldRouteNode> Nodes, double TravelCost, double EstimatedSeconds);

/// <summary>
/// Coarse 40x40 strategic route grid. Biomes influence movement cost; deterministic A* estimates
/// route distance and travel time without replacing the local colony's independent 40x26 A*.
/// </summary>
internal static class WorldRoutePlanner
{
    public const int GridSize = 40;
    private const double CellSize = ContinuousWorldState.WorldExtent / GridSize;

    public static WorldRouteResult FindRoute(ContinuousWorldState world, double fromX, double fromY,
        double toX, double toY, double speed)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!double.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        var start = ToNode(fromX, fromY);
        var goal = ToNode(toX, toY);
        var open = new PriorityQueue<WorldRouteNode, double>();
        var cameFrom = new Dictionary<WorldRouteNode, WorldRouteNode>();
        var cost = new Dictionary<WorldRouteNode, double> { [start] = 0 };
        open.Enqueue(start, Heuristic(start, goal));
        int expanded = 0;
        while (open.Count > 0 && expanded++ < GridSize * GridSize)
        {
            var current = open.Dequeue();
            if (current == goal) break;
            foreach (var next in Neighbours(current))
            {
                double nextCost = cost[current] + MovementCost(world, next);
                if (cost.TryGetValue(next, out double known) && nextCost >= known) continue;
                cost[next] = nextCost;
                cameFrom[next] = current;
                open.Enqueue(next, nextCost + Heuristic(next, goal));
            }
        }
        if (start != goal && !cameFrom.ContainsKey(goal))
            return new WorldRouteResult([start, goal], Euclidean(fromX, fromY, toX, toY), Euclidean(fromX, fromY, toX, toY) / speed);
        var nodes = new List<WorldRouteNode> { goal };
        while (nodes[^1] != start) nodes.Add(cameFrom[nodes[^1]]);
        nodes.Reverse();
        double distance = cost.GetValueOrDefault(goal) * CellSize;
        return new WorldRouteResult(nodes, distance, distance / speed);
    }

    private static WorldRouteNode ToNode(double x, double y) => new(
        Math.Clamp((int)(Math.Clamp(x, 0, ContinuousWorldState.WorldExtent - 1) / CellSize), 0, GridSize - 1),
        Math.Clamp((int)(Math.Clamp(y, 0, ContinuousWorldState.WorldExtent - 1) / CellSize), 0, GridSize - 1));
    private static IEnumerable<WorldRouteNode> Neighbours(WorldRouteNode node)
    {
        if (node.X > 0) yield return new(node.X - 1, node.Y);
        if (node.X + 1 < GridSize) yield return new(node.X + 1, node.Y);
        if (node.Y > 0) yield return new(node.X, node.Y - 1);
        if (node.Y + 1 < GridSize) yield return new(node.X, node.Y + 1);
    }
    private static double MovementCost(ContinuousWorldState world, WorldRouteNode node)
    {
        double x = (node.X + .5) * CellSize, y = (node.Y + .5) * CellSize;
        var region = world.RegionalMap.Regions.FirstOrDefault(item =>
            x >= item.X * RegionalWorldMap.RegionSize && x < (item.X + 1) * RegionalWorldMap.RegionSize &&
            y >= item.Y * RegionalWorldMap.RegionSize && y < (item.Y + 1) * RegionalWorldMap.RegionSize);
        string biome = region?.Biome.ToString() ?? "Plains";
        return biome.Contains("Mountain", StringComparison.OrdinalIgnoreCase) ? 2.4 :
               biome.Contains("Wetland", StringComparison.OrdinalIgnoreCase) ? 1.9 :
               biome.Contains("Desert", StringComparison.OrdinalIgnoreCase) ? 1.45 :
               biome.Contains("Forest", StringComparison.OrdinalIgnoreCase) ? 1.25 : 1.0;
    }
    private static double Heuristic(WorldRouteNode a, WorldRouteNode b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    private static double Euclidean(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
