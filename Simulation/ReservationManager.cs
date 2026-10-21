namespace KorTheBlight.Simulation;

internal readonly record struct ReservationKey(string TargetId, int PawnId);
internal sealed record ResourceReservation(string TargetId, int PawnId, int StackCount);

/// <summary>
/// Authoritative, bounded reservations for physical targets and stack quantities. Calls are
/// deterministic and atomic from the simulation thread; callers release reservations on job end.
/// </summary>
internal sealed class ReservationManager
{
    public const int MaximumReservations = 16_384;
    private readonly Dictionary<ReservationKey, int> _reservations = [];
    public int Count => _reservations.Count;
    public IReadOnlyCollection<ResourceReservation> Reservations => _reservations
        .OrderBy(item => item.Key.TargetId, StringComparer.Ordinal).ThenBy(item => item.Key.PawnId)
        .Select(item => new ResourceReservation(item.Key.TargetId, item.Key.PawnId, item.Value)).ToArray();

    public int ReservedByOthers(string targetId, int pawnId) =>
        _reservations.Where(item => item.Key.TargetId == targetId && item.Key.PawnId != pawnId).Sum(item => item.Value);

    public bool Reserve(string targetId, int pawnId, int stackCount, int availableStackCount)
    {
        if (string.IsNullOrWhiteSpace(targetId) || pawnId <= 0 || stackCount <= 0 || availableStackCount < 0)
            return false;
        var key = new ReservationKey(targetId, pawnId);
        int others = ReservedByOthers(targetId, pawnId);
        if (others + stackCount > availableStackCount) return false;
        if (!_reservations.ContainsKey(key) && _reservations.Count >= MaximumReservations) return false;
        _reservations[key] = stackCount;
        return true;
    }

    public bool IsReservedByOther(string targetId, int pawnId) =>
        _reservations.Keys.Any(key => key.TargetId == targetId && key.PawnId != pawnId);

    public void Release(string targetId, int pawnId) => _reservations.Remove(new ReservationKey(targetId, pawnId));
    public void ReleasePawn(int pawnId)
    {
        foreach (var key in _reservations.Keys.Where(key => key.PawnId == pawnId).ToArray())
            _reservations.Remove(key);
    }
    public void ReleaseTarget(string targetId)
    {
        foreach (var key in _reservations.Keys.Where(key => key.TargetId == targetId).ToArray())
            _reservations.Remove(key);
    }
    public void Clear() => _reservations.Clear();
}
