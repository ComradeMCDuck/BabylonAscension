using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss health bar on the HUD. Hidden until the BossEncounter's fight starts, then shows a red bar that shrinks from
/// its right side as the boss loses health, over a darker, desaturated version of the same color. Hidden again when
/// the boss dies. It listens to BossHealth.HealthChanged instead of polling, so it only updates when the boss is hit.
/// Setup: this component on an object that stays active (e.g. the HUD Canvas or an empty child of it), with the bar
/// itself under a separate child (Bar Root): Bar Root -> Background (Image) -> Fill (Image). The fill's anchors and
/// size are set by this script, so it always covers the background from the left.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    // How much of the fill color's saturation and brightness the background keeps.
    private const float BackgroundSaturation = 0.35f;
    private const float BackgroundBrightness = 0.35f;

    [Tooltip("The boss fight this bar belongs to.")]
    [SerializeField] private BossEncounter encounter;
    [Tooltip("Child holding the whole bar. Shown when the fight starts, hidden before and after. Must not be this object, since a hidden object can't hear the fight start.")]
    [SerializeField] private GameObject barRoot;
    [Tooltip("Image behind the fill. Its color is made from the Fill Color (darker and desaturated).")]
    [SerializeField] private Image background;
    [Tooltip("Image showing the remaining health. Shrinks from its right side.")]
    [SerializeField] private Image fill;
    [Tooltip("Optional label for the boss's name.")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private string bossName = "Mushussu";

    [Header("Look")]
    [SerializeField] private Color fillColor = new Color(0.8f, 0.08f, 0.08f);
    [Tooltip("Seconds the bar takes to shrink to the new health after a hit. 0 = instant.")]
    [SerializeField, Min(0f)] private float shrinkTime = 0.1f;

    private BossHealth bossHealth;
    private float shownAmount = 1f;
    private float targetAmount = 1f;
    private float shrinkSpeed;

    private void Awake()
    {
        if (encounter == null || barRoot == null || fill == null)
        {
            Debug.LogWarning($"{name}: BossHealthBar needs an Encounter, a Bar Root and a Fill assigned. Disabling.", this);
            gameObject.SetActive(false);
            return;
        }

        if (barRoot == gameObject)
            Debug.LogError($"{name}: Bar Root must be a child, not the object with BossHealthBar, or the bar never shows up.", this);

        if (encounter.Boss != null)
            bossHealth = encounter.Boss.GetComponentInChildren<BossHealth>();
        if (bossHealth == null)
            Debug.LogError($"{name}: The encounter's boss has no BossHealth, so the bar can't show anything.", this);

        ApplyColors();
        if (nameLabel != null) nameLabel.text = bossName;

        encounter.FightStarted += Show;
        encounter.Won += Hide;
        if (bossHealth != null) bossHealth.HealthChanged += OnHealthChanged;

        SetAmount(1f);
        barRoot.SetActive(encounter.IsFighting);
    }

    private void OnDestroy()
    {
        if (encounter != null)
        {
            encounter.FightStarted -= Show;
            encounter.Won -= Hide;
        }
        if (bossHealth != null) bossHealth.HealthChanged -= OnHealthChanged;
    }

    private void Show()
    {
        targetAmount = bossHealth != null ? bossHealth.Health01 : 1f;
        SetAmount(targetAmount);
        barRoot.SetActive(true);
    }

    private void Hide()
    {
        SetAmount(0f);
        barRoot.SetActive(false);
    }

    private void OnHealthChanged(float current, float max)
    {
        targetAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (shrinkTime <= 0f) SetAmount(targetAmount);
        // Constant speed per hit, so every hit takes shrinkTime to show however big it was.
        else shrinkSpeed = Mathf.Abs(shownAmount - targetAmount) / shrinkTime;
    }

    private void Update()
    {
        if (Mathf.Approximately(shownAmount, targetAmount)) return;
        // Unscaled, so the bar still catches up if a hit lands right before the game is paused.
        SetAmount(Mathf.MoveTowards(shownAmount, targetAmount, shrinkSpeed * Time.unscaledDeltaTime));
    }

    /// <summary>Stretches the fill from the background's left edge to amount (0..1) of its width.</summary>
    private void SetAmount(float amount)
    {
        shownAmount = amount;

        RectTransform rect = fill.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(amount, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ApplyColors()
    {
        if (fill != null) fill.color = fillColor;
        if (background == null) return;

        Color.RGBToHSV(fillColor, out float hue, out float saturation, out float value);
        Color dark = Color.HSVToRGB(hue, saturation * BackgroundSaturation, value * BackgroundBrightness);
        dark.a = fillColor.a;
        background.color = dark;
    }

    // Preview the colors in the editor while tweaking Fill Color.
    private void OnValidate()
    {
        ApplyColors();
    }
}
