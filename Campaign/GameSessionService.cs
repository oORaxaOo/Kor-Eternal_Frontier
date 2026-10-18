using System.Text.Json;
using KorTheBlight.Simulation;

namespace KorTheBlight.Campaign;

/// <summary>Atomic, versioned save for the linked strategic campaign and local colony.</summary>
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

    public static string Serialize(ColonySimulation colony, CampaignSimulation campaign)
    {
        ArgumentNullException.ThrowIfNull(colony);
        ArgumentNullException.ThrowIfNull(campaign);
        // Each subsystem validates its own schema before it is included in the session envelope.
        return JsonSerializer.Serialize(new SessionEnvelope
        {
            SchemaVersion = 1,
            ColonyJson = ColonySaveService.Serialize(colony),
            CampaignJson = CampaignSaveService.Serialize(campaign.State)
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
        if (string.IsNullOrWhiteSpace(envelope.ColonyJson) || string.IsNullOrWhiteSpace(envelope.CampaignJson))
            throw new InvalidDataException("Game session is missing its colony or campaign state.");

        var colony = ColonySaveService.Deserialize(envelope.ColonyJson);
        var campaign = new CampaignSimulation(CampaignSaveService.Deserialize(envelope.CampaignJson));
        return new LoadedGameSession(colony, campaign);
    }

    public static void Save(ColonySimulation colony, CampaignSimulation campaign, string? path = null)
    {
        string target = path ?? SavePath;
        string directory = Path.GetDirectoryName(Path.GetFullPath(target))
            ?? throw new InvalidOperationException("Could not determine the game session save directory.");
        Directory.CreateDirectory(directory);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, Serialize(colony, campaign));
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
        public string CampaignJson { get; set; } = string.Empty;
    }
}

internal sealed record LoadedGameSession(ColonySimulation Colony, CampaignSimulation Campaign);
