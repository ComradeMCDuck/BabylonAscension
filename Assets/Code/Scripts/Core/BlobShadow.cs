using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fake "blob" shadow: a soft dark disc lying flat on the ground straight below a character.
/// Sprites don't use real shadow maps (flat cards in one plane shadow each other badly), so this
/// is what anchors a character to the floor instead.
///
/// Put it on the character root (the object with the body collider). Every frame it finds the ground
/// below the feet with a 2D raycast and places the disc there. The higher the character is above the
/// ground, the smaller and fainter the disc gets; with no ground in range it is hidden.
///
/// The disc is its own scene object, not a child, so it is never flipped, scaled or rotated with the
/// character and never ends up inside copies made of the character (DebugMenu's respawn snapshots).
/// </summary>
[DisallowMultipleComponent]
public class BlobShadow : MonoBehaviour
{
    [Header("Size")]
    [Tooltip("Left-right size of the shadow in world units. 0 = take it from the body collider's width.")]
    [SerializeField, Min(0f)] private float width = 0f;
    [Tooltip("Front-back size of the shadow (into the screen) in world units. 0 = half of the width. " +
             "Keep it smaller than the platform is deep, or the shadow hangs over the platform's front edge.")]
    [SerializeField, Min(0f)] private float depth = 0f;

    [Header("Look")]
    [Tooltip("How dark the shadow is while the character stands on the ground.")]
    [SerializeField, Range(0f, 1f)] private float opacity = 0.45f;
    [Tooltip("Material for the disc. Empty = the material of the character's first SpriteRenderer, " +
             "so the shadow bends with the tower like the character does.")]
    [SerializeField] private Material material;
    [Tooltip("Sorting order of the disc. Lower than the characters so it is drawn underneath them.")]
    [SerializeField] private int sortingOrder = -10;

    [Header("Height above the ground")]
    [Tooltip("Height above the ground at which the shadow has faded out completely. Also how far down the ground is searched.")]
    [SerializeField, Min(0.01f)] private float maxHeight = 6f;
    [Tooltip("Size of the shadow just before it fades out, relative to its size on the ground.")]
    [SerializeField, Range(0f, 1f)] private float minScale = 0.4f;

