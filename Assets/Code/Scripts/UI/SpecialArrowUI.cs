using TMPro;
using UnityEngine;

/// <summary>
/// HUD text naming the picked special arrow and its number key (1-3). The charges are shown separately by
/// <see cref="SpecialChargesUI"/>. Put it on a TMP text in the HUD Canvas.
/// </summary>
public class SpecialArrowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Shown while a special arrow is picked. {0} = Arrow Name from its ArrowData, {1} = its number key.")]
    [SerializeField] private string arrowFormat = "[{1}] {0}";
    [Tooltip("Shown while no special arrow is picked. Empty hides the text.")]
    [SerializeField] private string noArrowText = "";

    private BowCombat bow;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        bow = FindAnyObjectByType<BowCombat>();
        if (label == null || bow == null)
        {
            Debug.LogError("SpecialArrowUI needs a TMP text and a BowCombat on the player.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (bow == null) return;
        bow.ArrowChanged += OnArrowChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (bow != null) bow.ArrowChanged -= OnArrowChanged;
    }

    private void OnArrowChanged(GameObject arrow) => Refresh();

    private void Refresh()
    {
        GameObject arrow = bow.SpecialArrowPrefab;
        label.text = arrow == null ? noArrowText : string.Format(arrowFormat, GetArrowName(arrow), GetSlotKey(arrow));
    }

    /// <summary>The Arrow Name from the arrow's ArrowData, or the prefab's name if that is empty.</summary>
    private static string GetArrowName(GameObject arrow)
    {
        ArrowProjectile projectile = arrow.GetComponent<ArrowProjectile>();
        string arrowName = projectile != null && projectile.Data != null ? projectile.Data.arrowName : null;
        return string.IsNullOrWhiteSpace(arrowName) ? arrow.name : arrowName;
    }

    /// <summary>The number key that picks this arrow, or "-" if it isn't on one.</summary>
    private string GetSlotKey(GameObject arrow)
    {
        for (int i = 0; i < bow.SpecialArrowSlots.Count; i++)
        {
            if (bow.SpecialArrowSlots[i] == arrow) return (i + 1).ToString();
        }
        return "-";
    }
}
