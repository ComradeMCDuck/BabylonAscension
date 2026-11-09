using System;
using UnityEngine;

/// <summary>
/// The souls the player has collected, spent at altars. Lives on the player, which every scene load recreates;
/// <see cref="PlayerProgress"/> carries the count into the next scene and through death.
/// </summary>
public class SoulInventory : MonoBehaviour
{
    [Tooltip("Souls the player starts with. Shows the current count while playing.")]
    [SerializeField, Min(0)] private int souls;

    /// <summary>Raised with the new count whenever souls are added or spent.</summary>
    public event Action<int> Changed;

    public int Souls => souls;

    public bool CanAfford(int cost) => souls >= cost;

    /// <summary>Replaces the count, e.g. with the souls carried over from the previous scene.</summary>
    public void SetSouls(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == souls) return;
        souls = amount;
        Changed?.Invoke(souls);
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        souls += amount;
        Changed?.Invoke(souls);
    }

    /// <summary>Spends the souls if the player has enough. Returns false and spends nothing otherwise.</summary>
    public bool TrySpend(int cost)
    {
        if (cost < 0 || !CanAfford(cost)) return false;
        souls -= cost;
        Changed?.Invoke(souls);
        return true;
    }
}
