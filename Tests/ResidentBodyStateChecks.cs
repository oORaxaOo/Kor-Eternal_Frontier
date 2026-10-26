using KorTheBlight.Simulation;
namespace KorTheBlight.Tests;
internal static class ResidentBodyStateChecks
{
    public static void Run()
    {
        var body = new ResidentBodyState();
        Check(body.TotalHealth == 100f && body.ActiveInjuryCount == 0, "a newly created resident starts healthy");
        bool newInjury = body.ApplyTrauma(ResidentBodyPart.LeftLeg, ResidentTraumaKind.Fracture, 60f, 12);
        Check(newInjury && body.Parts[ResidentBodyPart.LeftLeg] == 40f && body.ActiveInjuryCount == 1,
            "localized trauma must lower the affected body part and create an injury record");
        Check(body.MovementMultiplier < 1f && body.WorkMultiplier == 1f,
            "leg injuries affect movement without incorrectly damaging arm-based work");
        Check(!body.ApplyTrauma(ResidentBodyPart.LeftLeg, ResidentTraumaKind.Fracture, 5f, 13) &&
              body.ActiveInjuryCount == 1, "repeated trauma of one type consolidates rather than spamming records");
        body.ApplyTrauma(ResidentBodyPart.RightArm, ResidentTraumaKind.Cut, 70f, 14);
        Check(body.WorkMultiplier < 1f, "arm injuries must reduce actual construction and production work rates");

        var saved = body.CaptureSnapshot();
        Check(saved.IsValid(42), "body state snapshot validates");
        var restored = new ResidentBodyState();
        restored.RestoreSnapshot(saved, 42);
        Check(Math.Abs(restored.TotalHealth - body.TotalHealth) < 0.001f &&
              restored.Injuries.SequenceEqual(body.Injuries) &&
              Math.Abs(restored.MovementMultiplier - body.MovementMultiplier) < 0.001f,
            "part health, injuries and derived movement state persist across save/load");
        var invalid = body.CaptureSnapshot();
        invalid.Parts.RemoveAt(0);
        Check(!invalid.IsValid(42), "malformed body part collections are rejected");

        var thermal = new ResidentBodyState();
        Check(thermal.Advance(1f, 100f, 50f, 100, false, false) &&
              thermal.Injuries.Single().Kind == ResidentTraumaKind.ThermalBurn,
            "sustained severe heat exposure creates a physical injury");
        Check(thermal.Advance(1f, 100f, 50f, 101, false, false) == false,
            "continued exposure consolidates the existing injury record");
        float injuredHealth = thermal.TotalHealth;
        thermal.Advance(100f, 0f, 20f, 200, true, true);
        Check(thermal.TotalHealth > injuredHealth && thermal.ActiveInjuryCount == 0,
            "safe rest gradually heals health and resolves mild injuries");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Resident body check failed: {message}");
    }
}
