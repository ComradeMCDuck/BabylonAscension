using DYP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pause (Escape) and player death for a level. Scene changes, including the reload after death, go through the
/// <see cref="SceneLoader"/> on the same object, which owns the fade and the frozen time while switching.
/// </summary>
// Runs before player input so Escape blocks gameplay input in the same frame.
[DefaultExecutionOrder(-2000)]
[RequireComponent(typeof(SceneLoader))]
public class GameManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Transform playerRoot;
    [Tooltip("Where the player starts when the scene is entered without a SpawnPoint id (pressing Play, the main menu, a LevelExit with no spawn id). Empty = the player's position in the scene.")]
    [SerializeField] private Transform respawnPoint;

    [Header("Screen overlay")]
    [SerializeField] private Image blackPanel;
    [Tooltip("Shown while paused. Pause menu buttons (Resume, Main Menu...) can go inside it. They must be drawn on top of the black panel (lower in the hierarchy), or the panel blocks their clicks.")]
    [SerializeField] private GameObject pausedText;

    public bool IsPaused { get; private set; }
    /// <summary>True during any scene change, including the reload after death. Time is frozen meanwhile.</summary>
    public bool IsRespawning => SceneLoader.IsTransitioning;
    /// <summary>The black full-screen Image. SceneLoader fades with it too.</summary>
    public Image OverlayPanel => blackPanel;
    /// <summary>The time scale gameplay runs at, even while paused. SceneLoader restores it after a scene change.</summary>
    public float GameplayTimeScale => ownsTimeScale ? previousTimeScale : Time.timeScale;

    private float previousTimeScale;
    private bool ownsTimeScale;
    private Vector3 initialPosition;
    private SceneLoader sceneLoader;

    private void Awake()
    {
        sceneLoader = GetComponent<SceneLoader>();
        if (sceneLoader == null)
        {
            // RequireComponent doesn't add it to GameManagers that existed before the attribute did.
            Debug.LogWarning("GameManager had no SceneLoader; adding one. Add it to the object yourself to tune the fade.", this);
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        if (playerHealth == null || playerRoot == null || blackPanel == null || pausedText == null)
        {
            Debug.LogError("Assign the player Health, player root, black panel and pause text on GameManager.", this);
            enabled = false;
            return;
        }

        initialPosition = playerRoot.position;
        blackPanel.gameObject.SetActive(true);
        pausedText.SetActive(false);
        // While arriving from another scene, SceneLoader keeps the screen black until it fades in.
        SetOverlay(IsRespawning ? 1f : 0f);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        if (sceneLoader != null) sceneLoader.TransitionStarted += OnTransitionStarted;
    }

    private void Start()
    {
        // Runs before other scripts' Start (execution order -2000), so UI reading the player sees the carried values.
        PlayerProgress.Apply(playerRoot);
        MoveToSpawn();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetPaused(!IsPaused);
    }

    /// <summary>For a pause menu's Resume button.</summary>
    public void Resume() => SetPaused(false);

    public void SetPaused(bool paused)
    {
        if (IsRespawning || IsPaused == paused) return;
        IsPaused = paused;
        if (paused) FreezeTime();
        else RestoreTime();
        pausedText.SetActive(paused);
        SetOverlay(paused ? 0.5f : 0f);
    }

    private void OnPlayerDied(Health health)
    {
        // Reloading recreates enemies, pickups and the player from scene defaults.
        // The player comes back at the spawn point they entered the level through.
        sceneLoader.ReloadScene();
    }

    private void OnTransitionStarted()
    {
        // Carry souls and upgrades into the next scene. Health only when alive: after death it comes back full.
        PlayerProgress.Capture(playerRoot, keepHealth: !playerHealth.IsDead);

        // SceneLoader has frozen time and owns it until the next scene fades in, so drop the pause state
        // without restoring the time scale.
        IsPaused = false;
        ownsTimeScale = false;
        pausedText.SetActive(false);
    }

    private void MoveToSpawn()
    {
        SpawnPoint arrival = SpawnPoint.Find(SceneLoader.ArrivalSpawnId);
        if (arrival != null) playerRoot.position = arrival.transform.position;
        else playerRoot.position = respawnPoint != null ? respawnPoint.position : initialPosition;

        var movement = playerRoot.GetComponent<BasicMovementController2D>();
        if (movement != null) movement.ResetForRespawn();
        var body = playerRoot.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = playerRoot.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        var interpolator = playerRoot.GetComponent<MotionInterpolator>();
        if (interpolator != null) interpolator.Snap();
        Physics2D.SyncTransforms();
    }

    private void SetOverlay(float alpha)
    {
        blackPanel.color = new Color(0f, 0f, 0f, alpha);
        blackPanel.raycastTarget = alpha > 0f;
    }

    private void FreezeTime()
    {
        if (!ownsTimeScale)
        {
            previousTimeScale = Time.timeScale;
            ownsTimeScale = true;
        }
        Time.timeScale = 0f;
    }

    private void RestoreTime()
    {
        if (!ownsTimeScale) return;
        Time.timeScale = previousTimeScale;
        ownsTimeScale = false;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (sceneLoader != null) sceneLoader.TransitionStarted -= OnTransitionStarted;
        IsPaused = false;
        RestoreTime();
        // During a scene change the panel stays black until the old scene is gone.
        if (blackPanel != null && !IsRespawning) SetOverlay(0f);
        if (pausedText != null) pausedText.SetActive(false);
    }
}
