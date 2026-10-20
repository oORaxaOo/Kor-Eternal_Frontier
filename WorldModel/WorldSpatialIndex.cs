namespace KorTheBlight.WorldModel;

/// <summary>
/// Uniform spatial hash for continuous world entities. Candidate lookups inspect intersecting buckets;
/// exact distance filtering and stable ordering remain the caller's responsibility.
/// </summary>
internal sealed class WorldSpatialIndex
{
    public const double BucketSize = 1_000d;

    private readonly Dictionary<long, List<WorldEntityState>> _buckets = [];
    private readonly int _maxBucket;

    public WorldSpatialIndex(double worldExtent)
    {
        if (!double.IsFinite(worldExtent) || worldExtent <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldExtent));
        _maxBucket = (int)Math.Ceiling(worldExtent / BucketSize);
    }

    public int BucketCount => _buckets.Count;

    public void Rebuild(IEnumerable<WorldEntityState> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _buckets.Clear();
        foreach (var entity in entities)
        {
            int bx = Bucket(entity.X), by = Bucket(entity.Y);
            long key = Key(bx, by);
            if (!_buckets.TryGetValue(key, out var bucket))
                _buckets[key] = bucket = [];
            bucket.Add(entity);
        }
    }

    public List<WorldEntityState> QueryCandidates(double x, double y, double radius)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));

        int minX = Math.Clamp(Bucket(Math.Max(0, x - radius)), 0, _maxBucket);
        int minY = Math.Clamp(Bucket(Math.Max(0, y - radius)), 0, _maxBucket);
        int maxX = Math.Clamp(Bucket(x + radius), 0, _maxBucket);
        int maxY = Math.Clamp(Bucket(y + radius), 0, _maxBucket);
        var result = new List<WorldEntityState>();
        for (int bx = minX; bx <= maxX; bx++)
        for (int by = minY; by <= maxY; by++)
            if (_buckets.TryGetValue(Key(bx, by), out var bucket))
                result.AddRange(bucket);
        return result;
    }

    private int Bucket(double coordinate) => (int)Math.Floor(coordinate / BucketSize);
    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
}
