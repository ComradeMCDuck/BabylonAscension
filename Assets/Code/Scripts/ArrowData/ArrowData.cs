using UnityEngine;

[CreateAssetMenu(fileName = "ArrowData", menuName = "Scriptable Objects/ArrowData")]
public class ArrowData : ScriptableObject
{
    [SerializeField] public float minDamage = 5f;
    [SerializeField] public float maxDamage = 30f;
    [SerializeField] public float minArrowSpeed = 12f;
    [SerializeField] public float maxArrowSpeed = 24f;
    [SerializeField] public float minKnockback = 2f;
    [SerializeField] public float maxKnockback = 12f;
    [SerializeField] public int bounces = 0;
}
