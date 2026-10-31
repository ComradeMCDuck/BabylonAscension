using UnityEngine;

/// <summary>
/// An arrow icon in the altar window. The first click buys the arrow with souls (price from its ArrowData);
/// after that, clicking it makes the bow fire this arrow for free.
/// Only the arrow the bow is firing is lit; all others are greyed out (all of them while on the basic arrow).
/// </summary>
public class ArrowPickButton : AltarShopButton
{
    [Tooltip("The arrow prefab the bow fires after this button is clicked. Its ArrowData sets the price.")]
    [SerializeField] private GameObject arrowPrefab;

    private int cost;

    protected override int Cost => cost;
    protected override bool IsOwned => Bow.OwnsArrow(arrowPrefab);
    protected override bool IsLit => Bow.CurrentArrowPrefab == arrowPrefab;
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
        if (projectile != null && projectile.Data != null) cost = projectile.Data.soulCost;
        else Debug.LogError($"{arrowPrefab.name} needs an ArrowProjectile with ArrowData for its price.", this);
    }

    protected override void Unlock() => Bow.UnlockArrow(arrowPrefab);

    protected override void Use() => Bow.SetArrow(arrowPrefab);

    protected override void Subscribe(bool subscribe)
    {
        if (subscribe) Bow.ArrowChanged += OnArrowChanged;
        else Bow.ArrowChanged -= OnArrowChanged;
    }

    private void OnArrowChanged(GameObject newArrow) => Refresh();
}
