namespace KorTheBlight.Simulation;

/// <summary>
/// Data-first long-range progression catalogue. Entries are generated from named, stable families
/// so the project can grow into hundreds of authored unlocks without hard-coding every node into UI.
/// This catalogue is intentionally separate from the live colony until save migration and runtime
/// inventory/job contracts can consume it safely.
/// </summary>
internal static class ProgressionCatalog
{
    internal sealed record ResourceDef(string Id, string Name, string Group, int BaseValue, bool Renewable);
    internal sealed record ResearchDef(string Id, string Name, string Discipline, int Era, int CostKnowledge,
        int CostEnergy, IReadOnlyList<string> Prerequisites, string UnlockKind, string UnlockId, string Description);
    internal sealed record BuildingDef(string Id, string Name, string Family, int Era, int Footprint,
        int Work, IReadOnlyDictionary<string, int> Inputs, string Upkeep, string UnlockResearchId);
    internal sealed record UnitDef(string Id, string Name, string Role, int Era, int SupplyCost,
        int AlloyCost, int Upkeep, string UnlockResearchId);
    internal sealed record RecipeDef(string Id, string Name, string Industry, int Stage,
        IReadOnlyDictionary<string, int> Inputs, IReadOnlyDictionary<string, int> Outputs,
        int Work, string UnlockResearchId);

    private static readonly string[] Eras =
    [
        "Frontier", "Mechanised", "Industrial", "Electrified", "Atomic", "Networked",
        "Synthetic", "Post-Scarcity"
    ];

    private static readonly (string Id, string Name, string[] Focuses, string UnlockKind)[] Disciplines =
    [
        ("logistics", "Logistics", ["Field Operations", "Warehousing", "Haulage", "Rail Networks", "Automated Routing"], "building"),
        ("materials", "Materials Science", ["Composite Alloys", "Ceramics", "Polymers", "Smart Materials", "Adaptive Structures"], "recipe"),
        ("energy", "Energy Systems", ["Grid Stability", "Solar Conversion", "Fission Systems", "Fusion Systems", "Storage Density"], "building"),
        ("agriculture", "Food Systems", ["Soil Recovery", "Hydroponics", "Protein Synthesis", "Closed-Loop Farms", "Ecological Resilience"], "recipe"),
        ("industry", "Industrial Engineering", ["Machine Tools", "Assembly Lines", "Robotics", "Precision Fabrication", "Molecular Assembly"], "recipe"),
        ("computing", "Computing", ["Signal Processing", "Control Systems", "Distributed Computing", "Predictive Models", "Autonomous Planning"], "research"),
        ("medicine", "Medicine", ["Trauma Care", "Public Health", "Regenerative Care", "Neural Health", "Longevity Science"], "building"),
        ("defense", "Defence Engineering", ["Fortification", "Ballistics", "Sensors", "Electronic Warfare", "Autonomous Defence"], "unit"),
        ("exploration", "Exploration", ["Geological Survey", "Deep Drilling", "Remote Prospecting", "Orbital Survey", "Anomalous Frontiers"], "building"),
        ("ecology", "Ecology", ["Water Reclamation", "Waste Recovery", "Biodiversity", "Climate Control", "Biosphere Repair"], "recipe"),
        ("governance", "Colony Systems", ["Workforce Planning", "Civic Services", "Education Networks", "Supply Forecasting", "Crisis Coordination"], "building"),
        ("blight", "Blight Studies", ["Containment Theory", "Sample Handling", "Adaptive Pathogens", "Resonance Mapping", "Counter-Evolution"], "research")
    ];

