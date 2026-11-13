using System;
using UnityEngine;

/// <summary>
/// The genii the player has collected, spent at altars. Lives on the player, which every scene load recreates;
/// <see cref="PlayerProgress"/> carries the count into the next scene and through death.
/// </summary>
public class GeniiInventory : MonoBehaviour
{
    [Tooltip("Genii the player starts with. Shows the current count while playing.")]
    [UnityEngine.Serialization.FormerlySerializedAs("souls")]
    [SerializeField, Min(0)] private int genii;

    /// <summary>Raised with the new count whenever genii are added or spent.</summary>
    public event Action<int> Changed;

    public int Count => genii;

    public bool CanAfford(int cost) => genii >= cost;

    /// <summary>Replaces the count, e.g. with the genii carried over from the previous scene.</summary>
    public void SetCount(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == genii) return;
        genii = amount;
        Changed?.Invoke(genii);
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        genii += amount;
        Changed?.Invoke(genii);
    }

    /// <summary>Spends the genii if the player has enough. Returns false and spends nothing otherwise.</summary>
    public bool TrySpend(int cost)
    {
        if (cost < 0 || !CanAfford(cost)) return false;
        genii -= cost;
        Changed?.Invoke(genii);
        return true;
    }
}
