using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A bow upgrade icon in the altar window. Clicking it gives the player the upgrade for the rest of this life.
/// Not-owned upgrades are greyed out, owned ones are lit; clicking an owned upgrade does nothing.
/// </summary>
[RequireComponent(typeof(Button))]
public class UpgradePickButton : MonoBehaviour
{
    [Tooltip("The upgrade given to the player when this button is clicked.")]
    [SerializeField] private BowUpgrade upgrade;
    [Tooltip("Multiplied into the color of every image and text on the button while the upgrade isn't owned.")]
    [SerializeField] private Color greyedOutTint = new Color(0.35f, 0.35f, 0.35f, 1f);

    private Button button;
    private BowCombat bow;
    private Graphic[] graphics;
    private Color[] originalColors;

    private void Awake()
    {
        button = GetComponent<Button>();
        graphics = GetComponentsInChildren<Graphic>(true);
        originalColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
            originalColors[i] = graphics[i].color;

        bow = FindAnyObjectByType<BowCombat>();
        if (bow == null)
            Debug.LogError("UpgradePickButton can't find the player's BowCombat.", this);
        if (upgrade == null)
            Debug.LogError("Assign a BowUpgrade to UpgradePickButton.", this);
    }

    private void OnEnable()
    {
        button.onClick.AddListener(Pick);
        if (bow != null) bow.Upgrades.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(Pick);
        if (bow != null) bow.Upgrades.Changed -= Refresh;
    }

    private void Pick()
    {
        if (bow != null && upgrade != null) bow.Upgrades.Add(upgrade);
    }

    private void Refresh()
    {
        if (bow == null) return;
        bool greyedOut = !bow.Upgrades.Has(upgrade);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].color = greyedOut ? originalColors[i] * greyedOutTint : originalColors[i];
    }
}
