namespace KorTheBlight.Core;

public static class JobScheduler
{
    public static Job? SelectBestJob(ColonyState colony, Colonist worker)
    {
        if (colony is null || worker is null) return null;
        var candidates = colony.Jobs
            .Where(job => job.State == JobState.Pending)
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.Type)
            .ToList();

        foreach (var job in candidates)
        {
            if (job.RequiredResource is not null && !colony.Storage.HasAtLeast(job.RequiredResource, job.RequiredAmount))
                continue;
            if (!string.IsNullOrEmpty(worker.CurrentJobId))
                continue;
            if (colony.TryAssignJob(worker, job))
                return job;
        }

        return null;
    }

    public static void Tick(ColonyState colony)
    {
        if (colony is null) return;
        foreach (var colonist in colony.Colonists)
        {
            if (string.IsNullOrEmpty(colonist.CurrentJobId))
            {
                var job = SelectBestJob(colony, colonist);
                if (job is not null)
                {
                    colonist.CurrentJobId = job.Id;
                    job.State = JobState.Active;
                }
            }
        }

        foreach (var job in colony.Jobs.Where(j => j.State == JobState.Active || j.State == JobState.Reserved))
        {
            job.Progress = Math.Min(job.Progress + 1, job.TotalWork);
            if (job.Progress >= job.TotalWork)
            {
                colony.MarkCompleted(job);
            }
        }

        colony.UpdateNeeds();
    }
}

public static class ProductionManager
{
    public static bool TryAdvanceProduction(ColonyState colony, ProductionStation station, int workSteps = 1)
    {
        if (colony is null || station is null) return false;
        if (string.IsNullOrWhiteSpace(station.CurrentRecipeName)) return false;

        var recipe = colony.Stations
            .Where(s => s == station)
            .SelectMany(_ => new[] { new ProductionRecipe("default", new Dictionary<string, int>(), new Dictionary<string, int>(), 1) })
            .FirstOrDefault();

        if (recipe is null) return false;
        station.Progress = Math.Min(station.Progress + workSteps, recipe.WorkRequired);
        if (station.Progress >= recipe.WorkRequired)
        {
            station.Progress = recipe.WorkRequired;
            return true;
        }

        return false;
    }

    public static bool TryRecipe(ColonyState colony, ProductionStation station, ProductionRecipe recipe)
    {
        if (colony is null || station is null || recipe is null) return false;
        if (!colony.TryProduce(recipe, station)) return false;
        station.CurrentRecipeName = recipe.Name;
        station.Progress = recipe.WorkRequired;
        return true;
    }
}