    [Header("Placement")]
    [Tooltip("Layers that count as ground. Empty = Environment.")]
    [SerializeField] private LayerMask groundLayers;
    [Tooltip("How far above the ground surface the disc floats, so it doesn't flicker against the floor.")]
    [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;
    [Tooltip("Moves the disc toward the camera (negative) or away from it (positive), relative to the character.")]
    [SerializeField] private float depthOffset = 0f;

    // Resolution of the generated disc texture. The disc is blurry by design, so this can stay small.
    private const int TextureSize = 64;
    // Fraction of the radius that is fully dark; from there the disc fades out to its edge.
    private const float SolidCore = 0.35f;
    // The ray starts this far above the feet, so feet resting a hair inside the ground still find it.
    private const float RayStartHeight = 0.25f;
    // Shadow width relative to the collider width when no explicit width is set.
    private const float AutoWidthFactor = 1.2f;
    // Shadow depth relative to its width when no explicit depth is set.
    private const float AutoDepthRatio = 0.5f;

    // One disc sprite shared by every shadow.
    private static Sprite discSprite;
    private static readonly List<RaycastHit2D> hitBuffer = new List<RaycastHit2D>(8);
    private static ContactFilter2D groundFilter;

    private Collider2D body;
    private MotionInterpolator interpolator;
    private Transform shadow;
    private SpriteRenderer shadowRenderer;

    private void Awake()
    {
        body = GetComponent<Collider2D>();
        if (body == null) body = GetComponentInChildren<Collider2D>();
        if (body == null)
        {
            Debug.LogError($"{nameof(BlobShadow)} on '{name}' needs a Collider2D to know where the feet are.", this);
            enabled = false;
            return;
        }

        interpolator = GetComponent<MotionInterpolator>();
        if (groundLayers.value == 0) groundLayers = LayerMask.GetMask("Environment");

        if (material == null)
        {
            SpriteRenderer characterSprite = GetComponentInChildren<SpriteRenderer>(true);
            if (characterSprite != null) material = characterSprite.sharedMaterial;
        }

        CreateShadowObject();
    }

    private void CreateShadowObject()
    {
        GameObject shadowObject = new GameObject($"BlobShadow ({name})");
        shadow = shadowObject.transform;
        // A sprite faces the camera; turned 90 degrees around X it lies flat on the floor.
        shadow.rotation = Quaternion.Euler(90f, 0f, 0f);

        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetDiscSprite();
        if (material != null) shadowRenderer.sharedMaterial = material;
        shadowRenderer.sortingOrder = sortingOrder;
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.enabled = false;
    }

    private void OnEnable()
    {
        if (shadow != null) shadow.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (shadow != null) shadow.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    // LateUpdate: after MotionInterpolator (Update) has placed the visuals for this frame.
    private void LateUpdate()
    {
        Bounds bounds = body.bounds;
        // The collider moves in physics steps; follow the smoothed visuals instead so the shadow doesn't jitter.
        Vector3 visualOffset = interpolator != null ? interpolator.VisualOffset : Vector3.zero;
        float centerX = bounds.center.x + visualOffset.x;
        float feetY = bounds.min.y + visualOffset.y;

        if (!TryFindGround(centerX, feetY, out float groundY))
        {
            shadowRenderer.enabled = false;
            return;
        }

        float height = Mathf.Max(0f, feetY - groundY);
        float fade = Mathf.Clamp01(height / maxHeight);
        float scale = Mathf.Lerp(1f, minScale, fade);

        float shadowWidth = width > 0f ? width : bounds.size.x * AutoWidthFactor;
        float shadowDepth = depth > 0f ? depth : shadowWidth * AutoDepthRatio;

        shadow.position = new Vector3(centerX, groundY + surfaceOffset, transform.position.z + depthOffset);
        // The disc is 1x1 unit; its local Y points into the screen once it lies flat.
        shadow.localScale = new Vector3(shadowWidth * scale, shadowDepth * scale, 1f);
        shadowRenderer.color = new Color(0f, 0f, 0f, opacity * (1f - fade));
        shadowRenderer.enabled = true;
    }

    /// <summary>
    /// Finds the highest ground surface straight below the feet, within maxHeight.
    /// </summary>
    private bool TryFindGround(float x, float feetY, out float groundY)
    {
        groundFilter.useTriggers = false;
        groundFilter.SetLayerMask(groundLayers);

        Vector2 origin = new Vector2(x, feetY + RayStartHeight);
        int hitCount = Physics2D.Raycast(origin, Vector2.down, groundFilter, hitBuffer, maxHeight + RayStartHeight);

        bool found = false;
        groundY = 0f;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = hitBuffer[i];
            // Never land on the character's own colliders.
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (found && hit.point.y <= groundY) continue;

            groundY = hit.point.y;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Builds the shared soft disc once: white with a radial alpha falloff, 1x1 world unit,
    /// tinted black and scaled per shadow through the SpriteRenderer.
    /// </summary>
    private static Sprite GetDiscSprite()
    {
        if (discSprite != null) return discSprite;

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "BlobShadowDisc",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[TextureSize * TextureSize];
        float center = (TextureSize - 1) * 0.5f;
        // Slightly inside the texture edge, so the outermost pixels are fully transparent.
        float radius = center - 1f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / radius;
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SolidCore, 1f, distance));
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        discSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
            TextureSize, 0, SpriteMeshType.FullRect);
        discSprite.name = "BlobShadowDisc";
        discSprite.hideFlags = HideFlags.HideAndDontSave;
        return discSprite;
    }
}
