using System;
using TMPro;
using UnityEngine;

/// <summary>
/// An upgrade icon in the altar window. The first click buys the upgrade with genii for the rest of the run and makes
/// it active if one of the active slots (BowCombat's Max Active Upgrades) is free. After that, clicking switches it
/// between active and inactive, so the player picks which upgrades are in use.
/// Only active upgrades are lit; bought-but-inactive and not-bought ones are greyed out (not-bought ones show the price).
/// </summary>
public class UpgradePickButton : AltarShopButton
{
    /// <summary>
    /// Raised after a click on an owned upgrade (also right after buying it): the upgrade asset name, the result
    /// ("activated", "deactivated" or "slots_full") and how many upgrades are active afterwards.
    /// Static, so analytics can listen without finding the buttons. Scene subscribers must unsubscribe in OnDisable.
    /// </summary>
    public static event Action<string, string, int> Toggled;

    public const string ResultActivated = "activated";
    public const string ResultDeactivated = "deactivated";
    public const string ResultSlotsFull = "slots_full";

    [Tooltip("The upgrade given to the player when this button is clicked. It also sets the price.")]
    [SerializeField] private BowUpgrade upgrade;

    [Header("Texts (optional, filled from the upgrade)")]
    [Tooltip("Text set to the upgrade's Display Name when the window loads. Empty = left as it is.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Text set to the upgrade's Description when the window loads. Empty = left as it is.")]
    [SerializeField] private TMP_Text descriptionText;

    protected override int Cost => upgrade.GeniiCost;
    protected override string ItemKind => "upgrade";
    protected override string ItemName => upgrade != null ? upgrade.name : "";
    protected override bool IsOwned => Bow.Upgrades.Has(upgrade);
    protected override bool IsLit => Bow.Upgrades.IsActive(upgrade);
    protected override bool IsValid => upgrade != null;
    protected override string TooltipTitle => upgrade.DisplayName;
    protected override string TooltipDescription => upgrade.Description;

    protected override void Awake()
    {
        base.Awake();
        if (upgrade == null)
        {
            Debug.LogError("Assign a BowUpgrade to UpgradePickButton.", this);
            return;
        }

        // Written from the asset, so the text only has to be kept up to date in one place.
        if (nameText != null) nameText.text = upgrade.DisplayName;
        if (descriptionText != null) descriptionText.text = upgrade.Description;
    }

    protected override void Unlock() => Bow.Upgrades.Add(upgrade);

    /// <summary>Toggles the upgrade. Activating it does nothing while all active slots are taken.</summary>
    protected override void Use()
    {
        bool activate = !Bow.Upgrades.IsActive(upgrade);
        string result = activate ? ResultActivated : ResultDeactivated;
        if (!Bow.Upgrades.SetActive(upgrade, activate))
        {
            result = ResultSlotsFull;
            Debug.Log($"All {Bow.Upgrades.MaxActive} upgrade slots are in use; deactivate one to activate {upgrade.DisplayName}.");
        }
        Toggled?.Invoke(upgrade.name, result, Bow.Upgrades.ActiveCount);
    }

    protected override void Subscribe(bool subscribe)
    {
        if (subscribe) Bow.Upgrades.Changed += Refresh;
        else Bow.Upgrades.Changed -= Refresh;
    }
}
