using UnityEngine;
public class EnemyHit : MonoBehaviour
{
    [SerializeField] float damagePoints = 100f;
    Health playerHealth;
    Transform player;
    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null) return;
        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<Health>();
    }
    // Animation event on TEnemyAttack — Function name must match exactly
    public void DealDamage()
    {
        if (!enabled)
            return;

        EnemyHealth enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null && enemyHealth.IsDead)
            return;

        if (playerHealth == null || playerHealth.IsDead)
            return;

        playerHealth.HealthPoints -= damagePoints;
    }
}