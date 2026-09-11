using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerSound : MonoBehaviour
{
    [SerializeField] AudioClip aimSound;
    [SerializeField] AudioClip fireSound;
    [SerializeField] AudioClip reloadSound;
    [SerializeField] AudioClip takeDamageSound;
    [SerializeField] AudioClip deathSound;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void PlayAim() => Play(aimSound);
    public void PlayFire() => Play(fireSound);
    public void PlayReload() => Play(reloadSound);
    public void PlayTakeDamage() => Play(takeDamageSound);
    public void PlayDeath() => Play(deathSound);

    void Play(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}
