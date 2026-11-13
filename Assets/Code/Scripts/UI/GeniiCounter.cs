using TMPro;
using UnityEngine;

/// <summary>
/// Shows the player's genii count in a text. Use it for the HUD counter and the count inside the altar window.
/// </summary>
public class GeniiCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("{0} is replaced by the genii count.")]
    [SerializeField] private string format = "Genii: {0}";

    private GeniiInventory inventory;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        inventory = FindAnyObjectByType<GeniiInventory>();
        if (label == null || inventory == null)
        {
            Debug.LogError("GeniiCounter needs a TMP text and a GeniiInventory on the player.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (inventory == null) return;
        inventory.Changed += Show;
        Show(inventory.Count);
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Show;
    }

    private void Show(int count) => label.text = string.Format(format, count);
}
