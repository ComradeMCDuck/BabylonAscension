using UnityEngine;

/// <summary>
/// A bow upgrade icon in the altar window. Clicking it buys the upgrade with souls for the rest of this life.
/// Not-owned upgrades are greyed out, owned ones are lit; clicking an owned upgrade does nothing.
/// </summary>
public class UpgradePickButton : AltarShopButton
{
    [Tooltip("The upgrade given to the player when this button is clicked. It also sets the price.")]
    [SerializeField] private BowUpgrade upgrade;

    protected override int Cost => upgrade.SoulCost;
    protected override bool IsOwned => Bow.Upgrades.Has(upgrade);
    protected override bool IsLit => IsOwned;
    protected override bool IsValid => upgrade != null;

    protected override void Awake()
    {
        base.Awake();
        if (upgrade == null)
            Debug.LogError("Assign a BowUpgrade to UpgradePickButton.", this);
    }

    protected override void Unlock() => Bow.Upgrades.Add(upgrade);

    protected override void Subscribe(bool subscribe)
    {
        if (subscribe) Bow.Upgrades.Changed += Refresh;
        else Bow.Upgrades.Changed -= Refresh;
    }
}