    private static readonly (string Id, string Name, string[] Stages, string Input, string Output)[] Industries =
    [
        ("sawmill", "Timber Processing", ["Debarked Logs", "Lumber", "Kiln-Dried Lumber", "Engineered Boards", "Structural Laminates", "Carbonised Fibre", "Smart Timber", "Molecular Timber"], "wood", "lumber"),
        ("masonry", "Stoneworks", ["Crushed Stone", "Cut Blocks", "Reinforced Masonry", "Ceramic Aggregate", "Pressure-Cast Stone", "Heat Shield Tile", "Self-Healing Masonry", "Adaptive Ceramic"], "stone", "building_material"),
        ("metallurgy", "Metallurgy", ["Sorted Ore", "Metal Ingots", "Alloy Stock", "Hardened Alloy", "Conductive Alloy", "Superalloy", "Programmable Metal", "Atomic Lattice"], "ore", "alloy"),
        ("chemistry", "Chemical Processing", ["Crude Reagents", "Industrial Solvent", "Polymer Resin", "Synthetic Rubber", "Advanced Polymer", "Reactive Gel", "Programmable Matter", "Molecular Feedstock"], "reagents", "polymer"),
        ("electronics", "Electronics", ["Wire Spools", "Circuit Boards", "Control Modules", "Microprocessors", "Quantum Chips", "Photonic Arrays", "Neural Lattices", "Cognitive Cores"], "alloy", "components"),
        ("power", "Power Equipment", ["Copper Windings", "Basic Generator", "Turbine Assembly", "Grid Inverter", "High-Density Cell", "Fission Core", "Fusion Core", "Vacuum Energy Cell"], "alloy", "power_module"),
        ("food", "Food Processing", ["Preserved Rations", "Flour and Oil", "Protein Meal", "Nutrient Paste", "Cultured Protein", "Complete Nutrition", "Adaptive Rations", "Closed-Loop Nutrition"], "food", "rations"),
        ("medicine", "Pharmaceuticals", ["Clean Dressings", "Basic Medicines", "Antibiotics", "Trauma Kits", "Regenerative Serum", "Nanomedical Gel", "Cellular Repair Kit", "Adaptive Therapy"], "reagents", "medicine"),
        ("construction", "Construction Components", ["Fasteners", "Prefab Panels", "Insulated Panels", "Pressure Doors", "Modular Frames", "Smart Facades", "Self-Repair Modules", "Adaptive Habitat Kits"], "alloy", "components"),
        ("vehicles", "Vehicle Fabrication", ["Axle Sets", "Tracked Chassis", "Electric Drive", "Autonomous Drive", "Armoured Chassis", "Magnetic Drive", "Adaptive Chassis", "Field-Shift Chassis"], "alloy", "vehicle_parts"),
        ("weapons", "Defence Manufacturing", ["Kinetic Barrels", "Optic Assemblies", "Stabilised Mounts", "Smart Ammunition", "Directed-Energy Parts", "Countermeasure Suite", "Autonomous Turret Kit", "Adaptive Defence Core"], "alloy", "defence_parts"),
        ("research", "Research Instruments", ["Sample Glassware", "Spectrometer", "Lab Instruments", "Sensor Array", "Precision Scanner", "Quantum Sensor", "Field Analyzer", "Anomaly Observatory"], "components", "research_parts")
    ];

    private static readonly (string Id, string Name, int Footprint, int Work, string Upkeep)[] BuildingFamilies =
    [
        ("housing", "Habitat", 4, 12, "food"),
        ("storage", "Logistics Depot", 6, 10, "energy"),
        ("workshop", "Fabrication Workshop", 4, 18, "energy"),
        ("farm", "Agricultural Complex", 6, 16, "water"),
        ("power", "Power Plant", 4, 20, "fuel"),
        ("research", "Research Laboratory", 4, 22, "energy"),
        ("medical", "Medical Centre", 4, 18, "medicine"),
        ("recycling", "Reclamation Facility", 6, 16, "energy"),
        ("mine", "Extraction Complex", 4, 20, "energy"),
        ("defense", "Defence Bastion", 4, 24, "ammunition"),
        ("transport", "Transit Hub", 6, 18, "energy"),
        ("computing", "Command Computation Core", 4, 26, "energy")
    ];

    private static readonly (string Id, string Name, int Supplies, int Alloy, int Upkeep)[] UnitRoles =
    [
        ("engineer", "Engineer", 12, 4, 1), ("medic", "Field Medic", 14, 3, 1),
        ("scout", "Scout", 16, 5, 2), ("guard", "Guardian", 20, 10, 2),
        ("ranger", "Ranger", 22, 12, 3), ("heavy", "Heavy Infantry", 28, 18, 4),
        ("drone", "Recon Drone", 18, 20, 3), ("walker", "Support Walker", 35, 32, 6),
        ("tank", "Armoured Vehicle", 48, 45, 8), ("sentinel", "Autonomous Sentinel", 55, 60, 5)
    ];

