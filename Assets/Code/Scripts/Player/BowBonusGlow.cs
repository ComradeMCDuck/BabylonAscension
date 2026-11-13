using UnityEngine;

/// <summary>
/// Placeholder visual for BowCombat's full-draw bonus: a soft glow on the bow that pops in when
/// the bonus starts and pulses while it lasts. It needs no art: the glow sprite is generated in code.
/// BowCombat creates it under BowPivot when no Bonus Effect is assigned, and turns it on and off
/// (OnEnable restarts the pop). Replace it by assigning a real effect to BowCombat's Bonus Effect.
/// </summary>
public class BowBonusGlow : MonoBehaviour
{
    // Size of the generated glow texture, in pixels.
    private const int TextureSize = 64;
    // Sorting order added on top of the character's sprites, so the glow draws in front of the bow.
    private const int SortingOrderOffset = 50;

    [Tooltip("Color of the glow. Alpha is the brightest the pulse gets.")]
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.3f, 0.75f);
    [Tooltip("Diameter of the glow in world units.")]
    [SerializeField, Min(0f)] private float size = 1.2f;
    [Tooltip("Pulses per second.")]
    [SerializeField, Min(0f)] private float pulseSpeed = 4f;
    [Tooltip("How much the size and brightness swing during the pulse (0 = steady).")]
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.3f;
    [Tooltip("How much bigger the glow starts when it pops in.")]
    [SerializeField, Min(1f)] private float popScale = 1.8f;
    [Tooltip("Seconds the pop takes to shrink back to normal size.")]
    [SerializeField, Min(0.01f)] private float popDuration = 0.15f;

    // Shared by every glow, created once.
    private static Sprite s_GlowSprite;

    private SpriteRenderer spriteRenderer;
    private float activeTime;

    /// <summary>Creates the glow under the given parent, drawn above the character's sprites. Starts hidden.</summary>
    public static BowBonusGlow Create(Transform parent, SpriteRenderer sortingReference)
    {
        GameObject glowObject = new GameObject("BowBonusGlow (placeholder)");
        glowObject.transform.SetParent(parent, false);

        SpriteRenderer renderer = glowObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetGlowSprite();
        if (sortingReference != null)
        {
            renderer.sortingLayerID = sortingReference.sortingLayerID;
            renderer.sortingOrder = sortingReference.sortingOrder + SortingOrderOffset;
        }

        BowBonusGlow glow = glowObject.AddComponent<BowBonusGlow>();
        glowObject.SetActive(false);
        return glow;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        activeTime = 0f;
        Animate();
    }

    private void Update()
    {
        activeTime += Time.deltaTime;
        Animate();
    }

    private void Animate()
    {
        if (spriteRenderer == null) return;

        float pulse = 1f - pulseAmount * 0.5f * (1f + Mathf.Sin(activeTime * pulseSpeed * 2f * Mathf.PI));
        float pop = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(activeTime / popDuration));

        // The sprite is 1 world unit wide at scale 1.
        transform.localScale = Vector3.one * (size * pulse * pop);

        Color pulsed = color;
        pulsed.a *= pulse;
        spriteRenderer.color = pulsed;
    }

    /// <summary>A white circle that fades out from the center, 1 world unit across.</summary>
    private static Sprite GetGlowSprite()
    {
        if (s_GlowSprite != null) return s_GlowSprite;

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float center = (TextureSize - 1) * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }
        texture.Apply();

        s_GlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        return s_GlowSprite;
    }
}
