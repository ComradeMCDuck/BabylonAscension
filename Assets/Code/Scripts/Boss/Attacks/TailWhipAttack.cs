using System.Collections;
using UnityEngine;

/// <summary>
/// Mid range attack: the tail lashes out along the ground in the direction the boss faces, then pulls back.
/// The player has to jump over it. Damages the player at most once per whip.
/// Setup: a child of the boss with a BoxCollider2D and a DamageHitbox (optionally a 1x1 unit debug sprite).
/// The script sizes it through its scale, so the BoxCollider2D is reset to size 1x1 with no offset.
/// Keep it out of the Visual Root, because it positions itself.
/// </summary>
public class TailWhipAttack : BossAttack<TailWhipData>
{
    [Tooltip("Hitbox on a child of the boss with a BoxCollider2D. Sized and placed by this script.")]
    [SerializeField] private DamageHitbox hitbox;

    protected override void Awake()
    {
        base.Awake();

        if (hitbox == null)
        {
            Debug.LogError($"{name}: TailWhipAttack needs a Hitbox assigned. Disabling.", this);
            enabled = false;
            return;
        }

        // The scale is the size, so the collider itself has to be one unit.
        if (hitbox.TryGetComponent(out BoxCollider2D box))
        {
            box.size = Vector2.one;
            box.offset = Vector2.zero;
        }
        else
        {
            Debug.LogWarning($"{name}: The tail whip hitbox should use a BoxCollider2D, so it can be stretched along the ground.", this);
        }
    }

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        int facing = Boss.FacingDirection;
        SetLength(0f, facing);
        hitbox.Activate(data.damage);

        // Lash out fast (ease out), hold, then pull back.
        yield return Animate(data.extendTime, t => 1f - (1f - t) * (1f - t), facing);
        yield return Animate(data.holdTime, _ => 1f, facing);
        yield return Animate(data.retractTime, t => 1f - t * t, facing);
    }

    private IEnumerator Animate(float duration, System.Func<float, float> amountOverTime, int facing)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;
            SetLength(data.reach * amountOverTime(Mathf.Clamp01(elapsed / duration)), facing);
            hitbox.CheckHits();
        }
    }

    /// <summary>Stretches the hitbox from the tail's start to length in front of it.</summary>
    private void SetLength(float length, int facing)
    {
        Bounds body = Boss.SelfCollider.bounds;
        float frontX = facing > 0 ? body.max.x : body.min.x;
        float startX = frontX + data.startOffset * facing;
        float centerY = body.min.y + data.heightAboveFeet;

        Transform t = hitbox.transform;
        t.position = new Vector3(startX + facing * length * 0.5f, centerY, t.position.z);
        t.rotation = Quaternion.identity;
        SetWorldScale(t, new Vector2(Mathf.Max(length, 0.01f), data.thickness));
    }

    /// <summary>Sets the scale so the object ends up this big in world space, whatever its parents' scale is.</summary>
    private static void SetWorldScale(Transform t, Vector2 size)
    {
        Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            size.x / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            size.y / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            1f);
    }

    protected override void Cleanup()
    {
        if (hitbox != null) hitbox.Deactivate();
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null || !TryGetGizmoFrame(out _, out int facing)) return;

        BossBrain brain = GetComponentInParent<BossBrain>();
        Collider2D bodyCollider = brain != null ? brain.GetComponent<Collider2D>() : null;
        if (bodyCollider == null) return;

        Bounds body = bodyCollider.bounds;
        float frontX = facing > 0 ? body.max.x : body.min.x;
        float startX = frontX + data.startOffset * facing;
        var center = new Vector3(startX + facing * data.reach * 0.5f, body.min.y + data.heightAboveFeet);

        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(data.reach, data.thickness));
    }
}
