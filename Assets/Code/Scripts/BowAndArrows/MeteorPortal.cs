using UnityEngine;

/// <summary>
/// The portal a meteor shower arrow opens where it stops (see ArrowData's Meteor Shower section). Over Meteor
/// Duration it drops Meteor Count copies of Meteor Prefab from random points along its width, falling roughly
/// straight down, then fades out. The meteors are launched as the player's arrows at full charge with their own
/// ArrowData, so they hit enemies, not the player.
/// Made at runtime by <see cref="Open"/>; it isn't placed in scenes.
/// </summary>
public class MeteorPortal : MonoBehaviour
{
    // Seconds the portal takes to grow in and to fade out.
    private const float FadeTime = 0.25f;
    // Size of the placeholder oval texture, in pixels. Its width is one unit, so 64x16 makes an oval 4 times as wide as tall.
    private const int PlaceholderTextureWidth = 64;
    private const int PlaceholderTextureHeight = 16;

    private static Sprite s_PlaceholderSprite;

    private ArrowData data;
    private GameObject owner;
    private SpriteRenderer placeholder;
    private Color placeholderColor;
    private Vector3 fullScale;
    private float elapsed;
    private int dropped;

    /// <summary>
    /// Opens a portal at position for the given meteor shower ArrowData. owner is the player, so meteors ignore them.
    /// arrowSprite (optional) lends its material and sorting to the placeholder, so it bends with the tower.
    /// </summary>
    public static MeteorPortal Open(ArrowData data, Vector3 position, GameObject owner, SpriteRenderer arrowSprite)
    {
        if (data == null || !data.IsMeteorShower)
        {
            Debug.LogError("MeteorPortal needs an ArrowData with Meteor Count above 0 and a Meteor Prefab.");
            return null;
        }

        var portalObject = new GameObject("Meteor Portal");
        portalObject.transform.position = position + Vector3.up * data.portalHeightOffset;

        var portal = portalObject.AddComponent<MeteorPortal>();
        portal.data = data;
        portal.owner = owner;
        portal.SetUpVisual(arrowSprite);
        return portal;
    }

    private void SetUpVisual(SpriteRenderer arrowSprite)
    {
        if (data.portalEffect != null)
        {
            Instantiate(data.portalEffect, transform.position, Quaternion.identity, transform);
            fullScale = Vector3.one;
            return;
        }

        placeholder = gameObject.AddComponent<SpriteRenderer>();
        placeholder.sprite = GetPlaceholderSprite();
        placeholderColor = data.portalColor;
        placeholder.color = placeholderColor;

        if (arrowSprite != null)
        {
            placeholder.sharedMaterial = arrowSprite.sharedMaterial;
            placeholder.sortingLayerID = arrowSprite.sortingLayerID;
            placeholder.sortingOrder = arrowSprite.sortingOrder;
        }

        // The sprite is one unit wide (and a quarter unit tall), so scaling it evenly keeps the oval's shape.
        float width = Mathf.Max(0.1f, data.portalWidth);
        fullScale = new Vector3(width, width, 1f);
        transform.localScale = Vector3.zero;
    }

    private void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;

        // Meteors are spread evenly over the duration; the first drops as soon as the portal opens.
        int count = data.meteorCount;
        float interval = count > 1 ? data.meteorDuration / (count - 1) : 0f;
        while (dropped < count && elapsed >= dropped * interval)
        {
            DropMeteor();
            dropped++;
        }

        if (dropped >= count && elapsed >= data.meteorDuration + FadeTime)
            Destroy(gameObject);
    }

    private void Update()
    {
        // Grow in, stay, then fade out after the last meteor.
        float grow = Mathf.Clamp01(elapsed / FadeTime);
        float fade = 1f - Mathf.Clamp01((elapsed - data.meteorDuration) / FadeTime);
        transform.localScale = fullScale * grow;

        if (placeholder != null)
        {
            Color color = placeholderColor;
            color.a *= fade;
            placeholder.color = color;
        }
    }

    private void DropMeteor()
    {
        float halfWidth = data.portalWidth * 0.5f;
        Vector3 spawn = transform.position + Vector3.right * Random.Range(-halfWidth, halfWidth);
        Vector2 down = Quaternion.Euler(0f, 0f, Random.Range(-data.meteorAngleSpread, data.meteorAngleSpread)) * Vector2.down;

        GameObject meteorObject = Instantiate(data.meteorPrefab, spawn, Quaternion.identity);
        if (!meteorObject.TryGetComponent(out ArrowProjectile meteor))
        {
            Debug.LogError($"{data.name}: Meteor Prefab needs an ArrowProjectile.", meteorObject);
            Destroy(meteorObject);
            return;
        }

        meteor.Launch(down, 1f, owner);
    }

    // A soft white oval, one unit wide, shared by all placeholder portals.
    private static Sprite GetPlaceholderSprite()
    {
        if (s_PlaceholderSprite != null) return s_PlaceholderSprite;

        var texture = new Texture2D(PlaceholderTextureWidth, PlaceholderTextureHeight, TextureFormat.RGBA32, false)
        {
            name = "Meteor Portal Placeholder",
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < PlaceholderTextureHeight; y++)
        {
            for (int x = 0; x < PlaceholderTextureWidth; x++)
            {
                float u = (x + 0.5f) / PlaceholderTextureWidth * 2f - 1f;
                float v = (y + 0.5f) / PlaceholderTextureHeight * 2f - 1f;
                float alpha = Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 3f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        s_PlaceholderSprite = Sprite.Create(texture,
            new Rect(0f, 0f, PlaceholderTextureWidth, PlaceholderTextureHeight),
            new Vector2(0.5f, 0.5f), PlaceholderTextureWidth);
        return s_PlaceholderSprite;
    }
}
