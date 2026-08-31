using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using Unity.Hierarchy;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;

public class ThirdPersonController : MonoBehaviour
{
    private const string speedParamName = "Speed"; // const prevents values being changed from default values
    private const string jumpParamName = "Jump";
    private const string groundedParamName = "Grounded";
    private const string fallingParamName = "Falling";
    private const string quickTurnTriggerName = "QuickTurn";
    private const float lookThreshold = 0.01f;
    private const string xParamName = "x";
    private const string yParamName = "y";

    private Health health;

    [Header("Cinemachine")]
    [SerializeField]
    private Transform cameraTarget;


    [SerializeField]
    private float topClamp = 70.0f;

    [SerializeField]
    private float bottomClamp = -30.0f;

    [Header("Speed")]
    [SerializeField]
    private float lookSpeed = 10f;
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float runningSpeedMultiplier = 1.5f;
    [SerializeField] private float backwardSpeedMultiplier = 0.5f;


    [SerializeField]
    private float turnSpeed = 180f; // sets speed for tank rotation

    [Header("Quickturn")]
    [SerializeField]
    private float quickturnSpeed = 720f;
    private bool isQuickturning = false;
    private bool canQuickturn = true;



    [Header("Aim")]
    [SerializeField]
    private float aimTurnSpeed = 0f;
    private bool isAiming;

    [SerializeField] private float aimBodyAlignSpeed = 20f; // tweak to taste

    [SerializeField] private Transform aimPivot; // assign in Inspector
    public bool IsAiming => isAiming; // with aim script

    private Vector2 look;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;


    [Header("Jump")]
    [SerializeField]
    private float jumpStrength = 7f;

    [SerializeField]
    private float jumpDowntime = 1f;

    [Header("Grounded")]
    [SerializeField]
    private Transform groundCheckPoint;

    [SerializeField]
    private float groundCheckRadius = 0.08f;

    [SerializeField]
    private LayerMask groundLayer;

    private Rigidbody body;
    private Animator animator;
    private Vector2 move;

    private float currentSpeed;
    private float yaw;
    private float pitch;

    private bool isGrounded = true;



    private bool isRunning;
    private bool canJump = true;

    // Lock Inputs
    private bool inputLocked = false;

    public void LockInputs()
    {
        inputLocked = true;
        move = Vector2.zero;      // clear movement ; stops sticky inputs
        isRunning = false;        // clear run
        look = Vector2.zero;      // optional: clear look input
    }

