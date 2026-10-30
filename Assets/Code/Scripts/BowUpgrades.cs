using System;
using System.Collections.Generic;

/// <summary>
/// The bow upgrades the player owns. Lives on BowCombat, so the scene reload on death clears it.
/// </summary>
public class BowUpgrades
{
    private readonly List<BowUpgrade> owned = new List<BowUpgrade>();

    /// <summary>Raised after an upgrade is added.</summary>
    public event Action Changed;

    public IReadOnlyList<BowUpgrade> Owned => owned;

    public bool Has(BowUpgrade upgrade) => owned.Contains(upgrade);

    /// <summary>Adds the upgrade. Each upgrade can only be owned once; returns false if it already is.</summary>
    public bool Add(BowUpgrade upgrade)
    {
        if (upgrade == null || owned.Contains(upgrade)) return false;
        owned.Add(upgrade);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns the base value with every owned upgrade for that stat applied:
    /// all Add amounts first, then all Multiply amounts.
    /// </summary>
    public float Apply(BowStat stat, float baseValue)
    {
        float added = baseValue;
        float multiplier = 1f;
        foreach (var upgrade in owned)
        {
            if (upgrade.Stat != stat) continue;
            if (upgrade.Mode == BowUpgradeMode.Add) added += upgrade.Amount;
            else multiplier *= upgrade.Amount;
        }
        return added * multiplier;
    }
}
