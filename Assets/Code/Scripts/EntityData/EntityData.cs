using UnityEngine;

[CreateAssetMenu(fileName = "EntityData", menuName = "Scriptable Objects/EntityData")]
public class EntityData : ScriptableObject
{
    public int entityID;

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Movement")]
    public float detectionRange = 5f;
    public float moveSpeed = 4f;
    public float jumpSpeed = 1.0f;
    public float dashSpeed = 10f;
    public float dashDuration = 0.2f;
    public float nextDashCooldown = 1f;
    public bool lockZAxis = true;

    [Header("Attack")]
    public float attackDamage = 1f;
    public float attackRange = 2f;
    public float attackCooldown = 5f;
    public float attackSpeed = 1f;
    public GameObject attackEffectPrefab;

    [Header("Gravity")]
    public float fallMultiplier = 2f; // > 1 = faster fall, < 1 = slower fall
    public float riseMultiplier = 1f; // applies while moving upward
    public float maxFallSpeed = 20f; // terminal velocity
}
