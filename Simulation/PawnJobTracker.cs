namespace KorTheBlight.Simulation;

internal enum PawnToilKind { MoveTo, Interact, Work, Complete }
internal sealed record PawnToil(PawnToilKind Kind, string Label, CellPosition? Target = null);

/// <summary>Ordered phases for one pawn job, suitable for movement, interaction and work steps.</summary>
internal sealed class PawnJobTracker
{
    private readonly List<PawnToil> _toils;
    public int CurrentToilIndex { get; private set; }
    public IReadOnlyList<PawnToil> Toils => _toils;
    public PawnToil? CurrentToil => CurrentToilIndex < _toils.Count ? _toils[CurrentToilIndex] : null;
    public bool IsComplete => CurrentToilIndex >= _toils.Count;

    public PawnJobTracker(ColonyTaskKind jobKind, CellPosition target)
    {
        string action = jobKind switch
        {
            ColonyTaskKind.Move => "Reach destination",
            ColonyTaskKind.Gather => "Collect resource",
            ColonyTaskKind.Deliver => "Deliver carried resources",
            ColonyTaskKind.BuildTile => "Construct tile",
            ColonyTaskKind.BuildObject => "Construct object",
            ColonyTaskKind.Produce => "Produce at workstation",
            ColonyTaskKind.Mine => "Excavate tile",
            _ => throw new ArgumentOutOfRangeException(nameof(jobKind))
        };
        _toils = [new PawnToil(PawnToilKind.MoveTo, "Move to target", target)];
        if (jobKind != ColonyTaskKind.Move) _toils.Add(new PawnToil(PawnToilKind.Work, action, target));
        _toils.Add(new PawnToil(PawnToilKind.Complete, "Finish job"));
    }
    public bool AdvanceToNextToil()
    { if (IsComplete) return false; CurrentToilIndex++; return true; }
    public void Finish() => CurrentToilIndex = _toils.Count;
}
