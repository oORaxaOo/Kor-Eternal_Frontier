using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class PawnHealthChecks
{
    public static void Run()
    {
        var health = new PawnHealthState();
        Check(Math.Abs(health.OverallHealth - 100f) < 0.001f &&
              health.BodyParts.Count == Enum.GetValues<PawnBodyPartKind>().Length,
            "a new pawn must have the expected anatomical baseline");

        var injury = health.ApplyDamage(PawnBodyPartKind.Head, PawnDamageKind.Ballistic, 12f, 0d);
        Check(health.PartHealth(PawnBodyPartKind.Head) < 15f &&
              health.Consciousness < 1f && health.NeedsMedicalCare && injury.BleedingPerMinute > 0f,
            "ballistic head trauma must change health, consciousness, bleeding, and medical urgency");

        Check(health.TreatInjury(injury.Id), "existing injuries must be treatable");
        for (int i = 0; i < 600; i++) health.Advance(0.1f);
        Check(health.BloodLoss < 5f && health.Injuries.Single().IsTreated,
            "treated bleeding must remain controlled during simulation time");

        var saved = health.CaptureSnapshot();
        var restored = new PawnHealthState();
        restored.RestoreSnapshot(saved);
        Check(Math.Abs(restored.OverallHealth - health.OverallHealth) < 0.001f &&
              Math.Abs(restored.BloodLoss - health.BloodLoss) < 0.001f &&
              restored.Injuries.Single().Id == injury.Id,
            "body-part condition and wound history must round-trip through health snapshots");

        bool invalidRejected = false;
        saved.BodyParts[0].CurrentHealth = float.NaN;
        try { saved.Validate(); }
        catch (InvalidDataException) { invalidRejected = true; }
        Check(invalidRejected, "health snapshot validation must reject non-finite anatomical state");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Pawn health check failed: {message}");
    }
}