    public void UnlockInputs()
    {
        inputLocked = false;
    }

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        body = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();
    }


    private void Update()
    {

        if (inputLocked)
            return; // stops Look(), RotateBodyToCameraYaw(), etc.


        if (health.IsDead)  // death input freeze guard
            return;

        GroundedCheck(); // check immediately

        if (isAiming)
        {
            Look(); // updates yaw/pitch and rotates cameraTarget
            RotateBodyToCameraYaw(); // keep body aligned to the camera yaw
            RotateBodyToCameraPitch();
        }
        else
        {
            // when exiting aiming returns rotation to movement
            yaw = transform.eulerAngles.y;
        }

    }


    private void FixedUpdate()
    {
        if (health.IsDead) // death freeze input
            return;

        Move(); // before rotation

    }

    private void Move()
    {
        // lock input
        if (inputLocked)
        {
            // Freeze movement
            body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);

            // Stop locomotion animation
            animator.SetFloat("x", 0f);
            animator.SetFloat("y", 0f);
            animator.SetFloat(speedParamName, 0f);

            return;
        }

            // NEW: handle 180° correction after quickturn
            HandleForwardAfterQuickTurn(move);


        float vertical = move.y;     // W/S
        float horizontal = move.x;   // A/D

        // ROTATION (Tank)

        if (!isAiming)
        {
            // tank rotation (movement)
            float currentTurnSpeed = isAiming ? aimTurnSpeed : turnSpeed;
            transform.Rotate(0f, horizontal * currentTurnSpeed * Time.fixedDeltaTime, 0f);
        }


        // RUNNING
        // Determine if moving forward or backward
        bool movingForward = vertical > 0f;
        bool movingBackward = vertical < 0f;

        // Disable running when moving backward
        float baseSpeed = movementSpeed;

        if (movingBackward)
        {
            baseSpeed *= backwardSpeedMultiplier;   // slower backwards
            isRunning = false;                      // force no running
        }

        // Running only allowed when moving forward
        if (movingForward && isRunning)
        {
            baseSpeed *= runningSpeedMultiplier; // running multiplier
        }

        float targetSpeed = baseSpeed * Mathf.Abs(vertical);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.fixedDeltaTime * 8f);



        // MOVEMENT (Forward/back)
        if (!isAiming)
        {
            Vector3 forward = transform.forward;
            Vector3 velocity = forward * vertical * currentSpeed;
            body.linearVelocity = new Vector3(velocity.x, body.linearVelocity.y, velocity.z);
        }
        else
        {
            // Stop movement while aiming
            body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
        }

        // Animator Running
        float maxSpeed = movementSpeed * runningSpeedMultiplier;
        float normalizedAnimSpeed = currentSpeed / maxSpeed;
        animator.SetFloat(speedParamName, normalizedAnimSpeed);

        animator.SetBool(fallingParamName, !isGrounded && body.linearVelocity.y < -0.1f);

        // Animator Locomotion (scaled for walk/run)
        Vector2 input = new Vector2(horizontal, vertical);
        Vector2 normalized = Vector2.ClampMagnitude(input, 1f);

        // Scale X/Y so walk = 0.5, run = 1.0
        float directionScale = (isRunning && vertical > 0f) ? 1f : 0.5f;

        float animX = normalized.x * directionScale;
        float animY = normalized.y * directionScale;

        animator.SetFloat("x", animX, 0.15f, Time.deltaTime);
        animator.SetFloat("y", animY, 0.15f, Time.deltaTime);



    }

    private void Jump()
    {
        if (!isGrounded || !canJump)
        {
            return;
        }

        body.AddForce(Vector3.up * jumpStrength, ForceMode.Impulse);
        canJump = false;
        StartCoroutine(JumpDowntimeCoroutine());

        animator.SetTrigger(jumpParamName);
    }

    private IEnumerator JumpDowntimeCoroutine()
    {
        yield return new WaitForSeconds(0.25f);

        var waitforGrounded = new WaitUntil(() => isGrounded);
        yield return waitforGrounded;

        yield return new WaitForSeconds(jumpDowntime);
        canJump = true;
    }

    private void Look()
    {
        //this moves the camera with player input
        if (look.sqrMagnitude >= lookThreshold)
        {
            float deltaTimeMultiplier = Time.deltaTime * lookSpeed;
            yaw += look.x * deltaTimeMultiplier;
            pitch -= look.y * deltaTimeMultiplier;
        }
        yaw = ClampAngle(yaw, float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp, topClamp);


        cameraTarget.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void RotateBodyToCameraYaw()
    {
        // Build a flat (Y-only) rotation from the current yaw we computed for the camera
        Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);

        // Smoothly rotate the player to match the camera yaw
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * aimBodyAlignSpeed
        );
    }

    private void RotateBodyToCameraPitch()
    {
        if (!aimPivot) return;
        aimPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }


    private float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f)
        {
            lfAngle += 360f;
        }

        if (lfAngle > 360f)
        {
            lfAngle -= 360f;
        }

        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void GroundedCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        animator.SetBool(groundedParamName, isGrounded);
    }

    private void OnDrawGizmosSelected() // debug to check if groundcheckpoint is correct
    {
        if (groundCheckPoint == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(groundCheckPoint.position, groundCheckRadius);
    }

    private void OnMove(InputValue inputValue) // get inputs
    {
        if (inputLocked) return;

        Vector2 raw = inputValue.Get<Vector2>();

        // Block forward/backward movement while aiming
        if (isAiming)
            raw.y = 0f;

        move = raw;
    }

    private void OnJump()
    {
        Jump();
    }

    private void OnRun(InputValue inputValue)
    {
        if (inputLocked) return;

        isRunning = inputValue.isPressed;
    }

    private void OnAim(InputValue inputValue)
    {
        if (inputLocked) return;

        if (health.IsDead) return;

        if (inputValue.isPressed)
            isAiming = !isAiming;

    }


    private void OnQuickturn(InputValue value)
    {
        if (isAiming) return;
        if (value.isPressed)

            TryQuickturn();

    }

    private void TryQuickturn()
    {
        if (canQuickturn && !isQuickturning)
            TryQuickTurn();
    }

    private bool quickTurnJustHappened = false;
    public void OnQuickTurnFinished()
    {
        quickTurnJustHappened = true;
    }
    private IEnumerator SmoothRotate180(float duration)
    {
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot * Quaternion.Euler(0f, 180f, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
    }

    // QUICKTURN PLAYER ROTATION
    private void HandleForwardAfterQuickTurn(Vector2 moveInput)
    {
        if (isAiming) return; // no quickturn inputs while aiming

        bool movingForward = moveInput.y > 0.2f;
        bool movingBackward = moveInput.y < -0.2f;

        // Player must push forward AND a quick-turn must have just happened
        if (quickTurnJustHappened && (movingForward || movingBackward))
        {
            // Smooth 180° rotation over 0.25 seconds (tweak to taste)
            StartCoroutine(SmoothRotate180(0.25f));

            quickTurnJustHappened = false; // prevent repeated flips
        }

    }

    private void TryQuickTurn()
    {
        // no quickturn when jumping
        if (!isGrounded || !canJump)
        {
            return;
        }

        animator.SetTrigger(quickTurnTriggerName);
    }


    private void OnLook(InputValue value)
    {
        look = value.Get<Vector2>();
    }
}
