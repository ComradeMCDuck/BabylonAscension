using UnityEngine;

[CreateAssetMenu(fileName = "EntityData", menuName = "Scriptable Objects/EntityData")]
public class EntityData : ScriptableObject
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Movement")]
    public float detectionRange = 5f;
    public float moveSpeed = 4f;
    public bool lockZAxis = true;

    [Header("Attack")]
    public float attackDamage = 1f;
    public float attackRange = 2f;
    public float attackCooldown = 5f;
    public float attackSpeed = 1f;
    public GameObject attackEffectPrefab;
}
