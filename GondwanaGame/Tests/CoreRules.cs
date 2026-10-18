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

        Console.WriteLine("Core rules checks passed: inventory, reservation, transfers, and colony job assignment.");
        return 0;
    }
}
