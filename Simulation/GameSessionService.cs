using System.Text.Json;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation;

/// <summary>Atomic save envelope for the complete, canonical real-time world session.</summary>
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

    public static string Serialize(WorldSimulation world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Serialize(world.Colony, world.SpatialWorld);
    }

    public static string Serialize(ColonySimulation colony, ContinuousWorldState continuousWorld)
    {
        ArgumentNullException.ThrowIfNull(colony);
        ArgumentNullException.ThrowIfNull(continuousWorld);
        return JsonSerializer.Serialize(new SessionEnvelope
        {
            SchemaVersion = 1,
            ColonyJson = ColonySaveService.Serialize(colony),
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

        if (envelope.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported game session schema version: {envelope.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(envelope.ColonyJson) || string.IsNullOrWhiteSpace(envelope.ContinuousWorldJson))
            throw new InvalidDataException("Game session is missing its colony or continuous world state.");

        var colony = ColonySaveService.Deserialize(envelope.ColonyJson);
        var continuousWorld = ContinuousWorldState.Deserialize(envelope.ContinuousWorldJson);
        var home = continuousWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
        colony.BindWorldInventory(continuousWorld, home.Id);
        return new LoadedGameSession(colony, continuousWorld);
    }

    public static void Save(WorldSimulation world, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        SaveJson(Serialize(world), path);
    }

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
        public int SchemaVersion { get; set; } = 1;
        public string ColonyJson { get; set; } = string.Empty;
        public string ContinuousWorldJson { get; set; } = string.Empty;
    }
}

internal sealed record LoadedGameSession(
    ColonySimulation Colony,
    ContinuousWorldState ContinuousWorld);
