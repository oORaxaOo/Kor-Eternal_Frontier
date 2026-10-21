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

    /// <summary>
    /// Deterministic A* over an immutable, precomputed macro-cell cost snapshot.
    /// Safe to run on a worker thread: this overload never consults the live world model.
    /// IDs are 1-based row-major cell identifiers.
    /// </summary>
    public static IReadOnlyList<WorldRouteNode> FindMacroRoute(float[] movementCosts, int gridSize,
        int startCellId, int targetCellId)
    {
        ArgumentNullException.ThrowIfNull(movementCosts);
        if (gridSize <= 0 || movementCosts.Length != checked(gridSize * gridSize))
            throw new ArgumentException("Macro route cost snapshot must be a square grid.", nameof(movementCosts));
        int total = movementCosts.Length;
        if (startCellId < 1 || startCellId > total) throw new ArgumentOutOfRangeException(nameof(startCellId));
        if (targetCellId < 1 || targetCellId > total) throw new ArgumentOutOfRangeException(nameof(targetCellId));
        if (movementCosts.Any(cost => !float.IsFinite(cost) || cost <= 0))
            throw new ArgumentException("Macro route costs must be finite and positive.", nameof(movementCosts));

        int start = startCellId - 1, goal = targetCellId - 1;
        if (start == goal)
            return [new WorldRouteNode(start % gridSize, start / gridSize)];

        var open = new PriorityQueue<int, (float Score, int Index)>();
        var cameFrom = new int[total];
        Array.Fill(cameFrom, -1);
        var costs = new float[total];
        Array.Fill(costs, float.PositiveInfinity);
        var closed = new bool[total];
        costs[start] = 0f;
        open.Enqueue(start, (Heuristic(start, goal, gridSize), start));
        Span<int> neighbours = stackalloc int[4];

        while (open.TryDequeue(out int current, out _))
        {
            if (closed[current]) continue;
            if (current == goal) break;
            closed[current] = true;
            int x = current % gridSize, y = current / gridSize;
            int count = 0;
            if (x > 0) neighbours[count++] = current - 1;
            if (x + 1 < gridSize) neighbours[count++] = current + 1;
            if (y > 0) neighbours[count++] = current - gridSize;
            if (y + 1 < gridSize) neighbours[count++] = current + gridSize;

            for (int i = 0; i < count; i++)
            {
                int next = neighbours[i];
                if (closed[next]) continue;
                float candidate = costs[current] + movementCosts[next];
                if (candidate >= costs[next]) continue;
                costs[next] = candidate;
                cameFrom[next] = current;
                open.Enqueue(next, (candidate + Heuristic(next, goal, gridSize), next));
            }
        }

        if (cameFrom[goal] < 0)
            throw new InvalidOperationException($"No macro route exists between cells {startCellId} and {targetCellId}.");

        var reversed = new List<WorldRouteNode>();
        int cursor = goal;
        reversed.Add(new WorldRouteNode(cursor % gridSize, cursor / gridSize));
        while (cursor != start)
        {
            cursor = cameFrom[cursor];
            if (cursor < 0) throw new InvalidOperationException("Macro route predecessor chain is incomplete.");
            reversed.Add(new WorldRouteNode(cursor % gridSize, cursor / gridSize));
            if (reversed.Count > total) throw new InvalidOperationException("Macro route contains a cycle.");
        }
        reversed.Reverse();
        return reversed;
    }

    private static float Heuristic(int from, int to, int gridSize)
        => Math.Abs(from % gridSize - to % gridSize) + Math.Abs(from / gridSize - to / gridSize);

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
