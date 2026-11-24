using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The description pop-up for the altar window. Put it on the tooltip panel itself (a child of the shop Canvas,
/// drawn above the buttons). Shop buttons call <see cref="Show"/> when the mouse enters them and <see cref="Hide"/>
/// when it leaves.
/// The panel follows the mouse with its top-left corner below and to the right of the cursor. On each axis where that
/// would leave the screen, it flips to the other side, so in the bottom-right corner it sits above-left of the mouse.
/// It hides itself with a CanvasGroup instead of SetActive, so it keeps running and stays findable.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ShopTooltip : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional title text, set to the item's name.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Text set to the item's description.")]
    [SerializeField] private TMP_Text descriptionText;

    [Header("Placement")]
    [Tooltip("Gap between the cursor and the panel, in screen pixels.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(16f, 16f);
    [Tooltip("Smallest gap kept between the panel and the screen edge, in screen pixels.")]
    [SerializeField, Min(0f)] private float screenMargin = 4f;

    private static ShopTooltip instance;

    private RectTransform rect;
    private RectTransform parentRect;
    private Canvas canvas;
    private CanvasGroup group;
    private Object owner;

    public bool IsShown => owner != null;

    /// <summary>The tooltip in the loaded scene. Also finds one that starts inactive, or null if there is none.</summary>
    private static ShopTooltip Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<ShopTooltip>(FindObjectsInactive.Include);
            return instance;
        }
    }

    private void Awake()
    {
        instance = this;
        rect = (RectTransform)transform;
        parentRect = transform.parent as RectTransform;
        canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null || parentRect == null)
        {
            Debug.LogError("ShopTooltip must be a child of a UI Canvas.", this);
            enabled = false;
            return;
        }
        if (descriptionText == null) Debug.LogError("Assign a Description Text to ShopTooltip.", this);

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        // The panel must never catch the mouse, or it would steal the hover from the button under it and flicker.
        group.blocksRaycasts = false;
        group.interactable = false;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnDisable()
    {
        owner = null;
        SetVisible(false);
    }

    /// <summary>Shows the pop-up for <paramref name="requester"/>. An empty description shows nothing.</summary>
    public static void Show(Object requester, string title, string description)
    {
        ShopTooltip tooltip = Instance;
        if (tooltip == null || requester == null) return;
        if (string.IsNullOrWhiteSpace(description))
        {
            Hide(requester);
            return;
        }
        if (!tooltip.isActiveAndEnabled) tooltip.gameObject.SetActive(true);
        if (!tooltip.enabled) return;
        tooltip.Open(requester, title, description);
    }

    /// <summary>Hides the pop-up if <paramref name="requester"/> is the one showing it.</summary>
    public static void Hide(Object requester)
    {
        // Don't use Instance here: hiding must not search the scene (e.g. while it unloads).
        if (instance == null || !ReferenceEquals(instance.owner, requester)) return;
        instance.owner = null;
        instance.SetVisible(false);
    }

    private void Open(Object requester, string title, string description)
    {
        owner = requester;
        if (titleText != null)
        {
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
            titleText.text = title;
        }
        if (descriptionText != null) descriptionText.text = description;

        // Draw above the buttons, and resize now (Content Size Fitter etc.) so placement uses the new text's size.
        transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        SetVisible(true);
        FollowMouse();
    }

    private void LateUpdate()
    {
        if (IsShown) FollowMouse();
    }

    private void FollowMouse()
    {
        if (Mouse.current == null) return;
        Vector2 mouse = Mouse.current.position.ReadValue();

        // Panel size in screen pixels.
        Vector2 size = rect.rect.size * canvas.scaleFactor;

        // Default: right of and below the cursor. Flip each axis that would leave the screen.
        bool fitsRight = mouse.x + cursorOffset.x + size.x <= Screen.width - screenMargin;
        bool fitsBelow = mouse.y - cursorOffset.y - size.y >= screenMargin;

        // The pivot is the panel corner placed next to the cursor (top-left when below-right, bottom-right when above-left).
        rect.pivot = new Vector2(fitsRight ? 0f : 1f, fitsBelow ? 1f : 0f);
        Vector2 screenPoint = new Vector2(
            mouse.x + (fitsRight ? cursorOffset.x : -cursorOffset.x),
            mouse.y + (fitsBelow ? -cursorOffset.y : cursorOffset.y));

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out Vector2 local))
            rect.localPosition = new Vector3(local.x, local.y, rect.localPosition.z);
    }

    private void SetVisible(bool visible)
    {
        if (group != null) group.alpha = visible ? 1f : 0f;
    }
}
