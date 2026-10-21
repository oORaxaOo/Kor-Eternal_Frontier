namespace KorTheBlight.Simulation;

/// <summary>Four-neighbour A* over a bounded grid with positive per-cell movement costs.</summary>
internal static class AStarPathfinder
{
    private static readonly CellPosition[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    public static bool TryFindPath(int width, int height, CellPosition start, CellPosition target,
        Func<CellPosition, bool> canEnter, Func<CellPosition, int> movementCost, out List<CellPosition> path)
    {
        ArgumentNullException.ThrowIfNull(canEnter);
        ArgumentNullException.ThrowIfNull(movementCost);
        path = [];
        if (width <= 0 || height <= 0 || !InBounds(start) || !InBounds(target) || !canEnter(target)) return false;
        if (start == target) return true;
        var scores = new int[width, height];
        var previous = new CellPosition?[width, height];
        var closed = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) scores[x, y] = int.MaxValue;
        var frontier = new PriorityQueue<CellPosition, (int Estimate, int Sequence)>();
        int sequence = 0;
        scores[start.X, start.Y] = 0;
        frontier.Enqueue(start, (Heuristic(start, target), sequence++));
        while (frontier.TryDequeue(out var current, out _))
        {
            if (closed[current.X, current.Y]) continue;
            if (current == target) break;
            closed[current.X, current.Y] = true;
            foreach (var direction in Directions)
            {
                var next = new CellPosition(current.X + direction.X, current.Y + direction.Y);
                if (!InBounds(next) || closed[next.X, next.Y] || !canEnter(next)) continue;
                int cost = Math.Max(1, movementCost(next));
                int currentScore = scores[current.X, current.Y];
                int tentative = currentScore > int.MaxValue - cost ? int.MaxValue : currentScore + cost;
                if (tentative >= scores[next.X, next.Y]) continue;
                scores[next.X, next.Y] = tentative;
                previous[next.X, next.Y] = current;
                int heuristic = Heuristic(next, target);
                int estimate = tentative > int.MaxValue - heuristic ? int.MaxValue : tentative + heuristic;
                frontier.Enqueue(next, (estimate, sequence++));
            }
        }
        if (scores[target.X, target.Y] == int.MaxValue) return false;
        var cursor = target;
        while (cursor != start)
        {
            path.Add(cursor);
            if (previous[cursor.X, cursor.Y] is not { } prior) { path.Clear(); return false; }
            cursor = prior;
        }
        path.Reverse();
        return true;

        bool InBounds(CellPosition cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
    }

    private static int Heuristic(CellPosition from, CellPosition to) =>
        Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
}
