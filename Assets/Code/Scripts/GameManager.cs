using System.Collections;
using DYP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs before player input so Escape blocks gameplay input in the same frame.
[DefaultExecutionOrder(-2000)]
public class GameManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform respawnPoint;

    [Header("Screen overlay")]
    [SerializeField] private Image blackPanel;
    [SerializeField] private GameObject pausedText;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float blackScreenDuration = 0.25f;

    public bool IsPaused { get; private set; }
    public bool IsRespawning { get; private set; }
    private float previousTimeScale;
    private bool ownsTimeScale;
    private Vector3 initialPosition;
    private Coroutine respawnRoutine;
    private static string pendingRespawnScene;
    private static float respawnTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetReloadState()
    {
        pendingRespawnScene = null;
        respawnTimeScale = 1f;
    }

    private void Awake()
    {
        if (playerHealth == null || playerRoot == null || blackPanel == null || pausedText == null)
        {
            Debug.LogError("Assign the player Health, player root, black panel and pause text on GameManager.", this);
            enabled = false;
            return;
        }

        initialPosition = playerRoot.position;
        blackPanel.gameObject.SetActive(true);
        pausedText.SetActive(false);
        IsRespawning = pendingRespawnScene == gameObject.scene.path;
        if (IsRespawning)
        {
            pendingRespawnScene = null;
            previousTimeScale = respawnTimeScale;
            ownsTimeScale = true;
            Time.timeScale = 0f;
        }
        SetOverlay(IsRespawning ? 1f : 0f);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
    }

    private void Start()
    {
        MoveToSpawn();
        if (IsRespawning) respawnRoutine = StartCoroutine(FinishRespawn());
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetPaused(!IsPaused);
    }

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
        if (!IsRespawning) respawnRoutine = StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        IsRespawning = true;
        IsPaused = false;
        pausedText.SetActive(false);
        FreezeTime();
        yield return FadeTo(1f);
        if (blackScreenDuration > 0f)
            yield return new WaitForSecondsRealtime(blackScreenDuration);

        // The new scene's manager inherits the black overlay and frozen time.
        // Reloading recreates enemies, pickups and the player from scene defaults.
        pendingRespawnScene = gameObject.scene.path;
        respawnTimeScale = previousTimeScale;
        SceneManager.LoadScene(pendingRespawnScene, LoadSceneMode.Single);
        ownsTimeScale = false;
    }

    private IEnumerator FinishRespawn()
    {
        // Let the recreated objects finish Start before revealing the scene.
        yield return null;
        yield return FadeTo(0f);
        IsRespawning = false;
        respawnRoutine = null;
        RestoreTime();
    }

    private void MoveToSpawn()
    {
        playerRoot.position = respawnPoint != null ? respawnPoint.position : initialPosition;
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

    private IEnumerator FadeTo(float target)
    {
        float start = blackPanel.color.a;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlay(Mathf.Lerp(start, target, elapsed / fadeDuration));
            yield return null;
        }
        SetOverlay(target);
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
        if (respawnRoutine != null) StopCoroutine(respawnRoutine);
        respawnRoutine = null;
        IsPaused = false;
        IsRespawning = false;
        RestoreTime();
        if (blackPanel != null) SetOverlay(0f);
        if (pausedText != null) pausedText.SetActive(false);
    }
}
