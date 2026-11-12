using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The "Press E" hint on the HUD Canvas, shared by everything the player can interact with (level doors so far).
/// Put it on the Canvas or another always-active parent of the prompt, not on the prompt itself, because an
/// inactive object stops running Update. Level objects ask for it with <see cref="Show"/> and <see cref="Hide"/>;
/// the requests are static, so they work in any Awake order and are harmless in scenes without a prompt.
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    [Tooltip("The UI object to show, e.g. a panel with a \"Press E\" text. It is hidden on Awake.")]
    [SerializeField] private GameObject prompt;
    [Tooltip("Optional text that is replaced with the requester's own text (e.g. a door's \"Press E to enter\"). Empty = the prompt always shows what you typed into it.")]
    [SerializeField] private TMP_Text label;

    private struct Request
    {
        public Object Owner;
        public string Text;
    }

    // Who wants the prompt right now. The newest request decides the text.
    private static readonly List<Request> requests = new List<Request>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => requests.Clear();

    /// <summary>Asks for the prompt to be shown until <see cref="Hide"/> is called with the same owner.</summary>
    public static void Show(Object owner, string text = null)
    {
        if (owner == null) return;
        Hide(owner);
        requests.Add(new Request { Owner = owner, Text = text });
    }

    public static void Hide(Object owner)
    {
        for (int i = requests.Count - 1; i >= 0; i--)
            if (ReferenceEquals(requests[i].Owner, owner)) requests.RemoveAt(i);
    }

    private void Awake()
    {
        if (prompt == null)
        {
            Debug.LogError("InteractPrompt has no prompt object assigned; disabling it.", this);
            enabled = false;
            return;
        }
        if (prompt == gameObject)
            Debug.LogError("InteractPrompt must sit on a parent of the prompt (e.g. the Canvas), not on the prompt itself.", this);

        prompt.SetActive(false);
    }

    private void Update()
    {
        // Owners destroyed without calling Hide (e.g. with the previous scene) compare equal to null.
        for (int i = requests.Count - 1; i >= 0; i--)
            if (requests[i].Owner == null) requests.RemoveAt(i);

        // Hidden while the game is frozen: pause menu, shop window, scene change.
        bool show = requests.Count > 0 && Time.timeScale > 0f;
        if (show && label != null)
        {
            string text = requests[requests.Count - 1].Text;
            if (!string.IsNullOrEmpty(text) && label.text != text) label.text = text;
        }
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    private void OnDisable()
    {
        if (prompt != null) prompt.SetActive(false);
    }
}
