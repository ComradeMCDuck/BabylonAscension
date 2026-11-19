using System;
using System.Collections.Generic;

/// <summary>
/// The upgrades the player owns, and which of them are active. Bought upgrades stay owned for the run, but only the
/// active ones (at most <see cref="MaxActive"/>) take effect, so the player has to choose; they swap them at an altar.
/// Lives on BowCombat, which every scene load recreates; <see cref="PlayerProgress"/> carries the upgrades into the
/// next scene and through death.
/// </summary>
public class BowUpgrades
{
    private readonly List<BowUpgrade> owned = new List<BowUpgrade>();
    private readonly List<BowUpgrade> active = new List<BowUpgrade>();
    private int maxActive = 3;

    /// <summary>Raised after an upgrade is bought, activated or deactivated.</summary>
    public event Action Changed;

    public IReadOnlyList<BowUpgrade> Owned => owned;
    public IReadOnlyList<BowUpgrade> Active => active;
    public int ActiveCount => active.Count;

    /// <summary>How many upgrades can be active at once. Lowering it deactivates the last activated ones.</summary>
    public int MaxActive
    {
        get => maxActive;
        set
        {
            maxActive = Math.Max(0, value);
            if (active.Count <= maxActive) return;
            active.RemoveRange(maxActive, active.Count - maxActive);
            Changed?.Invoke();
        }
    }

    public bool HasFreeSlot => active.Count < maxActive;

    public bool Has(BowUpgrade upgrade) => owned.Contains(upgrade);
    public bool IsActive(BowUpgrade upgrade) => active.Contains(upgrade);

    /// <summary>Buys the upgrade (not active yet). Each upgrade can only be owned once; returns false if it already is.</summary>
    public bool Add(BowUpgrade upgrade)
    {
        if (upgrade == null || owned.Contains(upgrade)) return false;
        owned.Add(upgrade);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Activates or deactivates an owned upgrade. Activating needs a free slot. Returns true if the upgrade ends up
    /// in the requested state.
    /// </summary>
    public bool SetActive(BowUpgrade upgrade, bool makeActive)
    {
        if (upgrade == null || !owned.Contains(upgrade)) return false;
        if (makeActive == active.Contains(upgrade)) return true;

        if (makeActive)
        {
            if (!HasFreeSlot) return false;
            active.Add(upgrade);
        }
        else
        {
            active.Remove(upgrade);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns the base value with every active upgrade for that stat applied:
    /// all Add amounts first, then all Multiply amounts. Owned but inactive upgrades don't count.
    /// </summary>
    public float Apply(BowStat stat, float baseValue)
    {
        float added = baseValue;
        float multiplier = 1f;
        foreach (var upgrade in active)
        {
            if (upgrade.Stat != stat) continue;
            if (upgrade.Mode == BowUpgradeMode.Add) added += upgrade.Amount;
            else multiplier *= upgrade.Amount;
        }
        return added * multiplier;
    }
}
