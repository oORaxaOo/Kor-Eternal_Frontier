using System.Text.Json;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation.Research;

internal sealed class ResearchProject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float CostPoints { get; set; }
    public List<string> Prerequisites { get; set; } = [];
    public List<string> UnlockedTechTags { get; set; } = [];
    public string Epoch { get; set; } = "Frontier";
}

internal sealed class ItemDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int MarketValue { get; set; }
    public string SymbolColor { get; set; } = "#B8C5B0";
    public string? RequiredResearchId { get; set; }
}

internal sealed class ResearchProgressState
{
    public string? CurrentResearchId { get; set; }
    public float CurrentProgressPoints { get; set; }
    public HashSet<string> CompletedResearchIds { get; set; } = [];
}

/// <summary>Runtime content catalogue with stable IDs, data-file overrides, and deterministic generated fallbacks.</summary>
internal sealed class DatabaseBootstrap
{
    public Dictionary<string, ItemDefinition> ItemDatabase { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ResearchProject> ResearchDatabase { get; } = new(StringComparer.Ordinal);

    private static readonly string[] Materials =
    [
        "Iron", "Copper", "Tin", "Lead", "Zinc", "Silver", "Gold", "Platinum", "Titanium", "Cobalt",
        "Nickel", "Tungsten", "Lithium", "Aluminium", "Chromium", "Oak", "Pine", "MushroomFiber",
        "BlightWood", "Granite", "Marble", "Slate", "Limestone", "Basalt", "Obsidian", "Quartz",
        "Coal", "Sulfur", "Phosphorus", "Silicon", "Saltpeter", "BlightSpore", "RotCrystal", "XenoResin", "Chitin"
    ];
    private static readonly string[] Stages = ["Ore", "Raw", "Ingot", "Plate", "Wire", "Gear", "Pipe", "Dust", "Alloy", "Component"];
    private static readonly string[] Epochs = ["Frontier", "Bronze", "Iron", "Steam", "Electricity", "Atomic", "Digital", "Quantum", "BlightStudies"];

    public void LoadOrGenerate(string? dataDirectory = null)
    {
        ResearchDatabase.Clear();
        ItemDatabase.Clear();
        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            TryLoadProjects(Path.Combine(dataDirectory, "research_tree.json"));
            TryLoadItems(Path.Combine(dataDirectory, "items.json"));
        }
        if (ResearchDatabase.Count == 0) GenerateResearch();
        GenerateItems();
    }

    private void TryLoadProjects(string path)
    {
        if (!File.Exists(path)) return;
        var items = JsonSerializer.Deserialize<List<ResearchProject>>(File.ReadAllText(path));
        if (items is null) return;
        foreach (var project in items)
        {
            if (string.IsNullOrWhiteSpace(project.Id) || string.IsNullOrWhiteSpace(project.Name) ||
                !float.IsFinite(project.CostPoints) || project.CostPoints <= 0 || ResearchDatabase.ContainsKey(project.Id))
                throw new InvalidDataException($"Invalid or duplicate research definition '{project.Id}'.");
            ResearchDatabase.Add(project.Id, project);
        }
        ValidatePrerequisites();
    }

