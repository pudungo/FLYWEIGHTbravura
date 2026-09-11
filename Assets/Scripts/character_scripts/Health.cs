using UnityEngine;
using UnityEngine.InputSystem.Processors;

public class Health : MonoBehaviour
{
    public float StartingHealth = 100f;
    private Animator animator;
    private bool isDead = false;
    public bool IsDead => isDead;

    private ThirdPersonController controller;
    private PlayerSound playerSound;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<ThirdPersonController>();
        playerSound = GetComponent<PlayerSound>();
    }
    public float HealthPoints
    {
        get { return _HealthPoints; }
        set
        {
            float oldHealth = _HealthPoints;
            _HealthPoints = Mathf.Clamp(value, 0f, 100f);

            // If health went down, play damage animation
            if (_HealthPoints < oldHealth && !isDead)
            {
                animator.SetTrigger("TakeDamage");
                if (_HealthPoints > 0f)
                    playerSound?.PlayTakeDamage();

                // Lock now so Move() cannot re-apply velocity before the clip starts.
                // InputLockBehaviour on the TakeDamage state unlocks when the clip ends.
                if (controller != null)
                    controller.LockInputs();
            }

            if (_HealthPoints <= 0f)
            {
                Die();
            }
        }
    }


    [SerializeField]
    private float _HealthPoints = 100f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HealthPoints = StartingHealth;
    }

    void Die()
    {
        if (isDead) return; 

        isDead = true;
        playerSound?.PlayDeath();

        animator.ResetTrigger("TakeDamage"); // stops takedamage anim when dead
        animator.SetLayerWeight(1, 0f);
        animator.SetTrigger("Death");


    }

}
