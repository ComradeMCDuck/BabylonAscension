using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lets the player open a shop window with E while standing in the altar's trigger, and close it with E again.
/// The game is frozen (timeScale 0) while the window is open, so enemies, projectiles and the player all stop.
/// Uses the E key directly because the "Interact" action has a Hold interaction, which would delay the press.
/// </summary>
public class ShopAltar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The window shown in the middle of the screen. Leave empty to use a generated placeholder window.")]
    [SerializeField] private GameObject window;
    [Tooltip("Optional \"Press E\" prompt. It is hidden while the window is open.")]
    [SerializeField] private GameObject prompt;

    private GameManager gameManager;
    // The player can have several colliders, so count them instead of using a bool.
    private int playerCollidersInRange;
    private float previousTimeScale = 1f;

    public bool IsOpen { get; private set; }
    private bool PlayerInRange => playerCollidersInRange > 0;

    private void Awake()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        window = GameObject.FindWithTag("Altar");
        if (window == null) Debug.LogWarning($"ShopAltar {name} has no window assigned, creating a placeholder window.");
        window.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        // The Escape pause menu and the respawn fade own the time scale; don't fight them.
        if (gameManager != null && (gameManager.IsPaused || gameManager.IsRespawning)) return;

        if (IsOpen) Close();
        else if (PlayerInRange && Time.timeScale > 0f) Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        window.SetActive(true);
        if (prompt != null) prompt.SetActive(false);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        // The window can already be destroyed when the scene unloads.
        if (window != null) window.SetActive(false);
        if (prompt != null) prompt.SetActive(PlayerInRange);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other)) playerCollidersInRange++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other)) playerCollidersInRange = Mathf.Max(0, playerCollidersInRange - 1);
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player")
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"));
    }

    private void OnDisable()
    {
        Close();
        playerCollidersInRange = 0;
    }
}
