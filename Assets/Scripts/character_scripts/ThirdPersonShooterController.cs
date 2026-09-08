using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class ThirdPersonShooterController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera aimVirtualCamera;
    [SerializeField] private CinemachineCamera reloadVirtualCamera;

    [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();
    [SerializeField] private Transform debugTransform;
    private Animator animator;
    [SerializeField] private Animator platformAnimator;


    private const string fireTriggerName = "Firing";
    private const string reloadStateName = "Reload";
    private const string reloadingTriggerName = "Reloading";

    public float weaponRange = 100f;
    [SerializeField] float enemyDamagePerShot = 10f;
    [SerializeField] int clipSize = 6;
    [SerializeField] float fireRate = 8f;
    [SerializeField] int currentAmmo;

    float nextFireTime;

    private float aimLayerWeight = 0f;

    private Health health;
    private ThirdPersonController controller;

    private void Awake()
    {
        controller = GetComponent<ThirdPersonController>();
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();
        currentAmmo = clipSize;
    }

    private bool isAiming;
    private bool isFiring;
    private bool isReloading;
    private bool reloadStateEntered;
    private bool reloadKeptAimLayer;

    private void OnAim(InputValue inputValue)
    {
        if (controller != null && controller.IsInputLocked)
            return;

        if (health.IsDead)
            return;

        if (inputValue.isPressed)
        {
            isAiming = !isAiming; // toggle
            Aim(isAiming);

        }
    }

    private void OnFire(InputValue value)
    {
        if (!CanUseWeapon()) // Only Fire when Aim
            return;

        if (value.isPressed)
            TryFire();
    }

    private void Aim(bool aiming)
    {
        if (aimVirtualCamera != null)
            aimVirtualCamera.gameObject.SetActive(aiming);
    }

    private void Reload(bool reloading)
    {
        if (reloadVirtualCamera != null)
            reloadVirtualCamera.gameObject.SetActive(reloading);
    }
    
    private void TryFire()
    {
        if (!isAiming || isReloading)
            return;

        if (currentAmmo <= 0)
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
        currentAmmo--;

        animator.SetTrigger(fireTriggerName);

        if (TryGetAimHit(weaponRange, out RaycastHit hit))
        {
            Debug.Log("Hit: " + hit.transform.name);

            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.CompareTag("Enemy"))
                enemyHealth.TakeDamage(enemyDamagePerShot);

            FreezablePlatform platform = hit.transform.GetComponent<FreezablePlatform>();
            if (platform != null)
            {
                platform.ToggleFreeze();
            }
        }
        else
        {
            Debug.Log("Miss");
        }
    }

    private void OnReload(InputValue value)
    {
        if (controller != null && controller.IsInputLocked)
            return;

        if (health != null && health.IsDead)
            return;

        if (value.isPressed)
            TryReload();
    }

    private void TryReload()
    {
        if (isReloading || currentAmmo >= clipSize)
            return;

        isReloading = true;
        reloadStateEntered = false;
        reloadKeptAimLayer = isAiming;

        // Reloading is a trigger: Any State would re-enter Reload (CanTransitionToSelf)
        // and InputLockBehaviour.OnStateExit would unlock movement mid-clip.
        // Play the reload state, then consume the trigger so that cannot happen.
        animator.ResetTrigger(fireTriggerName);
        animator.SetTrigger(reloadingTriggerName);

        if (reloadKeptAimLayer)
        {
            // Keep locomotion on the base layer so the masked shoot overlay
            // does not also play a full-body reload that plants the torso.
            animator.Play(reloadStateName, 1, 0f);
            Reload(true);
        }
        else
        {
            animator.Play(reloadStateName, 0, 0f);
        }

        animator.ResetTrigger(reloadingTriggerName);

        if (controller != null)
            controller.SetReloadLock(true);
    }

    private bool CanUseWeapon()
    {
        if (controller != null && controller.IsInputLocked)
            return false;

        if (health != null && health.IsDead)
            return false;

        return isAiming && !isReloading;
    }

    private bool TryGetAimHit(float range, out RaycastHit hit)
    {
        hit = default;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = cam.ScreenPointToRay(screenCenterPoint);

        // Always ignore the Player layer. The camera sits behind the character,
        // so a ray from that origin hits july_maya9 before the aimed object.
        int layerMask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);

        if (!Physics.Raycast(ray, out hit, range, layerMask))
            return false;

        return !hit.transform.IsChildOf(transform);
    }

    // TESTING to check mouse cursor movements
    public Vector3 screenPosition; 
    public Vector3 worldPosition;

    private void Update()
    {
        // Death no aim input
        if (health.IsDead)
        {
            isAiming = false;
            isReloading = false;
            reloadStateEntered = false;
            reloadKeptAimLayer = false;
            animator.ResetTrigger(reloadingTriggerName);
            animator.ResetTrigger(fireTriggerName);
            Aim(false);
            Reload(false);

            if (controller != null)
                controller.SetReloadLock(false);

            animator.SetLayerWeight(1, 0f);
            return;
        }

        UpdateReloadCamera();

        // Keep the shooting overlay up for the whole reload if we started from aim.
        float targetWeight = (isAiming || (isReloading && reloadKeptAimLayer)) ? 1f : 0f;

         // Smooth fade (tweak 8f for faster/slower blending)
        aimLayerWeight = Mathf.Lerp(aimLayerWeight, targetWeight, Time.deltaTime * 15f);

        animator.SetLayerWeight(1, aimLayerWeight);

        if (isAiming)
            UpdateAimTarget();
    }

    private void UpdateReloadCamera()
    {
        if (!isReloading)
            return;

        animator.ResetTrigger(fireTriggerName);

        if (IsInReloadState())
        {
            reloadStateEntered = true;
            return;
        }

        if (!reloadStateEntered)
            return;

        isReloading = false;
        reloadStateEntered = false;
        reloadKeptAimLayer = false;
        currentAmmo = clipSize;
        animator.ResetTrigger(reloadingTriggerName);
        if (controller != null)
            controller.SetReloadLock(false);
        Reload(false);
    }

    private bool IsInReloadState()
    {
        int layer = reloadKeptAimLayer ? 1 : 0;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
        return current.IsName(reloadStateName)
            || (animator.IsInTransition(layer) && next.IsName(reloadStateName));
    }

    private void UpdateAimTarget()
    {
        if (debugTransform == null)
            return;

        if (!TryGetAimRay(out Ray ray))
            return;

        int layerMask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
        if (Physics.Raycast(ray, out RaycastHit hit, weaponRange, layerMask)
            && !hit.transform.IsChildOf(transform))
        {
            debugTransform.position = hit.point;
            return;
        }

        debugTransform.position = ray.origin + ray.direction * weaponRange;
    }

    private bool TryGetAimRay(out Ray ray)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            ray = default;
            return false;
        }

        Vector2 screenCenterPoint = new Vector2(Screen.width / 2f, Screen.height / 2f);
        ray = cam.ScreenPointToRay(screenCenterPoint);
        return true;
    }
}
