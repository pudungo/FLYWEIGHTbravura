using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class AnimationRiggingFootPlanter : MonoBehaviour
{
    [SerializeField] private MultiParentConstraint footRefConstraint;
    [SerializeField] private TwoBoneIKConstraint footIK;
    [SerializeField] private Transform IKTarget;
    [SerializeField] private float rayYOffset = 1;
    [SerializeField] private float rayDistance = 0.1f;
    [SerializeField] private float plantedYOffset = 0.1f;
    [SerializeField] private LayerMask mask;

    private Vector3 rayOrigin;

    private void LateUpdate()
    {
        footIK.weight = 0;
        footRefConstraint.weight = 1;
        transform.position = footRefConstraint.transform.position;
        rayOrigin = transform.position + Vector3.up * rayYOffset;
        var footPos = footRefConstraint.transform.position;

        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayDistance, mask))
        {
            var hitPosY = hit.point.y + plantedYOffset;
            if (footPos.y < hitPosY)
            {
                footIK.weight = 1;
                var pos = hit.point;
                pos.y += plantedYOffset;
                IKTarget.position = pos;
                var tarRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * footRefConstraint.transform.rotation;
                IKTarget.rotation = tarRot;
            }
        }
        Debug.DrawRay(rayOrigin, Vector3.down * rayDistance, Color.red);
    }
}