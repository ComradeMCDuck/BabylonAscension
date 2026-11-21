using UnityEngine;

/// <summary>
/// An arrow icon in the altar window. Clicking it buys the arrow with genii (price from its ArrowData) and makes it
/// the bow's special arrow (fired with the right mouse button). After that, the player switches between bought
/// arrows with the number keys (BowCombat's Special Arrow Slots); clicking an owned arrow picks it too.
/// The button is greyed out until the arrow is bought.
/// </summary>
public class ArrowPickButton : AltarShopButton
{
    [Tooltip("The arrow prefab the bow fires after this button is clicked. Its ArrowData sets the price.")]
    [SerializeField] private GameObject arrowPrefab;

    private int cost;
    private ArrowData data;

    protected override int Cost => cost;
    protected override string TooltipTitle =>
        data != null && !string.IsNullOrWhiteSpace(data.arrowName) ? data.arrowName : ItemName;
    protected override string TooltipDescription => data != null ? data.description : "";
    protected override string ItemKind => "arrow";
    protected override string ItemName => arrowPrefab != null ? arrowPrefab.name : "";
    protected override bool IsOwned => Bow.OwnsArrow(arrowPrefab);
    protected override bool IsLit => IsOwned;
    protected override bool IsValid => arrowPrefab != null;

    protected override void Awake()
    {
        base.Awake();
        if (arrowPrefab == null)
        {
            Debug.LogError("Assign an arrow prefab to ArrowPickButton.", this);
            return;
        }

        var projectile = arrowPrefab.GetComponent<ArrowProjectile>();
        if (projectile != null && projectile.Data != null)
        {
            data = projectile.Data;
            cost = data.geniiCost;
        }
        else Debug.LogError($"{arrowPrefab.name} needs an ArrowProjectile with ArrowData for its price.", this);
    }

    protected override void Unlock() => Bow.UnlockArrow(arrowPrefab);

    protected override void Use() => Bow.SetArrow(arrowPrefab);
}
