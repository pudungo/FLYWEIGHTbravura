using UnityEngine;

public class AimUpperBody : MonoBehaviour
{
    [SerializeField] private Transform aimTarget;
    [SerializeField] private float weightLerpSpeed = 15f;
    [SerializeField] private float spineWeight = 0.25f;
    [SerializeField] private float chestWeight = 0.25f;
    [SerializeField] private float headWeight = 0.6f;
    [SerializeField] private float maxPitch = 30f;
    [SerializeField] private float maxYaw = 25f;
    [SerializeField] private float pitchOffset = 12f;

    private Animator animator;
    private ThirdPersonController controller;
    private Transform spine;
    private Transform chest;
    private Transform head;
    private float weight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<ThirdPersonController>();

        if (animator == null)
            return;

        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        head = animator.GetBoneTransform(HumanBodyBones.Head);

        // Bone aim can move verts outside the bind-pose AABB; keep the mesh visible.
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;
    }

    private void LateUpdate()
    {
        bool reloading = controller != null && controller.IsReloadLocked;
        bool aiming = controller != null
            && controller.IsAiming
            && !controller.IsInputLocked
            && !reloading;

        if (reloading)
        {
            weight = 0f;
            return;
        }

        float targetWeight = aiming ? 1f : 0f;
        weight = Mathf.Lerp(weight, targetWeight, Time.deltaTime * weightLerpSpeed);

        if (weight < 0.01f)
            return;

        if (!TryGetClampedLook(out float pitch, out float yaw))
            return;

        AimBone(spine, spineWeight * weight, pitch, yaw);
        AimBone(chest, chestWeight * weight, pitch, yaw);
        AimBone(head, headWeight * weight, pitch, yaw);
    }

    private bool TryGetClampedLook(out float pitch, out float yaw)
    {
        pitch = 0f;
        yaw = 0f;

        Vector3 lookDir;
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Match the crosshair ray. Chest-to-hit aims above the reticle because
            // the camera sits higher than the torso.
            lookDir = cam.transform.forward;
        }
        else if (aimTarget != null)
        {
            Transform origin = chest != null ? chest : transform;
            lookDir = aimTarget.position - origin.position;
            if (lookDir.sqrMagnitude < 0.0001f)
                return false;
            lookDir.Normalize();
        }
        else
        {
            return false;
        }

        Vector3 local = transform.InverseTransformDirection(lookDir);
        yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch += pitchOffset;

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        return true;
    }

    private void AimBone(Transform bone, float boneWeight, float pitch, float yaw)
    {
        if (bone == null || boneWeight <= 0f)
            return;

        // Rotate in character space so Mixamo bone.forward (often along the spine)
        // is not aimed at the sky, which twists arms through the mesh.
        Quaternion extra = Quaternion.AngleAxis(yaw * boneWeight, transform.up)
            * Quaternion.AngleAxis(pitch * boneWeight, transform.right);
        bone.rotation = extra * bone.rotation;
    }
}
