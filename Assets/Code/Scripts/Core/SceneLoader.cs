using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The one place that changes scenes: level exits, death respawns and menu buttons all go through here, so every
/// switch looks the same (freeze time, fade to black, load, fade back in). It lives on the GameManager prefab in
/// levels; menu scenes can use it on its own. The new scene's loader picks up the black screen and frozen time
/// from static state, because this object is destroyed with the old scene.
/// UI buttons: wire OnClick to <see cref="LoadScene"/>, <see cref="ReloadScene"/>, <see cref="LoadNextScene"/>,
/// <see cref="LoadMainMenu"/> or <see cref="QuitGame"/>.
/// </summary>
// Before GameManager (-2000), so the arriving black screen and frozen time are set before anything else wakes.
[DefaultExecutionOrder(-2100)]
public class SceneLoader : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Scene name loaded by LoadMainMenu (e.g. a pause menu's \"Main Menu\" button). Must be in the Build Profiles scene list.")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Fade")]
    [Tooltip("Full-screen black Image used for the fade. Empty = the GameManager's black panel on this object. No panel at all = scenes switch without a fade.")]
    [SerializeField] private Image fadePanel;
    [Tooltip("Seconds to fade out before loading and to fade in after loading. Uses real time, so it works while paused.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    [Tooltip("Seconds the screen stays fully black before the next scene loads.")]
    [SerializeField, Min(0f)] private float blackScreenDuration = 0.25f;

    /// <summary>The loader of the current scene, or null if the scene has none.</summary>
    public static SceneLoader Instance { get; private set; }
    /// <summary>True from the start of a fade-out until the next scene has faded back in. Time is frozen meanwhile.</summary>
    public static bool IsTransitioning { get; private set; }
    /// <summary>
    /// The <see cref="SpawnPoint"/> id the player should appear at in the current scene, or null for the scene's
    /// default spawn. It is kept when the scene reloads, so dying respawns the player where they entered the level.
    /// </summary>
    public static string ArrivalSpawnId { get; private set; }

    /// <summary>Raised when a scene change starts, before the fade-out. GameManager uses it to close the pause menu.</summary>
    public event Action TransitionStarted;

    // Time scale to restore after the next scene fades in (e.g. a debug slow-motion value).
    private static float restoreTimeScale = 1f;

    private GameManager gameManager;
    private Coroutine routine;
    // Set once SceneManager.LoadScene is called. From then on this object is about to be destroyed and must
    // leave the static transition state alone for the next scene's loader.
    private bool loadIssued;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        IsTransitioning = false;
        ArrivalSpawnId = null;
        restoreTimeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Safety net: if a scene without a SceneLoader is loaded mid-transition, nothing would ever unfreeze time.
    /// sceneLoaded runs after the new scene's Awake, so its loader (if any) has already registered.
    /// </summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsTransitioning || mode != LoadSceneMode.Single) return;
        if (Instance != null && Instance.gameObject.scene == scene) return;

        Debug.LogWarning($"Scene '{scene.name}' has no SceneLoader, so it was shown without a fade-in. Add the GameManager prefab or a SceneLoader to it.");
        IsTransitioning = false;
        Time.timeScale = restoreTimeScale;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
        {
            Debug.LogError($"More than one SceneLoader in the scene; disabling the one on {name}.", this);
            enabled = false;
            return;
        }
        Instance = this;

        gameManager = GetComponent<GameManager>();
        if (fadePanel == null && gameManager != null) fadePanel = gameManager.OverlayPanel;
        if (fadePanel != null) fadePanel.gameObject.SetActive(true);

        if (IsTransitioning)
        {
            // Arriving from another scene: stay black and frozen until Start has placed everything.
            Time.timeScale = 0f;
            SetFade(1f);
        }
    }

    private void Start()
    {
        if (IsTransitioning) routine = StartCoroutine(FinishArrival());
    }

    // ---- Public API (UI buttons call the void methods) ----

    /// <summary>Loads a scene by name and puts the player at its default spawn. For UI buttons.</summary>
    public void LoadScene(string sceneName) => TryLoadScene(sceneName, null);

    /// <summary>Reloads the current scene. The player appears where they entered the level.</summary>
    public void ReloadScene() => TryLoadScene(gameObject.scene.path, ArrivalSpawnId);

    /// <summary>Loads the scene after this one in the Build Profiles scene list.</summary>
    public void LoadNextScene()
    {
        int next = gameObject.scene.buildIndex + 1;
        if (gameObject.scene.buildIndex < 0 || next >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"'{gameObject.scene.name}' has no next scene in the Build Profiles scene list.", this);
            return;
        }
        TryLoadScene(SceneUtility.GetScenePathByBuildIndex(next), null);
    }

    /// <summary>Goes to the main menu and forgets the run's progress, so starting again begins fresh.</summary>
    public void LoadMainMenu()
    {
        // Cleared after TryLoadScene, because its TransitionStarted makes GameManager capture the progress.
        if (TryLoadScene(mainMenuScene, null)) PlayerProgress.Clear();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Fades out and loads <paramref name="scene"/> (a name or an asset path from the Build Profiles list).
    /// <paramref name="spawnId"/> names the <see cref="SpawnPoint"/> to arrive at; null = the default spawn.
    /// Returns false, and does nothing, if a transition is already running or the scene can't be loaded.
    /// </summary>
    public bool TryLoadScene(string scene, string spawnId)
    {
        if (!isActiveAndEnabled || IsTransitioning) return false;
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Can't load scene '{scene}'. Check the name and that it is in File > Build Profiles > Scene List.", this);
            return false;
        }

        // Read before TransitionStarted, which makes GameManager drop its paused state.
        restoreTimeScale = gameManager != null ? gameManager.GameplayTimeScale : Time.timeScale;
        if (restoreTimeScale <= 0f) restoreTimeScale = 1f;

        IsTransitioning = true;
        Time.timeScale = 0f;
        TransitionStarted?.Invoke();
        routine = StartCoroutine(FadeOutAndLoad(scene, spawnId));
        return true;
    }

    // ---- Transition ----

    private IEnumerator FadeOutAndLoad(string scene, string spawnId)
    {
        yield return FadeTo(1f);
        if (blackScreenDuration > 0f)
            yield return new WaitForSecondsRealtime(blackScreenDuration);

        ArrivalSpawnId = string.IsNullOrEmpty(spawnId) ? null : spawnId;
        loadIssued = true;
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }

    private IEnumerator FinishArrival()
    {
        // Let the new scene's objects finish Start (GameManager moves the player to the spawn) before revealing it.
        yield return null;
        yield return FadeTo(0f);
        routine = null;
        IsTransitioning = false;
        Time.timeScale = restoreTimeScale;
    }

    private IEnumerator FadeTo(float target)
    {
        if (fadePanel == null) yield break;
        float start = fadePanel.color.a;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFade(Mathf.Lerp(start, target, elapsed / fadeDuration));
            yield return null;
        }
        SetFade(target);
    }

    private void SetFade(float alpha)
    {
        if (fadePanel == null) return;
        fadePanel.color = new Color(0f, 0f, 0f, alpha);
        // Block clicks while black so menu buttons can't start a second load.
        fadePanel.raycastTarget = alpha > 0f;
    }

    private void OnDisable()
    {
        // Interrupted before the load was issued (object disabled mid-fade): don't leave the game frozen.
        if (routine != null && !loadIssued)
        {
            StopCoroutine(routine);
            IsTransitioning = false;
            Time.timeScale = restoreTimeScale;
            SetFade(0f);
        }
        routine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
