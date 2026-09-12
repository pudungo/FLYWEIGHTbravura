using Unity.Cinemachine;
using UnityEngine;

public class Recoil : MonoBehaviour
{
    [SerializeField] float recoilPitch = 3f;
    [SerializeField] float recoilYaw = 1f;
    [SerializeField] float recoilRoll = 0f;
    [SerializeField] float snappiness = 25f;
    [SerializeField] float returnSpeed = 5f;

    [SerializeField] Transform recoilPivot;
    Vector3 current;
    Vector3 target;

    private void Awake()
    {
        if (recoilPivot == null)
            return;

        ThirdPersonShooterController shooter = GetComponent<ThirdPersonShooterController>();
        CinemachineCamera aimCamera = shooter != null ? shooter.AimCamera : null;
        if (aimCamera == null)
            return;

        var aimTarget = aimCamera.Target;
        aimTarget.TrackingTarget = recoilPivot;
        aimCamera.Target = aimTarget;
    }

    private void LateUpdate()
    {
        target = Vector3.Lerp(target, Vector3.zero, returnSpeed * Time.deltaTime);
        current = Vector3.Slerp(current, target, snappiness * Time.deltaTime);

        if (recoilPivot != null)
            recoilPivot.localRotation = Quaternion.Euler(current);
    }

    public void Fire()
    {
        target += new Vector3(
            -recoilPitch,
            Random.Range(-recoilYaw, recoilYaw),
            Random.Range(-recoilRoll, recoilRoll));
    }

    public void ResetRecoil()
    {
        current = Vector3.zero;
        target = Vector3.zero;
        if (recoilPivot != null)
            recoilPivot.localRotation = Quaternion.identity;
    }
}
