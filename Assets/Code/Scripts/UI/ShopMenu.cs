using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The single shop window for the scene. Put it on the shop UI Canvas (or another always-active parent of the
/// window, not on the window itself, because an inactive object stops running Update).
/// The window hides itself on Awake, so it stays hidden even in scenes without any <see cref="ShopAltar"/>.
/// Altars only report when the player enters or leaves them; E opens the window while the player is at any altar,
/// and E or Escape closes it again (Escape through <see cref="EscapeCloseStack"/>). The game is frozen (timeScale 0) while the window is open.
/// Uses the E key directly because the "Interact" action has a Hold interaction, which would delay the press.
/// </summary>
public class ShopMenu : MonoBehaviour
{
    // Fallback tag used to find the window when none is assigned (older scenes tagged the window "Altar").
    private const string WindowFallbackTag = "Altar";

    [Header("References")]
    [Tooltip("The shop window shown in the middle of the screen. It is hidden on Awake.")]
    [SerializeField] private GameObject window;

    /// <summary>The shop menu of the loaded scene, or null if the scene has none.</summary>
    public static ShopMenu Instance { get; private set; }

    private GameManager gameManager;
    // Altars the player is standing in. A set, so overlapping altars and repeated reports are harmless.
    private readonly HashSet<ShopAltar> altarsInRange = new HashSet<ShopAltar>();
    private float previousTimeScale = 1f;

    public bool IsOpen { get; private set; }
    public bool PlayerAtAltar => altarsInRange.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"More than one ShopMenu in the scene; disabling the one on {name}.", this);
            enabled = false;
            return;
        }
        Instance = this;

        gameManager = FindAnyObjectByType<GameManager>();

        if (window == null) window = GameObject.FindWithTag(WindowFallbackTag);
        if (window == null)
        {
            Debug.LogError("ShopMenu has no window assigned and none was found by tag; disabling it.", this);
            enabled = false;
            return;
        }
        window.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        // The Escape pause menu and the respawn fade own the time scale; don't fight them.
        if (gameManager != null && (gameManager.IsPaused || gameManager.IsRespawning)) return;

        if (IsOpen) Close();
        else if (PlayerAtAltar && Time.timeScale > 0f) Open();
    }

    public void Open()
    {
        if (IsOpen || window == null) return;
        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        window.SetActive(true);
        // Escape now closes the shop instead of opening the pause menu (GameManager reads the key).
        EscapeCloseStack.Push(this, Close);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        EscapeCloseStack.Remove(this);
        Time.timeScale = previousTimeScale;
        // The window can already be destroyed when the scene unloads.
        if (window != null) window.SetActive(false);
    }

    /// <summary>Called by an altar when the player steps into it.</summary>
    public void PlayerEntered(ShopAltar altar)
    {
        if (altar != null) altarsInRange.Add(altar);
    }

    /// <summary>Called by an altar when the player leaves it, or when the altar is disabled or destroyed.</summary>
    public void PlayerExited(ShopAltar altar)
    {
        altarsInRange.Remove(altar);
    }

    private void OnDisable()
    {
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
