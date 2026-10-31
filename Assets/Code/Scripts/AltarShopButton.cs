using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared logic for a buyable item button in the altar window (arrows, bow upgrades).
/// Clicking an item the player doesn't own buys it with souls if they can afford it; clicking an owned item uses it.
/// Every image and text on the button is greyed out unless the subclass says the item is lit.
/// </summary>
[RequireComponent(typeof(Button))]
public abstract class AltarShopButton : MonoBehaviour
{
    [Tooltip("Multiplied into the color of every image and text on the button while it's greyed out.")]
    [SerializeField] private Color greyedOutTint = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Price")]
    [Tooltip("Optional text showing the price. It isn't greyed out with the rest of the button.")]
    [SerializeField] private TMP_Text priceText;
    [Tooltip("{0} is replaced by the soul cost.")]
    [SerializeField] private string priceFormat = "{0}";
    [Tooltip("Shown instead of the price once the item is owned. Empty hides the price text.")]
    [SerializeField] private string ownedLabel = "";
    [Tooltip("Price text color while the player can't afford the item.")]
    [SerializeField] private Color unaffordableColor = new Color(0.9f, 0.25f, 0.25f, 1f);

    private Button button;
    private Graphic[] graphics;
    private Color[] originalColors;
    private Color priceColor;

    protected BowCombat Bow { get; private set; }
    protected SoulInventory Souls { get; private set; }

    protected abstract int Cost { get; }
    protected abstract bool IsOwned { get; }
    /// <summary>Full color when true, greyed out otherwise.</summary>
    protected abstract bool IsLit { get; }
    /// <summary>Gives the item to the player after it's paid for.</summary>
    protected abstract void Unlock();
    /// <summary>Called on every click of an owned item, including right after buying it.</summary>
    protected virtual void Use() { }
    /// <summary>Subscribe to (or unsubscribe from) the events that change IsOwned or IsLit; call Refresh from them.</summary>
    protected virtual void Subscribe(bool subscribe) { }
    protected virtual bool IsValid => true;

    protected virtual void Awake()
    {
        button = GetComponent<Button>();
        graphics = GetComponentsInChildren<Graphic>(true);
        originalColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
            originalColors[i] = graphics[i].color;
        if (priceText != null) priceColor = priceText.color;

        Bow = FindAnyObjectByType<BowCombat>();
        Souls = FindAnyObjectByType<SoulInventory>();
        if (Bow == null || Souls == null)
        {
            Debug.LogError($"{GetType().Name} needs the player's BowCombat and SoulInventory.", this);
            enabled = false;
        }
    }

    protected virtual void OnEnable()
    {
        if (Bow == null || Souls == null || !IsValid) return;
        button.onClick.AddListener(OnClick);
        Souls.Changed += OnSoulsChanged;
        Subscribe(true);
        Refresh();
    }

    protected virtual void OnDisable()
    {
        if (Bow == null || Souls == null || !IsValid) return;
        button.onClick.RemoveListener(OnClick);
        Souls.Changed -= OnSoulsChanged;
        Subscribe(false);
    }

    private void OnClick()
    {
        if (!IsOwned)
        {
            if (!Souls.TrySpend(Cost)) return;
            Unlock();
        }
        Use();
        Refresh();
    }

    private void OnSoulsChanged(int souls) => Refresh();

    protected void Refresh()
    {
        bool greyedOut = !IsLit;
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == priceText) continue;
            graphics[i].color = greyedOut ? originalColors[i] * greyedOutTint : originalColors[i];
        }

        if (priceText == null) return;
        bool owned = IsOwned;
        priceText.gameObject.SetActive(!owned || !string.IsNullOrEmpty(ownedLabel));
        priceText.text = owned ? ownedLabel : string.Format(priceFormat, Cost);
        priceText.color = !owned && !Souls.CanAfford(Cost) ? unaffordableColor : priceColor;
    }
}
