using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD circles for the special arrow charges, one Image per charge (left to right = first to last).
/// Each assigned Image becomes the grey background knob, and a fill Image with the same sprite is created as its
/// child (children draw on top, and no extra object is added to a layout group).
/// A charged circle is full and Charged Color (white). The charge being earned back fills its circle from the
/// bottom up, by the share of basic-arrow hits counted so far, in Filling Color; it turns Charged Color once full.
/// The grey background shows while a circle is used up or filling, and is hidden once it is full.
/// Circles fill and drain smoothly instead of jumping.
/// </summary>
public class SpecialChargesUI : MonoBehaviour
{
    [Tooltip("One circle Image per charge, in order (e.g. with the Knob sprite). Each one is shown as the grey " +
             "background; the white fill is created on top of it in Awake. " +
             "Use as many as the bow's Max Special Charges; extra circles stay empty.")]
    [SerializeField] private Image[] chargeCircles;
    [Tooltip("Color of a fully charged circle.")]
    [SerializeField] private Color chargedColor = Color.white;
    [Tooltip("Color of the fill while a circle is filling up.")]
    [SerializeField] private Color fillingColor = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("Color of the knob behind a circle that is used up or filling.")]
    [SerializeField] private Color backgroundColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
    [Tooltip("How fast a circle fills or drains, in whole circles per second.")]
    [SerializeField, Min(0.01f)] private float fillSpeed = 3f;

    private BowCombat bow;
    // The fill Image created on top of each circle (null where the circle slot is empty).
    private Image[] fills;
    // Fill each circle is moving toward, set by Refresh from the bow's charges.
    private float[] targetFills;

    private void Awake()
    {
        bow = FindAnyObjectByType<BowCombat>();
        if (bow == null || chargeCircles == null || chargeCircles.Length == 0)
        {
            Debug.LogError("SpecialChargesUI needs a BowCombat on the player and at least one charge circle Image.", this);
            enabled = false;
            return;
        }

        if (chargeCircles.Length != bow.MaxSpecialCharges)
            Debug.LogWarning($"SpecialChargesUI has {chargeCircles.Length} circles but the bow holds " +
                             $"{bow.MaxSpecialCharges} charges.", this);

        targetFills = new float[chargeCircles.Length];
        fills = new Image[chargeCircles.Length];
        for (int i = 0; i < chargeCircles.Length; i++)
        {
            if (chargeCircles[i] != null) fills[i] = CreateFill(chargeCircles[i]);
        }
    }

    /// <summary>
    /// Turns the circle into the grey background and adds a child Image with the same sprite that fills from the
    /// bottom up, stretched over the whole circle.
    /// </summary>
    private Image CreateFill(Image circle)
    {
        circle.type = Image.Type.Simple;
        circle.color = backgroundColor;

        var fillObject = new GameObject("Charge Fill", typeof(RectTransform));
        var rect = (RectTransform)fillObject.transform;
        rect.SetParent(circle.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var fill = fillObject.AddComponent<Image>();
        fill.sprite = circle.sprite;
        fill.material = circle.material;
        fill.preserveAspect = circle.preserveAspect;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0f;
        return fill;
    }

    private void OnEnable()
    {
        if (bow == null) return;
        bow.SpecialArrowChanged += Refresh;
        Refresh();
        SnapFills();
    }

    private void OnDisable()
    {
        if (bow != null) bow.SpecialArrowChanged -= Refresh;
    }

    private void Refresh()
    {
        int charges = bow.SpecialCharges;
        float progress = bow.BasicHitsPerCharge > 0 ? (float)bow.BasicHitsTowardCharge / bow.BasicHitsPerCharge : 0f;

        for (int i = 0; i < targetFills.Length; i++)
        {
            if (i < charges) targetFills[i] = 1f;
            else if (i == charges) targetFills[i] = Mathf.Clamp01(progress);
            else targetFills[i] = 0f;
        }
    }

    // Unscaled, so the circles still settle while the game is paused (e.g. behind the altar window).
    private void Update()
    {
        float step = fillSpeed * Time.unscaledDeltaTime;
        for (int i = 0; i < fills.Length; i++)
        {
            if (fills[i] == null) continue;
            fills[i].fillAmount = Mathf.MoveTowards(fills[i].fillAmount, targetFills[i], step);
            UpdateLook(i);
        }
    }

    /// <summary>Jumps straight to the current charges, so a freshly loaded scene doesn't animate from empty.</summary>
    private void SnapFills()
    {
        for (int i = 0; i < fills.Length; i++)
        {
            if (fills[i] == null) continue;
            fills[i].fillAmount = targetFills[i];
            UpdateLook(i);
        }
    }

    private void UpdateLook(int index)
    {
        bool full = fills[index].fillAmount >= 1f;
        fills[index].color = full ? chargedColor : fillingColor;
        // The background knob only shows while the circle is used up or filling. Disabling the Image doesn't hide its
        // child fill.
        chargeCircles[index].enabled = !full;
    }
}