    private static readonly ResourceDef[] BaseResources =
    [
        new("wood", "Wood", "Biomass", 2, true), new("stone", "Stone", "Minerals", 2, false),
        new("food", "Food", "Life Support", 1, true), new("water", "Water", "Life Support", 1, true),
        new("ore", "Raw Ore", "Minerals", 3, false), new("alloy", "Alloy", "Metals", 6, false),
        new("components", "Components", "Manufactured", 10, false), new("energy", "Energy", "Utilities", 2, true),
        new("knowledge", "Knowledge", "Intangible", 8, false), new("reagents", "Chemical Reagents", "Chemicals", 5, false),
        new("lumber", "Processed Lumber", "Manufactured", 4, false), new("building_material", "Building Material", "Manufactured", 5, false),
        new("polymer", "Polymer", "Chemicals", 7, false), new("rations", "Rations", "Life Support", 3, true),
        new("medicine", "Medicine", "Medical", 12, false), new("power_module", "Power Module", "Manufactured", 14, false),
        new("vehicle_parts", "Vehicle Parts", "Manufactured", 16, false), new("defence_parts", "Defence Components", "Military", 18, false),
        new("research_parts", "Research Instruments", "Science", 20, false), new("rare_earth", "Rare Earth Elements", "Minerals", 12, false),
        new("crystal", "Conductive Crystal", "Minerals", 15, false), new("fuel", "Refined Fuel", "Utilities", 8, false),
        new("ammunition", "Ammunition", "Military", 6, false), new("data", "Research Data", "Intangible", 10, false),
        new("biomass", "Processed Biomass", "Biomass", 4, true), new("medicine_precursor", "Medical Precursors", "Chemicals", 9, false),
        new("quantum_core", "Quantum Core", "Advanced", 40, false), new("nanites", "Nanites", "Advanced", 35, false),
        new("exotic_matter", "Exotic Matter", "Anomalous", 60, false), new("blight_sample", "Contained Blight Sample", "Hazardous", 25, false),
        new("containment_medium", "Containment Medium", "Hazardous", 14, false), new("recycled_feedstock", "Recycled Feedstock", "Reclamation", 3, true)
    ];

    public static IReadOnlyList<ResourceDef> Resources { get; } = BaseResources;
    public static IReadOnlyList<ResearchDef> Research { get; } = BuildResearch();
    public static IReadOnlyList<BuildingDef> Buildings { get; } = BuildBuildings();
    public static IReadOnlyList<UnitDef> Units { get; } = BuildUnits();
    public static IReadOnlyList<RecipeDef> Recipes { get; } = BuildRecipes();

    public static int TotalDefinitions => Resources.Count + Research.Count + Buildings.Count + Units.Count + Recipes.Count;

    private static IReadOnlyList<ResearchDef> BuildResearch()
    {
        var result = new List<ResearchDef>();
        foreach (var discipline in Disciplines)
        for (int era = 0; era < Eras.Length; era++)
        foreach (var focus in discipline.Focuses)
        {
            string focusId = Slug(focus);
            string id = $"research.{discipline.Id}.{focusId}.era{era}";
            var prerequisites = new List<string>();
            if (era > 0)
            {
                prerequisites.Add($"research.{discipline.Id}.{focusId}.era{era - 1}");
                string core = $"research.logistics.field_operations.era{era - 1}";
                if (id != core && !prerequisites.Contains(core)) prerequisites.Add(core);
            }
            string unlockKind = discipline.UnlockKind;
            string unlockId = unlockKind switch
            {
                "building" => $"building.{BuildingFamilyFor(discipline.Id)}.tier{era}",
                "unit" => $"unit.{UnitRoles[era % UnitRoles.Length].Id}.tier{era}",
                "recipe" => $"recipe.{Industries[era % Industries.Length].Id}.stage{era}",
                _ => $"research_bonus.{discipline.Id}.{focusId}.era{era}"
            };
            int multiplier = 1 + Array.FindIndex(Disciplines, d => d.Id == discipline.Id) / 3;
            result.Add(new ResearchDef(id, $"{Eras[era]} {discipline.Name}: {focus}", discipline.Name, era,
                20 * (era + 1) * (era + 1) * multiplier, 10 * (era + 1) * multiplier,
                prerequisites.Distinct().ToArray(), unlockKind, unlockId,
                $"Advances {focus.ToLowerInvariant()} into the {Eras[era].ToLowerInvariant()} era and unlocks the next production, infrastructure or resilience layer."));
        }
        return result;
    }

