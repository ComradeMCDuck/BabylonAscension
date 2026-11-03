using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this component to a UI element that contains 1) an Image that will act as the health‑bar
/// and 2) a Text that displays the numeric HP.  
/// The component pulls the player’s Health component each frame and updates the UI.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [Header("References – drag in the Inspector")]

    // The player GameObject that holds the Health script
    [Tooltip("GameObject with the Health component on it")]
    public GameObject player;

    // UI elements (can be part of the same Canvas)
    [Tooltip("Image that will be filled to represent the HP – set its Source Image > Type = Filled")]
    public Image healthFillImage;

    [Tooltip("Text that displays the numeric health (optional) – use TextMeshProUGUI")]
    public TMP_Text healthText;
    [SerializeField] private Image healthSpriteImage;
    [SerializeField] private Sprite[] healthSprites;

    // Cached reference to the Health script
    private Health _playerHealth;

    void Awake()
    {
        if (player == null)
        {
            Debug.LogError("[HealthUI] No player assigned.");
            enabled = false;
            return;
        }

        _playerHealth = player.GetComponent<Health>();
        if (_playerHealth == null)
        {
            Debug.LogError("[HealthUI] Player does not contain a Health component.");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        // Get current & max health – the Health component assumes maxHealth is public.
        // If it isn’t, change the Health script to expose it publicly or use a method.
        float current = _playerHealth.CurrentHP;   // <-- add a property in Health (see below)
        float max = _playerHealth.MaxHP;           // <-- add a property in Health
        float displayCurrent = Mathf.Max(0f, current);

        // Update the fill amount (0–1)
        if (healthFillImage)
            healthFillImage.fillAmount = max > 0f ? Mathf.Clamp01(displayCurrent / max) : 0f;

        // Update the numeric text (optional)
        if (healthText)
            healthText.text = $"{(int)displayCurrent} / {(int)max}";

        // Update health sprite based on percentage
        if (healthSpriteImage && healthSprites != null && healthSprites.Length == 5)
        {
            float pct = max > 0f ? Mathf.Clamp01(displayCurrent / max) : 0f;
            int index;
            if (pct <= 0f) index = 0;
            else if (pct <= 0.25f) index = 1;
            else if (pct <= 0.5f) index = 2;
            else if (pct <= 0.75f) index = 3;
            else index = 4;
            healthSpriteImage.sprite = healthSprites[index];
        }
    }
}