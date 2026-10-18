using System.Text.Json;

namespace KorTheBlight.Simulation;

/// <summary>Player-editable balance for the contained starter scenario.</summary>
internal sealed class ScenarioBalance
{
    public int StartingAlloy { get; init; } = 140;
    public int StartingSupplies { get; init; } = 120;
    public float WorkerSpeed { get; init; } = 1.8f;
    public float WorkIntervalSeconds { get; init; } = 1.25f;
    public int AlloyPerGather { get; init; } = 12;
    public int SuppliesPerGather { get; init; } = 10;
    public int PurifierAlloyCost { get; init; } = 80;
    public int PurifierSuppliesCost { get; init; } = 40;
    public float PurifierWorkPerInterval { get; init; } = .13f;
    public float BlightSpreadSeconds { get; init; } = 3;
    public int BlightSpreadSkipEvery { get; init; } = 3;
    public float PurifierRange { get; init; } = 2.2f;
    public float ShelterDamagePerSecond { get; init; } = 3;

    public static ScenarioBalance Load(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Balance file missing at {path}; built-in defaults will be used.");
            return new ScenarioBalance();
        }
        try
        {
            var balance = JsonSerializer.Deserialize<ScenarioBalance>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The balance file was empty.");
            balance.Validate();
            return balance;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new InvalidDataException($"Invalid balance file '{path}': {ex.Message}", ex);
        }
    }

    private void Validate()
    {
        if (StartingAlloy < 0 || StartingSupplies < 0 || AlloyPerGather <= 0 || SuppliesPerGather <= 0 || PurifierAlloyCost <= 0 || PurifierSuppliesCost <= 0)
            throw new InvalidDataException("Resource amounts and costs must be non-negative; gathering amounts and purifier costs must be positive.");
        if (!float.IsFinite(WorkerSpeed) || WorkerSpeed is <= 0 or > 20 || !float.IsFinite(WorkIntervalSeconds) || WorkIntervalSeconds is <= 0 or > 60)
            throw new InvalidDataException("WorkerSpeed must be in (0, 20] and WorkIntervalSeconds in (0, 60].");
        if (!float.IsFinite(PurifierWorkPerInterval) || PurifierWorkPerInterval is <= 0 or > 1 || !float.IsFinite(BlightSpreadSeconds) || BlightSpreadSeconds is <= 0 or > 120)
            throw new InvalidDataException("PurifierWorkPerInterval must be in (0, 1] and BlightSpreadSeconds in (0, 120].");
        if (BlightSpreadSkipEvery is < 2 or > 20 || !float.IsFinite(PurifierRange) || PurifierRange is <= 0 or > 10 || !float.IsFinite(ShelterDamagePerSecond) || ShelterDamagePerSecond is < 0 or > 100)
            throw new InvalidDataException("Spread interval, purifier range, or shelter damage is outside the supported range.");
    }
}
