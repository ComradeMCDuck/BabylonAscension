using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The laser arrow: instead of flying, the arrow becomes a rectangle (a trigger BoxCollider2D) that starts where the
/// shot left the bow and points along the aim. Its length, width and duration come from ArrowData's Laser section.
/// It goes through walls and armor and hits every enemy inside it once, also ones that walk in while it lasts.
/// ArrowProjectile.Launch hands bow shots over to this when the ArrowData is a laser; it adds this component itself
/// if the prefab doesn't have it.
/// Like DamageHitbox, hits are found with overlap checks every physics step, because enemies use kinematic
/// Rigidbody2Ds, which don't send trigger events.
/// </summary>
public class LaserBeam : MonoBehaviour
{
    [Tooltip("Optional sprite stretched over the rectangle. Put it on a child of the arrow. " +
             "Empty = a plain rectangle in ArrowData's Laser Color is drawn.")]
    [SerializeField] private SpriteRenderer beamSprite;

    private static readonly List<Collider2D> s_Overlaps = new List<Collider2D>();
    private static Sprite s_PlaceholderSprite;

    private ArrowProjectile arrow;
    private ArrowData data;
    private BoxCollider2D box;
    private ContactFilter2D filter;
    private GameObject owner;
    private Vector2 direction;
    private float damage;
    private float knockback;
    private float damageMultiplier;
    private float startTime;
    private Color beamColor;
    private bool firing;

    // Characters and ArrowTargets already hit, so each is hit once even with several colliders inside.
    private readonly HashSet<Transform> hitCharacters = new HashSet<Transform>();
    private readonly HashSet<ArrowTarget> hitArrowTargets = new HashSet<ArrowTarget>();

    /// <summary>
    /// Places the rectangle at the arrow's position along shotDirection and starts hitting. damage and knockback
    /// already include the bow's charge; damageMultiplier is applied to poison the same way as for other arrows.
    /// </summary>
    public void Fire(ArrowProjectile source, Vector2 shotDirection, GameObject shooter,
        float shotDamage, float shotKnockback, float shotDamageMultiplier, int targetLayers)
    {
        arrow = source;
        data = source.Data;
        owner = shooter;
        direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : Vector2.right;
        damage = shotDamage;
        knockback = shotKnockback;
        damageMultiplier = shotDamageMultiplier;

        float length = data.laserLength;
        float width = data.laserWidth;

        // The object's position is the start of the beam; the rectangle reaches forward along its X axis.
        // A plain Z rotation: FromToRotation can turn a straight-left shot around Y instead, mirroring the sprite.
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        if (!TryGetComponent(out box)) box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(length, width);
        box.offset = new Vector2(length * 0.5f, 0f);

        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(targetLayers);

        SetUpVisual(length, width);

        startTime = Time.time;
        firing = true;

        // Hit straight away, so a short beam can't miss an enemy between two physics steps.
        Physics2D.SyncTransforms();
        CheckHits();

        Destroy(gameObject, data.laserDuration);
    }

    private void FixedUpdate()
    {
        if (firing) CheckHits();
    }

    private void Update()
    {
        if (!firing || beamSprite == null) return;

        // Fade out over the beam's life.
        float t = Mathf.Clamp01((Time.time - startTime) / data.laserDuration);
        Color color = beamColor;
        color.a *= 1f - t;
        beamSprite.color = color;
    }

    private void CheckHits()
    {
        s_Overlaps.Clear();
        box.Overlap(filter, s_Overlaps);

        foreach (Collider2D other in s_Overlaps)
        {
            if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
                continue;

            // Shootable lamps, switches and the like. The beam never stops on them.
            if (other.TryGetComponent(out ArrowTarget arrowTarget))
            {
                if (hitArrowTargets.Add(arrowTarget)) arrowTarget.TryHit(arrow, true);
                continue;
            }

            IDamageable target = ArrowProjectile.FindDamageable(other);
            if (target == null || !hitCharacters.Add(ArrowProjectile.GetCharacter(other)))
                continue;

            target.TakeDamage(damage);

            if (knockback > 0f)
                ArrowProjectile.ApplyKnockback(other, direction * knockback);

            if (data.IsPoisonous)
                PoisonEffect.Apply(target, data.poisonDamage * damageMultiplier, data.poisonTime, data.poisonInterval);
        }
    }

    /// <summary>
    /// Stretches the beam sprite over the rectangle. Without one, hides the arrow's own sprite and draws a plain
    /// rectangle with the same material (so it bends with the tower like the rest of the level).
    /// </summary>
    private void SetUpVisual(float length, float width)
    {
        SpriteRenderer arrowSprite = GetComponent<SpriteRenderer>();

        if (beamSprite == null)
        {
            var visual = new GameObject("Laser Beam Visual");
            visual.transform.SetParent(transform, false);
            beamSprite = visual.AddComponent<SpriteRenderer>();
            beamSprite.sprite = GetPlaceholderSprite();
            beamColor = data.laserColor;

            if (arrowSprite != null)
            {
                beamSprite.sharedMaterial = arrowSprite.sharedMaterial;
                beamSprite.sortingLayerID = arrowSprite.sortingLayerID;
                beamSprite.sortingOrder = arrowSprite.sortingOrder;
            }
        }
        else
        {
            beamColor = beamSprite.color;
        }

        // The arrow's own sprite would sit at the start of the beam.
        if (arrowSprite != null && arrowSprite != beamSprite) arrowSprite.enabled = false;

        Transform visualTransform = beamSprite.transform;
        if (visualTransform == transform)
        {
            Debug.LogWarning($"{name}: put the laser's beam sprite on a child, so it can be stretched without " +
                             "stretching the collider.", this);
            return;
        }

        visualTransform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
        visualTransform.localRotation = Quaternion.identity;

        if (beamSprite.drawMode != SpriteDrawMode.Simple)
        {
            beamSprite.size = new Vector2(length, width);
            visualTransform.localScale = Vector3.one;
        }
        else if (beamSprite.sprite != null)
        {
            Vector2 spriteSize = beamSprite.sprite.bounds.size;
            visualTransform.localScale = new Vector3(
                spriteSize.x > 0f ? length / spriteSize.x : 1f,
                spriteSize.y > 0f ? width / spriteSize.y : 1f,
                1f);
        }
    }

    // A 1x1 white sprite, one unit in size, shared by all placeholder beams.
    private static Sprite GetPlaceholderSprite()
    {
        if (s_PlaceholderSprite != null) return s_PlaceholderSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Laser Placeholder" };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        s_PlaceholderSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return s_PlaceholderSprite;
    }

    private void OnDrawGizmosSelected()
    {
        // Shows the rectangle a shot from here would make, along this object's X axis.
        ArrowProjectile projectile = arrow != null ? arrow : GetComponent<ArrowProjectile>();
        ArrowData shown = projectile != null ? projectile.Data : null;
        if (shown == null || !shown.IsLaser) return;

        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(shown.laserLength * 0.5f, 0f, 0f), new Vector3(shown.laserLength, shown.laserWidth, 0f));
    }
}
