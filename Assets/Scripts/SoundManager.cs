using UnityEngine;
using static Collectible;

public class SoundManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
public static SoundManager Instance { get; private set; }

[Header("Pickup Sounds")]
    
[SerializeField] private AudioClip coinPickupSound;

private AudioSource audioSource;

private void Awake()
{
    if (Instance != null && Instance != this)
    {
    Destroy(gameObject);
    return;
    }

    Instance = this;
    DontDestroyOnLoad(gameObject);
    audioSource = GetComponent<AudioSource>();
}

private void OnEnable()
{
    CollectibleEventSystem.OnCollectibleCollected+= PlayPickupSound;
}

private void OnDisable()
{

    CollectibleEventSystem.OnCollectibleCollected -= PlayPickupSound;
}

private void PlayPickupSound(CollectibleType type, int amount)
{
     switch (type)
    {
    case CollectibleType.Coin:

    audioSource.PlayOneShot(coinPickupSound);
    break;
    }
 }

}
