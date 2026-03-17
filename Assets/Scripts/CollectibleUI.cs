using UnityEngine;
using TMPro;

/// Simple UI bridge that listens to CollectibleManager and
/// updates on-screen text for coins whenever the
/// player picks up a collectible.
public class CollectibleUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI coinsText;

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenManagerReady());
    }

    private void OnDisable()
    {
        if (CollectibleManager.Instance != null)
        {
            CollectibleManager.Instance.OnTotalsChanged -= HandleTotalsChanged;
        }
    }

    private System.Collections.IEnumerator SubscribeWhenManagerReady()
    {
        // Wait until the CollectibleManager singleton is initialized
        while (CollectibleManager.Instance == null)
        {
            yield return null;
        }

        CollectibleManager.Instance.OnTotalsChanged += HandleTotalsChanged;

        // Initialize UI with current totals in case there were pickups before this enabled.
        HandleTotalsChanged(
            CollectibleManager.Instance.CoinsTotal,
            CollectibleManager.Instance.GemsTotal
        );
    }

    private void HandleTotalsChanged(int coins, int gems)
    {
        if (coinsText != null)
        {
            coinsText.text = coins.ToString();
        }
    }
}

