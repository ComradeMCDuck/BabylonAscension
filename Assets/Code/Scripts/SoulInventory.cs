using System;
using UnityEngine;

/// <summary>
/// The souls the player has collected, spent at altars. Lives on the player,
/// so the scene reload on death resets it to 0.
/// </summary>
public class SoulInventory : MonoBehaviour
{
    [Tooltip("Souls the player starts with. Shows the current count while playing.")]
    [SerializeField, Min(0)] private int souls;

    /// <summary>Raised with the new count whenever souls are added or spent.</summary>
    public event Action<int> Changed;

    public int Souls => souls;

    public bool CanAfford(int cost) => souls >= cost;

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
