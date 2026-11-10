using UnityEngine;

/// <summary>
/// A shop altar in the level. It only tracks whether the player stands in its trigger and reports that to the
/// scene's <see cref="ShopMenu"/>, which owns the window, the E key and the time scale. This way the shop UI
/// works the same with any number of altars, including none.
/// </summary>
public class ShopAltar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional \"Press E\" prompt. Shown while the player is in range and the shop window is closed.")]
    [SerializeField] private GameObject prompt;

    // The player can have several colliders, so count them instead of using a bool.
    private int playerCollidersInRange;

    public bool PlayerInRange => playerCollidersInRange > 0;

    private void Awake()
    {
        if (prompt != null) prompt.SetActive(false);
    }

    private void Update()
    {
        if (prompt == null) return;
        ShopMenu menu = ShopMenu.Instance;
        bool show = PlayerInRange && menu != null && !menu.IsOpen;
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerCollidersInRange++;
        if (playerCollidersInRange == 1 && ShopMenu.Instance != null) ShopMenu.Instance.PlayerEntered(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other) || playerCollidersInRange == 0) return;
        playerCollidersInRange--;
        if (playerCollidersInRange == 0 && ShopMenu.Instance != null) ShopMenu.Instance.PlayerExited(this);
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player")
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"));
    }

    private void OnDisable()
    {
        // Removing the altar while the player stands in it must not leave the shop openable.
        // The explicit != null check (not ?.) also handles a ShopMenu already destroyed on scene unload.
        if (ShopMenu.Instance != null) ShopMenu.Instance.PlayerExited(this);
        playerCollidersInRange = 0;
        if (prompt != null) prompt.SetActive(false);
    }
}
