using UnityEngine;

/// <summary>
/// An armored area of an enemy: put it on a child object with a Collider2D, on the "Enemy" layer, and place and size the
/// collider in the prefab (as if the enemy faces right when mirrorWithFacing is on). ArrowProjectile checks for it on
/// every hit: player arrows that hit the armor bounce off like a mirror without using a bounce, Penetration arrows pierce
/// it by using one penetration, and Explosive arrows explode but hurt this enemy for less. Explosions that don't hit the
/// armor directly, melee and the Humbaba charge ignore it.
/// The collider is a trigger, so the movement controller (which only collides with Environment) never notices it.
/// Disabling the component disables the collider too, which is how breakable armor can turn it off later.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyArmor : MonoBehaviour
{
    [Tooltip("Mirror the armor to the side the enemy faces (e.g. Humbaba's front plate). Turn off for armor that stays put, like a shell on top.")]
    [SerializeField] private bool mirrorWithFacing = true;
    [Tooltip("Damage multiplier for this enemy when an Explosive arrow hits the armor directly. Other targets in the blast take full damage.")]
    [Range(0f, 1f)] [SerializeField] private float explosionDamageMultiplier = 0.5f;

    private Collider2D armorCollider;
    private EnemyBehavior enemy;
    private Transform character;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private int appliedFacing = 1;

    public float ExplosionDamageMultiplier => explosionDamageMultiplier;
    /// <summary>The enemy root this armor belongs to (the Rigidbody2D's transform, the same one ArrowProjectile tracks hits by).</summary>
    public Transform Character => character;

    private void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer("Enemy");
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        armorCollider = GetComponent<Collider2D>();
        // A trigger, so it never blocks movement; arrows still find it because queries hit triggers.
        armorCollider.isTrigger = true;

        if (gameObject.layer != LayerMask.NameToLayer("Enemy"))
            Debug.LogWarning($"{name}: EnemyArmor should be on the \"Enemy\" layer, otherwise player arrows can't hit it.", this);

        Rigidbody2D body = GetComponentInParent<Rigidbody2D>();
        character = body != null ? body.transform : transform.root;

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;

        enemy = GetComponentInParent<EnemyBehavior>();
        if (mirrorWithFacing && enemy == null)
        {
            Debug.LogError($"{name}: EnemyArmor with Mirror With Facing needs an EnemyBehavior on a parent. Mirroring is turned off.", this);
            mirrorWithFacing = false;
        }
    }

    private void OnEnable()
    {
        if (armorCollider != null) armorCollider.enabled = true;
    }

    private void OnDisable()
    {
        if (armorCollider != null) armorCollider.enabled = false;
    }

    private void FixedUpdate()
    {
        if (!mirrorWithFacing) return;

        int facing = enemy.FacingDirection;
        if (facing == appliedFacing) return;

        appliedFacing = facing;
        transform.localPosition = new Vector3(baseLocalPosition.x * facing, baseLocalPosition.y, baseLocalPosition.z);
        // Flipping the scale too mirrors asymmetric collider shapes (polygons, offset boxes).
        transform.localScale = new Vector3(baseLocalScale.x * facing, baseLocalScale.y, baseLocalScale.z);
        // Arrows check hits this same physics step, so move the collider now instead of at the next simulation.
        Physics2D.SyncTransforms();
    }
}
