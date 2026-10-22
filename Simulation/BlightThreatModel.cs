namespace KorTheBlight.Simulation;

/// <summary>
/// Separates Blight aggression from colony wealth. Environmental contamination and scarcity
/// increase pressure; the threat budget is intentionally not multiplied by current wealth.
/// </summary>
internal readonly record struct BlightThreatAssessment(double Pressure, int ThreatPoints)
{
    public bool ShouldTrigger => Pressure >= BlightThreatModel.IncidentThreshold;
}

internal static class BlightThreatModel
{
    public const double IncidentThreshold = 0.42d;
    private const double ScarcityReferenceWealth = 5_000d;

    public static BlightThreatAssessment Assess(double localEnvironmentalPressure, double macroBlightPresence, double colonyWealth)
    {
        if (!double.IsFinite(localEnvironmentalPressure) || localEnvironmentalPressure is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(localEnvironmentalPressure));
        if (!double.IsFinite(macroBlightPresence) || macroBlightPresence is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(macroBlightPresence));
        if (!double.IsFinite(colonyWealth) || colonyWealth < 0d)
            throw new ArgumentOutOfRangeException(nameof(colonyWealth));

        // Low wealth increases scarcity pressure instead of making the colony appear safer.
        // Local and regional Blight remain independent inputs, so high wealth cannot cancel them.
        double scarcityPressure = ScarcityReferenceWealth / (colonyWealth + ScarcityReferenceWealth);
        double pressure = Math.Clamp(
            0.12d + localEnvironmentalPressure * 0.35d + macroBlightPresence * 0.25d + scarcityPressure * 0.48d,
            0d, 1d);

        // Threat strength is derived from pressure, not wealth. A rich colony can still face
        // a serious Blight assault, while a ruined colony can face an escalating one.
        int threatPoints = Math.Clamp((int)Math.Round(15d + pressure * 235d), 25, 250);
        return new BlightThreatAssessment(pressure, threatPoints);
    }
}
