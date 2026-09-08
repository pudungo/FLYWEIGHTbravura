using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    public float StartingHealth = 100f;

    [SerializeField] float currentHealth = 100f;

    Animator animator;
    NavMeshAgent agent;
    Rigidbody body;
    BehaviorGraphAgent brain;
    bool isDead;

    public bool IsDead => isDead;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        brain = GetComponent<BehaviorGraphAgent>();
    }

    void Start()
    {
        currentHealth = StartingHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        if (animator != null)
            animator.SetTrigger("TakeDamage");
    }

    public void StopMovement()
    {
        StopMovementOn(gameObject, animator, agent, body);
    }

    public static void StopMovementOn(GameObject enemy)
    {
        if (enemy == null)
            return;

        StopMovementOn(
            enemy,
            enemy.GetComponent<Animator>(),
            enemy.GetComponent<NavMeshAgent>(),
            enemy.GetComponent<Rigidbody>());
    }

    static void StopMovementOn(GameObject enemy, Animator animator, NavMeshAgent agent, Rigidbody body)
    {
        if (animator != null)
        {
            animator.SetFloat("SpeedMagnitude", 0f);
            animator.applyRootMotion = false;
        }

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;

        StopMovement();

        if (animator != null)
        {
            animator.ResetTrigger("TakeDamage");
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Death");
        }

        EnemyHit hit = GetComponent<EnemyHit>();
        if (hit != null)
            hit.enabled = false;

        // Death is handled here rather than in the graph, so there is no Death
        // branch to switch to and the running Attack branch would keep ticking
        // unless the agent is stopped outright.
        if (brain != null)
            brain.enabled = false;

        Destroy(gameObject, 5f);
    }
}