    private static IReadOnlyList<BuildingDef> BuildBuildings()
    {
        var result = new List<BuildingDef>();
        foreach (var family in BuildingFamilies)
        for (int era = 0; era < Eras.Length; era++)
        {
            string id = $"building.{family.Id}.tier{era}";
            int multiplier = era + 1;
            var inputs = new Dictionary<string, int>
            {
                ["building_material"] = 4 * multiplier,
                ["alloy"] = era == 0 ? 0 : 2 * multiplier,
                ["components"] = era < 2 ? 0 : multiplier
            }.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value);
            result.Add(new BuildingDef(id, $"{Eras[era]} {family.Name}", family.Id, era,
                family.Footprint + (era >= 4 ? 2 : 0), family.Work * multiplier, inputs,
                family.Upkeep, $"research.{ResearchDisciplineFor(family.Id)}.{FocusForFamily(family.Id)}.era{era}"));
        }
        return result;
    }

    private static IReadOnlyList<UnitDef> BuildUnits()
    {
        var result = new List<UnitDef>();
        foreach (var role in UnitRoles)
        for (int era = 0; era < Eras.Length; era++)
        {
            int scale = era + 1;
            result.Add(new UnitDef($"unit.{role.Id}.tier{era}", $"{Eras[era]} {role.Name}", role.Id,
                era, role.Supplies * scale, role.Alloy * scale, role.Upkeep * scale,
                $"research.defense.fortification.era{era}"));
        }
        return result;
    }

    private static IReadOnlyList<RecipeDef> BuildRecipes()
    {
        var result = new List<RecipeDef>();
        foreach (var industry in Industries)
        for (int stage = 0; stage < industry.Stages.Length; stage++)
        {
            int scale = stage + 1;
            string recipeId = $"recipe.{industry.Id}.stage{stage}";
            string inputId = stage == 0 ? industry.Input : industry.Output;
            var inputs = new Dictionary<string, int> { [inputId] = stage == 0 ? scale * 2 : scale + 1 };
            if (stage > 0) inputs[industry.Input] = scale;
            var outputs = new Dictionary<string, int> { [industry.Output] = scale * 2 };
            result.Add(new RecipeDef(recipeId, industry.Stages[stage], industry.Name, stage,
                inputs, outputs, 4 * scale * (stage + 1),
                $"research.industry.machine_tools.era{Math.Min(stage, Eras.Length - 1)}"));
        }
        return result;
    }

    public static bool Validate(out string error)
    {
        var allIds = Resources.Select(x => x.Id)
            .Concat(Research.Select(x => x.Id))
            .Concat(Buildings.Select(x => x.Id))
            .Concat(Units.Select(x => x.Id))
            .Concat(Recipes.Select(x => x.Id)).ToArray();
        if (allIds.Length != allIds.Distinct(StringComparer.Ordinal).Count())
        {
            error = "Progression catalogue contains duplicate IDs.";
            return false;
        }

        var researchIds = Research.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var node in Research)
            if (node.Prerequisites.Any(p => !researchIds.Contains(p)))
            {
                error = $"Research node {node.Id} has an unknown prerequisite.";
                return false;
            }

        foreach (var building in Buildings)
            if (!researchIds.Contains(building.UnlockResearchId))
            {
                error = $"Building {building.Id} has an unknown research unlock.";
                return false;
            }
        foreach (var unit in Units)
            if (!researchIds.Contains(unit.UnlockResearchId))
            {
                error = $"Unit {unit.Id} has an unknown research unlock.";
                return false;
            }
        foreach (var recipe in Recipes)
            if (!researchIds.Contains(recipe.UnlockResearchId))
            {
                error = $"Recipe {recipe.Id} has an unknown research unlock.";
                return false;
            }

        error = string.Empty;
        return true;
    }

    private static string BuildingFamilyFor(string discipline) => discipline switch
    {
        "logistics" => "storage", "energy" => "power", "medicine" => "medical",
        "defense" => "defense", "exploration" => "mine", "governance" => "computing", _ => "workshop"
    };

    private static string ResearchDisciplineFor(string family) => family switch
    {
        "housing" or "medical" => "medicine", "storage" or "transport" => "logistics",
        "farm" => "agriculture", "power" => "energy", "research" or "computing" => "computing",
        "defense" => "defense", "mine" => "exploration", "recycling" => "ecology", _ => "industry"
    };

    private static string FocusForFamily(string family) => family switch
    {
        "housing" or "medical" => "trauma_care", "storage" or "transport" => "warehousing",
        "farm" => "soil_recovery", "power" => "grid_stability", "research" or "computing" => "signal_processing",
        "defense" => "fortification", "mine" => "geological_survey", "recycling" => "water_reclamation", _ => "machine_tools"
    };

    private static string Slug(string value) => value.ToLowerInvariant()
        .Replace(" ", "_", StringComparison.Ordinal)
        .Replace("-", "_", StringComparison.Ordinal)
        .Replace("/", "_", StringComparison.Ordinal);
}
