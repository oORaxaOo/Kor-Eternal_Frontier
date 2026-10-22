using KorTheBlight.Simulation.Combat;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation;

/// <summary>
/// Adapts workbench item IDs to the settlement's one canonical inventory ledger.
/// Bulk resources remain in typed world resource stacks; manufactured parts live in item stacks.
/// All mutation occurs on the simulation thread, so compound debits are validated before commit.
/// </summary>
internal sealed class WorldWorkbenchInventory(ContinuousWorldState world, long settlementId) : IWorkbenchInventory
{
    private readonly ContinuousWorldState _world = world ?? throw new ArgumentNullException(nameof(world));
    private readonly long _settlementId = settlementId;

    public int GetAmount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        return itemId switch
        {
            WorkbenchManager.Alloy => (int)Math.Floor(_world.InventoryAt(_settlementId, WorldResourceKind.Alloy) + 1e-9),
            WorkbenchManager.Components => (int)Math.Floor(_world.InventoryAt(_settlementId, WorldResourceKind.Components) + 1e-9),
            _ => _world.ItemAmountAt(_settlementId, itemId)
        };
    }

    public bool TryAdd(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount < 0) return false;
        return itemId switch
        {
            WorkbenchManager.Alloy => _world.TryAddInventory(_settlementId, WorldResourceKind.Alloy, amount),
            WorkbenchManager.Components => _world.TryAddInventory(_settlementId, WorldResourceKind.Components, amount),
            _ => _world.TryAddItem(_settlementId, itemId, amount)
        };
    }

    public bool TryTake(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount < 0 || GetAmount(itemId) < amount) return false;
        return itemId switch
        {
            WorkbenchManager.Alloy => _world.TryTakeInventory(_settlementId, WorldResourceKind.Alloy, amount),
            WorkbenchManager.Components => _world.TryTakeInventory(_settlementId, WorldResourceKind.Components, amount),
            _ => _world.TryTakeItem(_settlementId, itemId, amount)
        };
    }

    public bool TryTakeMany(IReadOnlyDictionary<string, int> costs)
    {
        ArgumentNullException.ThrowIfNull(costs);
        if (costs.Any(cost => string.IsNullOrWhiteSpace(cost.Key) || cost.Value < 0 ||
                              GetAmount(cost.Key) < cost.Value)) return false;
        var taken = new List<(string ItemId, int Amount)>();
        foreach (var cost in costs.OrderBy(cost => cost.Key, StringComparer.Ordinal))
        {
            if (cost.Value == 0) continue;
            if (TryTake(cost.Key, cost.Value))
            {
                taken.Add((cost.Key, cost.Value));
                continue;
            }

            // Defensive rollback: with the single-writer simulation this should not occur after preflight,
            // but it prevents a partial debit if an invariant is violated by a future adapter.
            foreach (var prior in taken) TryAdd(prior.ItemId, prior.Amount);
            return false;
        }
        return true;
    }

    public IReadOnlyDictionary<string, int> Snapshot()
    {
        var result = new Dictionary<string, int>(_world.ItemInventorySnapshot(_settlementId), StringComparer.Ordinal)
        {
            [WorkbenchManager.Alloy] = GetAmount(WorkbenchManager.Alloy),
            [WorkbenchManager.Components] = GetAmount(WorkbenchManager.Components)
        };
        return result;
    }
}
