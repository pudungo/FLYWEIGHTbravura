using System.Collections;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    public float StartingHealth = 100f;

    [SerializeField] float currentHealth = 100f;
    [SerializeField] float despawnDestroyDelay = 5f;


    Animator animator;
    NavMeshAgent agent;
    Rigidbody body;
    BehaviorGraphAgent brain;
    EnemyDeathVfx deathVfx;
    bool isDead;
    bool isDespawning;

    public bool IsDead => isDead;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        brain = GetComponent<BehaviorGraphAgent>();
        deathVfx = GetComponent<EnemyDeathVfx>();
        if (deathVfx == null)
            deathVfx = gameObject.AddComponent<EnemyDeathVfx>();
    }

    void Start()
    {
        currentHealth = StartingHealth;
    }

    void LateUpdate()
    {
        if (isDead || isDespawning || animator == null)
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool inDespawn = current.IsName("Despawn");
        bool goingToDespawn = animator.IsInTransition(0)
            && animator.GetNextAnimatorStateInfo(0).IsName("Despawn");

        if (!inDespawn && !goingToDespawn)
            return;

        AnimatorStateInfo despawnInfo = inDespawn
            ? current
            : animator.GetNextAnimatorStateInfo(0);
        BeginDespawn(despawnInfo);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || isDespawning)
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
        if (isDead || isDespawning)
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

        StartCoroutine(PlayDeathVfxThenDestroy());
    }

    IEnumerator PlayDeathVfxThenDestroy()
    {
        if (deathVfx != null)
            yield return deathVfx.PlayRoutine(animator);

        Destroy(gameObject);
    }

    void BeginDespawn(AnimatorStateInfo despawnInfo) // after attacking, enemy will despawn
    {
        if (isDead || isDespawning)
            return;

        isDespawning = true;

        StopMovement();

        if (animator != null)
        {
            animator.ResetTrigger("TakeDamage");
            animator.ResetTrigger("Attack");
        }

        EnemyHit hit = GetComponent<EnemyHit>();
        if (hit != null)
            hit.enabled = false;

        if (brain != null)
            brain.enabled = false;

        float speed = animator != null ? Mathf.Max(0.01f, animator.speed) : 1f;
        float length = despawnInfo.length > 0.01f ? despawnInfo.length : 2f;
        float remaining = (1f - Mathf.Clamp01(despawnInfo.normalizedTime)) * length / speed;
        Destroy(gameObject, despawnDestroyDelay);
    }
}
