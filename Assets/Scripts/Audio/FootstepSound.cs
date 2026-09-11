using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootstepSound : MonoBehaviour
{
    [SerializeField] AudioClip[] footstepSounds;

    public Animator animator;
    private float _lastFootstep;

    private void OnValidate()
    {
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Update()
    {
        var footstep = animator.GetFloat("Footstep");   // based on each animation's curves
        if (Mathf.Abs(footstep) < 0.0001f) footstep = 0;   // if the footstep is very close to 0, set it to 0

        if (_lastFootstep > 0 && footstep < 0 || _lastFootstep < 0 && footstep > 0)
        {
            var randomClip = footstepSounds[Random.Range(0, footstepSounds.Length - 1)];
            AudioSource.PlayClipAtPoint(randomClip, transform.position);
        }
        _lastFootstep = footstep;
    }
}
