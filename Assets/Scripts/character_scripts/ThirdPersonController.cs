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

    private const string quickTurnTriggerName = "QuickTurn";
    private const float lookThreshold = 0.01f;
    private const string xParamName = "x";
    private const string yParamName = "y";
    private const float aimTurnAnimDeadzone = 2f;
    private const float aimTurnAnimX = 0.5f;
    private const float aimTurnSmoothTime = 0.08f;

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

    [Header("Aim")]
    [SerializeField]
    private float aimTurnSpeed = 0f;
    private bool isAiming;

    [SerializeField] private float aimBodyAlignSpeed = 10f; // tweak to taste

    [SerializeField] private Transform aimPivot; // assign in Inspector
    public bool IsAiming => isAiming; // with aim script

    private Vector2 look;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    private Rigidbody body;
    private Animator animator;
    private PlayerInput playerInput;
    private Vector2 move;

    private float currentSpeed;
    private float yaw;
    private float pitch;

    private bool isRunning;
    private float aimTurnX;
    private float aimTurnXVelocity;

    // Lock Inputs
    private bool inputLocked = false;
    private bool reloadLocked = false;
    private bool rootMotionTurnActive = false;

    public bool IsInputLocked => inputLocked || reloadLocked;
    public bool IsReloadLocked => reloadLocked;

    public void LockInputs()
    {
        inputLocked = true;
    }

    public void UnlockInputs()
    {
        inputLocked = false;
        if (!IsInputLocked)
            RefreshHeldInputs();
    }

    public void SetReloadLock(bool locked)
    {
        reloadLocked = locked;
        if (!IsInputLocked)
            RefreshHeldInputs();
    }

    public void BeginRootMotionTurn()
    {
        rootMotionTurnActive = true;
    }

    public void EndRootMotionTurn()
    {
        rootMotionTurnActive = false;
    }

    private void Awake() // mouse not visible
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        body = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        health = GetComponent<Health>();
    }


    private void Update()
    {

        if (IsInputLocked)
            return; // stops Look(), RotateBodyToCameraYaw(), etc.


        if (health.IsDead)  // death input freeze guard
            return;


        if (isAiming)
        {
            Look(); // updates yaw/pitch and rotates cameraTarget
            RotateBodyToCameraYaw(); // keep body aligned to the camera yaw
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
        if (IsInputLocked)
        {
            // Freeze movement
            body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);

            // Stop locomotion animation
            animator.SetFloat("x", 0f);
            animator.SetFloat("y", 0f);
            animator.SetFloat(speedParamName, 0f);

            return;
        }


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


        // Animator Locomotion
        float animX;
        float animY;
        if (isAiming)
        {
            float remainingYaw = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
            float targetX = 0f;

            if (Mathf.Abs(look.x) > lookThreshold)
                targetX = Mathf.Sign(look.x) * aimTurnAnimX;
            else if (Mathf.Abs(remainingYaw) > aimTurnAnimDeadzone)
                targetX = Mathf.Sign(remainingYaw) * aimTurnAnimX;

            aimTurnX = Mathf.SmoothDamp(
                aimTurnX,
                targetX,
                ref aimTurnXVelocity,
                aimTurnSmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);

            animX = aimTurnX;
            animY = 0f;
        }
        else
        {
            aimTurnX = 0f;
            aimTurnXVelocity = 0f;

            Vector2 input = new Vector2(horizontal, vertical);
            Vector2 normalized = Vector2.ClampMagnitude(input, 1f);
            float directionScale = (isRunning && vertical > 0f) ? 1f : 0.5f;
            animX = normalized.x * directionScale;
            animY = normalized.y * directionScale;
        }

        if (isAiming)
        {
            animator.SetFloat(xParamName, animX);
            animator.SetFloat(yParamName, animY);
        }
        else
        {
            animator.SetFloat(xParamName, animX, 0.15f, Time.deltaTime);
            animator.SetFloat(yParamName, animY, 0.15f, Time.deltaTime);
        }

    }

    // Apply clip yaw through the Rigidbody so physics cannot discard it.
    // Present only during Walk Turn 180; locomotion stays scripted.
    private void OnAnimatorMove()
    {
        if (!rootMotionTurnActive || body == null)
            return;

        Vector3 euler = (body.rotation * animator.deltaRotation).eulerAngles;
        body.MoveRotation(Quaternion.Euler(0f, euler.y, 0f));
        body.angularVelocity = Vector3.zero;
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


    private void OnMove(InputValue inputValue) // get inputs
    {
        Vector2 raw = inputValue.Get<Vector2>();

        // Block forward/backward movement while aiming
        if (isAiming)
            raw.y = 0f;

        move = raw;
    }


    private void OnRun(InputValue inputValue)
    {
        isRunning = inputValue.isPressed;
    }

    private void OnAim(InputValue inputValue)
    {
        if (IsInputLocked) return;

        if (health.IsDead) return;

        if (inputValue.isPressed)
            isAiming = !isAiming;

    }


    private void OnQuickturn(InputValue value)
    {
        if (IsInputLocked) return;
        if (isAiming) return;
        if (value.isPressed)
            TryQuickTurn();
    }


    private void TryQuickTurn()
    {
        if (IsInputLocked) return;

        animator.SetTrigger(quickTurnTriggerName);
    }


    private void OnLook(InputValue value)
    {
        look = value.Get<Vector2>();
    }

    private void RefreshHeldInputs()
    {
        if (playerInput == null)
            return;

        InputAction moveAction = playerInput.actions.FindAction("Move");
        if (moveAction != null)
        {
            Vector2 raw = moveAction.ReadValue<Vector2>();
            if (isAiming)
                raw.y = 0f;
            move = raw;
        }

        InputAction runAction = playerInput.actions.FindAction("Run");
        if (runAction != null)
            isRunning = runAction.IsPressed();

    }
}
