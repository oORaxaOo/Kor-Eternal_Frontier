using KorTheBlight.Core;

namespace KorTheBlight.Tests;

internal static class CoreRules
{
    public static int Run()
    {
        var inventory = new ResourceInventory();
        inventory.Add("alloy", 24);
        inventory.Reserve("alloy", 8);
        inventory.Release("alloy", 4);
        if (inventory.Get("alloy") != 24 || inventory.Reserved("alloy") != 4)
            throw new InvalidOperationException("ResourceInventory reserve/release should preserve exact stock and reservation state.");

        var target = new ResourceInventory();
        if (!inventory.TryTransferTo(target, "alloy", 8))
            throw new InvalidOperationException("ResourceInventory must transfer stock without creating phantom resources.");
        if (inventory.Get("alloy") != 16 || target.Get("alloy") != 8)
            throw new InvalidOperationException("Transfer should move stock exactly from source to destination.");

        var colony = new ColonyState();
        var worker = colony.AddColonist("Tester", 2, 3);
        var job = colony.EnqueueJob(JobType.Gather, "Gather alloy", 10, "alloy", 6);
        if (!colony.TryAssignJob(worker, job))
            throw new InvalidOperationException("A valid job should assign to a free worker.");
        if (job.State != JobState.Reserved || worker.CurrentJobId != job.Id)
            throw new InvalidOperationException("Assigned jobs must track the worker and reservation state.");

        colony.MarkCompleted(job);
        if (job.State != JobState.Complete || worker.CurrentJobId is not null)
            throw new InvalidOperationException("Completed jobs must clear the active assignment from the worker.");

        var station = colony.AddStation("Smelter");
        var recipe = new ProductionRecipe(
            "Alloy bar",
            new Dictionary<string, int> { ["ore"] = 2 },
            new Dictionary<string, int> { ["alloy"] = 1 },
            workRequired: 2);

        colony.Storage.Add("ore", 8);
        if (!colony.TryProduce(recipe, station))
            throw new InvalidOperationException("A valid recipe must consume inputs and create outputs in the station inventory.");
        if (station.Inventory["alloy"] != 1 || colony.Storage.Get("ore") != 6)
            throw new InvalidOperationException("Production should consume inputs and emit deterministic outputs.");

        var needs = colony.AddColonist("NeedsTester", 1, 1);
        colony.UpdateNeeds();
        if (needs.Needs[NeedType.Food] != 1 || needs.Needs[NeedType.Work] != 1)
            throw new InvalidOperationException("Population needs should update deterministically on each simulation tick.");

        var schedulerWorker = colony.AddColonist("Scheduler", 5, 5);
        var schedulerJob = colony.EnqueueJob(JobType.Build, "Build storage", 7, "alloy", 1);
        if (JobScheduler.SelectBestJob(colony, schedulerWorker) is null)
            throw new InvalidOperationException("The scheduler must pick a valid pending job for an eligible worker.");
        if (schedulerJob.State != JobState.Reserved || schedulerWorker.CurrentJobId != schedulerJob.Id)
            throw new InvalidOperationException("The scheduler should assign the selected job and mark the worker as occupied.");

        Console.WriteLine("Core rules checks passed: inventory, reservations, transfers, jobs, production, scheduler, and needs model.");
        return 0;
    }
}
