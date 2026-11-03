using TMPro;
using UnityEngine;

/// <summary>
/// Shows the player's soul count in a text. Use it for the HUD counter and the count inside the altar window.
/// </summary>
public class SoulCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("{0} is replaced by the soul count.")]
    [SerializeField] private string format = "Souls: {0}";

    private SoulInventory inventory;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        inventory = FindAnyObjectByType<SoulInventory>();
        if (label == null || inventory == null)
        {
            Debug.LogError("SoulCounter needs a TMP text and a SoulInventory on the player.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (inventory == null) return;
        inventory.Changed += Show;
        Show(inventory.Souls);
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Show;
    }

    private void Show(int souls) => label.text = string.Format(format, souls);
}
