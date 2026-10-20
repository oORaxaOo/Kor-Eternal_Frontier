namespace KorTheBlight.Simulation;

/// <summary>
/// A compact aggregate for residents whose individual actions are outside the active simulation area.
/// This is deliberately data-oriented: one record can represent hundreds of residents.
/// Visible/nearby residents can still use CitizenAgent for individual pathfinding and interaction.
/// </summary>
internal sealed class PopulationCohort
{
    public int Id { get; init; }
    public string RegionId { get; init; } = "home";
    public CitizenOccupation Occupation { get; init; }
    public CitizenAgeBand AgeBand { get; init; }
    public int Population { get; internal set; }
    public double Hunger { get; internal set; } = 100;
    public double Rest { get; internal set; } = 100;
    public double Social { get; internal set; } = 75;
    public double Safety { get; internal set; } = 100;
    public double Health { get; internal set; } = 100;
    public double Morale => (Hunger + Rest + Social + Safety + Health) / 5d;
}

internal enum CitizenOccupation { Unassigned, Farmer, Builder, Crafter, Hauler, Guard, Scholar, Caretaker }
internal enum CitizenAgeBand { Child, Adult, Elder }

/// <summary>
/// Low-frequency simulation for off-screen population. No per-resident object, path or engine node is
/// allocated during a tick. This lets settlement populations grow beyond thousands while reserving
/// detailed individual simulation for residents close enough to matter visually or tactically.
/// </summary>
internal sealed class PopulationCohortSimulation
{
    private readonly List<PopulationCohort> _cohorts = [];
    private int _nextId = 1;
    private double _accumulator;

    public IReadOnlyList<PopulationCohort> Cohorts => _cohorts;
    public int TotalPopulation => _cohorts.Sum(c => c.Population);
    public double ClockSeconds { get; private set; }
    public double FoodUnits { get; private set; }
    public int CohortCount => _cohorts.Count;

    public PopulationCohort AddCohort(string regionId, CitizenOccupation occupation,
        CitizenAgeBand ageBand, int population, double initialFoodUnits = 0)
    {
        if (string.IsNullOrWhiteSpace(regionId)) throw new ArgumentException("A region ID is required.", nameof(regionId));
        if (population <= 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (initialFoodUnits < 0 || !double.IsFinite(initialFoodUnits))
            throw new ArgumentOutOfRangeException(nameof(initialFoodUnits));
        if (!Enum.IsDefined(occupation) || !Enum.IsDefined(ageBand))
            throw new ArgumentOutOfRangeException(nameof(occupation));

        var cohort = _cohorts.FirstOrDefault(c =>
            c.RegionId == regionId && c.Occupation == occupation && c.AgeBand == ageBand);
        if (cohort is null)
        {
            cohort = new PopulationCohort
            {
                Id = _nextId++,
                RegionId = regionId,
                Occupation = occupation,
                AgeBand = ageBand,
                Population = population
            };
            _cohorts.Add(cohort);
        }
        else
        {
            cohort.Population = checked(cohort.Population + population);
        }
        FoodUnits += initialFoodUnits;
        return cohort;
    }

    public bool AddFood(double amount)
    {
        if (amount <= 0 || !double.IsFinite(amount)) return false;
        FoodUnits += amount;
        return true;
    }

    /// <summary>Advances hourly aggregate needs. Large delta values are handled without losing elapsed hours.</summary>
    public void Step(double deltaSeconds)
    {
        if (deltaSeconds <= 0 || !double.IsFinite(deltaSeconds)) return;
        ClockSeconds += deltaSeconds;
        _accumulator += deltaSeconds;
        while (_accumulator >= 3_600d)
        {
            SimulateHour();
            _accumulator -= 3_600d;
        }
    }

    private void SimulateHour()
    {
        foreach (var cohort in _cohorts)
        {
            if (cohort.Population <= 0) continue;
            double foodDemand = cohort.Population * (cohort.AgeBand == CitizenAgeBand.Child ? 0.025d :
                cohort.AgeBand == CitizenAgeBand.Elder ? 0.035d : 0.04d);
            double supplied = Math.Min(FoodUnits, foodDemand);
            FoodUnits -= supplied;
            double rationCoverage = foodDemand <= 0 ? 1d : supplied / foodDemand;

            cohort.Hunger = ClampNeed(cohort.Hunger + (rationCoverage - 0.72d) * 5d);
            cohort.Rest = ClampNeed(cohort.Rest - (cohort.Occupation == CitizenOccupation.Unassigned ? 1.0d : 2.2d));
            // Social need slowly declines; safe, stable settlement conditions improve it slightly.
            cohort.Social = ClampNeed(cohort.Social - 0.12d + (cohort.Safety > 60 ? 0.03d : 0d));
            cohort.Safety = ClampNeed(cohort.Safety - (cohort.Safety < 35 ? 0.1d : 0d));
            double recovery = cohort.Hunger > 45 && cohort.Safety > 35 ? 0.08d : -0.12d;
            cohort.Health = ClampNeed(cohort.Health + recovery + (rationCoverage < 0.5d ? -0.7d : 0d));
        }
    }

    private static double ClampNeed(double value) => Math.Clamp(value, 0d, 100d);
}
