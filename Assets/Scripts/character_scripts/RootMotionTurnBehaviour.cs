using UnityEngine;

public class RootMotionTurnBehaviour : StateMachineBehaviour
{
    private Rigidbody body;
    private ThirdPersonController controller;
    private RigidbodyConstraints cachedConstraints;
    private AnimatorUpdateMode cachedUpdateMode;
    private bool hasCachedConstraints;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        controller = animator.GetComponent<ThirdPersonController>();
        body = animator.GetComponent<Rigidbody>();

        cachedUpdateMode = animator.updateMode;
        animator.updateMode = AnimatorUpdateMode.Fixed;

        if (body != null)
        {
            cachedConstraints = body.constraints;
            hasCachedConstraints = true;
            body.constraints = cachedConstraints & ~RigidbodyConstraints.FreezeRotationY;
            body.angularVelocity = Vector3.zero;
        }

        animator.ResetTrigger("QuickTurn");

        if (controller != null)
            controller.BeginRootMotionTurn();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (controller != null)
            controller.EndRootMotionTurn();

        animator.updateMode = cachedUpdateMode;

        if (body != null && hasCachedConstraints)
        {
            Vector3 euler = body.rotation.eulerAngles;
            body.rotation = Quaternion.Euler(0f, euler.y, 0f);
            body.angularVelocity = Vector3.zero;
            body.constraints = cachedConstraints;
        }

        hasCachedConstraints = false;
        body = null;
        controller = null;
    }
}
