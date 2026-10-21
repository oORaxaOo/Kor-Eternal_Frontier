using System.Collections.Concurrent;

namespace KorTheBlight.WorldModel;

internal sealed record BackgroundWorldRouteResult(
    long CaravanId,
    int TargetCellId,
    IReadOnlyList<WorldRouteNode>? Nodes,
    string? Error);

/// <summary>
/// Bounds off-thread A* work and transfers immutable results back to the simulation thread.
/// Worker tasks never read or mutate the live world, caravan objects, UI, or render state.
/// </summary>
internal sealed class BackgroundWorldRouteScheduler
{
    private readonly ConcurrentQueue<BackgroundWorldRouteResult> _completed = new();
    private readonly HashSet<long> _inFlight = [];
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;

    public int MaxConcurrentRoutes { get; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
    public int InFlightCount { get { AssertOwnerThread(); return _inFlight.Count; } }

    public bool IsInFlight(long caravanId)
    {
        AssertOwnerThread();
        return _inFlight.Contains(caravanId);
    }

    public bool TrySchedule(long caravanId, int targetCellId, int gridSize, int startCellId,
        float[] immutableMovementCosts)
    {
        AssertOwnerThread();
        ArgumentNullException.ThrowIfNull(immutableMovementCosts);
        if (caravanId <= 0) throw new ArgumentOutOfRangeException(nameof(caravanId));
        if (targetCellId <= 0 || targetCellId > gridSize * gridSize) throw new ArgumentOutOfRangeException(nameof(targetCellId));
        if (startCellId <= 0 || startCellId > gridSize * gridSize) throw new ArgumentOutOfRangeException(nameof(startCellId));
        if (gridSize <= 0 || immutableMovementCosts.Length != gridSize * gridSize)
            throw new ArgumentException("Route snapshot dimensions do not match its grid.", nameof(immutableMovementCosts));
        if (_inFlight.Count >= MaxConcurrentRoutes || !_inFlight.Add(caravanId)) return false;

        // This closure holds only scalar route inputs and an immutable snapshot array.
        // Task.Run is deliberately bounded by the main-thread scheduler.
        _ = Task.Run(() =>
        {
            try
            {
                var route = WorldRoutePlanner.FindMacroRoute(immutableMovementCosts, gridSize, startCellId, targetCellId);
                _completed.Enqueue(new BackgroundWorldRouteResult(caravanId, targetCellId, route, null));
            }
            catch (Exception error)
            {
                _completed.Enqueue(new BackgroundWorldRouteResult(caravanId, targetCellId, null,
                    $"{error.GetType().Name}: {error.Message}"));
            }
        });
        return true;
    }

    public List<BackgroundWorldRouteResult> DrainCompleted()
    {
        AssertOwnerThread();
        var drained = new List<BackgroundWorldRouteResult>();
        while (_completed.TryDequeue(out var result))
        {
            _inFlight.Remove(result.CaravanId);
            drained.Add(result);
        }
        return drained;
    }

    private void AssertOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Background route scheduling and result application must stay on the simulation thread.");
    }
}