    private void TryLoadItems(string path)
    {
        if (!File.Exists(path)) return;
        var items = JsonSerializer.Deserialize<List<ItemDefinition>>(File.ReadAllText(path));
        if (items is null) return;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Label) ||
                item.MarketValue < 0 || !IsHexColor(item.SymbolColor) || ItemDatabase.ContainsKey(item.Id))
                throw new InvalidDataException($"Invalid or duplicate item definition '{item.Id}'.");
            ItemDatabase.Add(item.Id, item);
        }
    }

    private void GenerateResearch()
    {
        string? previous = null;
        int index = 1;
        foreach (string epoch in Epochs)
        for (int tier = 1; tier <= 8; tier++)
        {
            string id = $"tech_{epoch.ToLowerInvariant()}_{tier}";
            var project = new ResearchProject
            {
                Id = id, Name = $"{epoch} · Technology {tier}",
                Description = $"Unlocks new {epoch.ToLowerInvariant()}-era production and infrastructure options.",
                CostPoints = index * 500,
                Epoch = epoch,
                Prerequisites = previous is null ? [] : [previous],
                UnlockedTechTags = [$"Tech_{epoch}_{tier}"]
            };
            ResearchDatabase.Add(id, project);
            previous = id;
            index++;
        }
        ValidatePrerequisites();
    }

    private void GenerateItems()
    {
        var random = new Random(42);
        var projectIds = ResearchDatabase.Keys.Order(StringComparer.Ordinal).ToArray();
        foreach (string material in Materials)
        foreach (string stage in Stages)
        {
            string id = $"{stage.ToLowerInvariant()}_{material.ToLowerInvariant()}";
            if (ItemDatabase.ContainsKey(id)) continue;
            int projectIndex = Math.Min(projectIds.Length - 1, Array.IndexOf(projectIds, projectIds[random.Next(projectIds.Length)]));
            string projectId = projectIds[Math.Max(0, projectIndex)];
            int value = 5 + random.Next(496) + projectIndex * 12;
            ItemDatabase[id] = new ItemDefinition
            {
                Id = id, Label = $"{material} {stage}", Category = stage is "Ore" or "Raw" ? "RawMaterial" : "Industry",
                MarketValue = value, SymbolColor = ColorFor(stage, material), RequiredResearchId = projectId
            };
        }
        string[] advancedTypes = ["Module", "Processor", "Relay", "Catalyst", "Alloy", "Synthetic"];
        for (int i = 1; i <= 200; i++)
        {
            string id = $"advanced_item_{i:000}";
            if (ItemDatabase.ContainsKey(id)) continue;
            ItemDatabase[id] = new ItemDefinition
            {
                Id = id, Label = $"Frontier {advancedTypes[random.Next(advancedTypes.Length)]} Mk-{i}",
                Category = "HighTech", MarketValue = 1_000 + i * 10,
                SymbolColor = "#D85DDF", RequiredResearchId = projectIds[random.Next(projectIds.Length)]
            };
        }
    }

    private void ValidatePrerequisites()
    {
        foreach (var project in ResearchDatabase.Values)
            if (project.Prerequisites is null || project.Prerequisites.Any(id => !ResearchDatabase.ContainsKey(id) || id == project.Id))
                throw new InvalidDataException($"Research project '{project.Id}' has an unknown or self prerequisite.");
        foreach (var project in ResearchDatabase.Values) Visit(project.Id, new HashSet<string>(), new HashSet<string>());
        void Visit(string id, HashSet<string> active, HashSet<string> complete)
        {
            if (complete.Contains(id)) return;
            if (!active.Add(id)) throw new InvalidDataException($"Research dependency cycle includes '{id}'.");
            foreach (string dependency in ResearchDatabase[id].Prerequisites) Visit(dependency, active, complete);
            active.Remove(id);
            complete.Add(id);
        }
    }

    public static bool IsHexColor(string? value)
    {
        if (value is null || value.Length != 7 || value[0] != '#') return false;
        return value.AsSpan(1).ToString().All(Uri.IsHexDigit);
    }

    private static string ColorFor(string stage, string material)
    {
        if (material.Contains("Blight", StringComparison.OrdinalIgnoreCase) || material.Contains("Rot", StringComparison.OrdinalIgnoreCase)) return "#B82E66";
        if (stage == "Ore") return "#B96A48";
        if (stage is "Ingot" or "Alloy" or "Plate") return "#AAB3B8";
        if (stage == "Component") return "#D8B159";
        return "#6D9872";
    }
}

/// <summary>Progression manager; definitions stay data-driven and only completed project IDs are saved.</summary>
internal sealed class ResearchManager
{
    public DatabaseBootstrap Database { get; }
    public ResearchProgressState State { get; }

    public ResearchManager(DatabaseBootstrap database, ResearchProgressState? state = null)
    {
        Database = database ?? throw new ArgumentNullException(nameof(database));
        State = state ?? new ResearchProgressState();
        State.CompletedResearchIds ??= [];
        if (State.CompletedResearchIds.Any(id => !Database.ResearchDatabase.ContainsKey(id)))
            throw new InvalidDataException("Saved research references an unknown project.");
        if (State.CurrentResearchId is not null && !Database.ResearchDatabase.ContainsKey(State.CurrentResearchId))
            throw new InvalidDataException("Saved current research references an unknown project.");
    }

    public bool CanStart(string id)
        => Database.ResearchDatabase.TryGetValue(id, out var project) &&
           !State.CompletedResearchIds.Contains(id) &&
           project.Prerequisites.All(State.CompletedResearchIds.Contains);

    public bool Start(string id)
    {
        if (!CanStart(id)) return false;
        State.CurrentResearchId = id;
        State.CurrentProgressPoints = 0;
        return true;
    }

    public bool AddProgress(float points)
    {
        if (!float.IsFinite(points) || points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        if (State.CurrentResearchId is null || !Database.ResearchDatabase.TryGetValue(State.CurrentResearchId, out var project)) return false;
        State.CurrentProgressPoints = Math.Min(project.CostPoints, State.CurrentProgressPoints + points);
        if (State.CurrentProgressPoints < project.CostPoints) return false;
        State.CompletedResearchIds.Add(project.Id);
        State.CurrentResearchId = null;
        State.CurrentProgressPoints = 0;
        return true;
    }

    public bool IsTechUnlocked(string tag)
        => Database.ResearchDatabase.Values.Any(project => State.CompletedResearchIds.Contains(project.Id) && project.UnlockedTechTags.Contains(tag));

    public bool CanCraftItem(string itemId)
        => Database.ItemDatabase.TryGetValue(itemId, out var item) &&
           (string.IsNullOrWhiteSpace(item.RequiredResearchId) || State.CompletedResearchIds.Contains(item.RequiredResearchId));
}
