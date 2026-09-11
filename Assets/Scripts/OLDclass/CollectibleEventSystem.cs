using UnityEngine;
using System;
public static class CollectibleEventSystem
{
    // Define a static event for when a collectible is collected
    public static event Action<Collectible.CollectibleType, int> OnCollectibleCollected;
    // Method to invoke the event
    public static void RaiseCollectibleCollected(Collectible.CollectibleType type, int amount)
    {
        OnCollectibleCollected?.Invoke(type, amount);
    }
}