using System.Text.Json;
using KorTheBlight.Simulation;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Campaign;

/// <summary>Atomic, versioned save envelope for the local settlement and continuous world state.</summary>
internal static class GameSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SavePath
    {
        get
        {
            string root = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(root, "kor-eternal-frontier", "session.json");
        }
    }

    /// <summary>Compatibility entry point for callers that do not yet own a WorldSimulation.</summary>
    public static string Serialize(ColonySimulation colony, CampaignSimulation campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        return Serialize(colony, campaign, ContinuousWorldState.Generate(campaign.State.Seed));
    }

    public static string Serialize(WorldSimulation world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Serialize(world.Colony, world.World, world.SpatialWorld);
    }

    public static string Serialize(ColonySimulation colony, CampaignSimulation campaign, ContinuousWorldState continuousWorld)
    {
        ArgumentNullException.ThrowIfNull(colony);
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(continuousWorld);
        // Validate each subsystem before committing one atomic session snapshot.
        return JsonSerializer.Serialize(new SessionEnvelope
        {
            SchemaVersion = 2,
            ColonyJson = ColonySaveService.Serialize(colony),
            CampaignJson = CampaignSaveService.Serialize(campaign.State),
            ContinuousWorldJson = ContinuousWorldState.Serialize(continuousWorld)
        }, JsonOptions);
    }

    public static LoadedGameSession Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Game session save is empty.");
        SessionEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SessionEnvelope>(json, JsonOptions)
                ?? throw new InvalidDataException("Game session save is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Game session save is not valid JSON: {error.Message}", error);
        }

        if (envelope.SchemaVersion is not (1 or 2))
            throw new InvalidDataException($"Unsupported game session schema version: {envelope.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(envelope.ColonyJson) || string.IsNullOrWhiteSpace(envelope.CampaignJson))
            throw new InvalidDataException("Game session is missing its colony or legacy migration state.");

        var colony = ColonySaveService.Deserialize(envelope.ColonyJson);
        var campaign = new CampaignSimulation(CampaignSaveService.Deserialize(envelope.CampaignJson));
        // Version 1 saves predate continuous world-space entities. Migrate them deterministically
        // from the existing seed instead of trying to reinterpret hex adjacency as world geometry.
        var continuousWorld = envelope.SchemaVersion == 1 || string.IsNullOrWhiteSpace(envelope.ContinuousWorldJson)
            ? ContinuousWorldState.Generate(campaign.State.Seed)
            : ContinuousWorldState.Deserialize(envelope.ContinuousWorldJson);
        return new LoadedGameSession(colony, campaign, continuousWorld);
    }

    public static void Save(WorldSimulation world, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        SaveJson(Serialize(world), path);
    }

    /// <summary>Compatibility entry point; new gameplay must save the complete WorldSimulation.</summary>
    public static void Save(ColonySimulation colony, CampaignSimulation campaign, string? path = null)
        => SaveJson(Serialize(colony, campaign), path);

    private static void SaveJson(string json, string? path)
    {
        string target = path ?? SavePath;
        string directory = Path.GetDirectoryName(Path.GetFullPath(target))
            ?? throw new InvalidOperationException("Could not determine the game session save directory.");
        Directory.CreateDirectory(directory);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static LoadedGameSession Load(string? path = null)
    {
        string target = path ?? SavePath;
        if (!File.Exists(target)) throw new FileNotFoundException("No Kor Eternal Frontier session save exists yet.", target);
        return Deserialize(File.ReadAllText(target));
    }

    private sealed class SessionEnvelope
    {
        public int SchemaVersion { get; set; } = 2;
        public string ColonyJson { get; set; } = string.Empty;
        public string CampaignJson { get; set; } = string.Empty;
        public string ContinuousWorldJson { get; set; } = string.Empty;
    }
}

internal sealed record LoadedGameSession(
    ColonySimulation Colony,
    CampaignSimulation Campaign,
    ContinuousWorldState ContinuousWorld);
