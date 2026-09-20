using UnityEngine;

/// <summary>
/// Physics-based first-person player controller with sliding, counter-movement,
/// and air control. Uses Rigidbody forces for all movement.
///
/// Required: Rigidbody (useGravity=true, interpolation=Interpolate), CapsuleCollider.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform playerCam;
    public Transform orientation;
    public LayerMask whatIsGround;
    public LayerMask whatIsWallrunnable;

    [Header("State")]
    public bool grounded;
    public bool dead;
    public bool secondJump = true;
    public int jumpsLeft = 1;
    public int maxJumps = 1;
    public bool onWall;
    public bool wallRunning;
    public bool surfing;

    [Header("Movement")]
    private float moveSpeed = 4500f;
    private float maxSpeed = 22f;
    private float slideForce = 800f;
    private float slideCounterMovement = 0.12f;
    private float counterMovement = 0.14f;
    private float threshold = 0.01f;
    private float maxSlopeAngle = 35f;
    private float jumpForce = 13f;
    private int jumpCounterResetTime = 10;
    private readonly Vector3 crouchScale = new(1f, 0.5f, 1f);

    private Rigidbody rb;
    private Collider playerCollider;
    private Vector3 playerScale;
    private float playerHeight;
    private float x, y;
    private float fallSpeed;
    private Vector3 lastMoveSpeed;
    private Vector3 normalVector;
    public Vector3 wallNormalVector;
    private float wallRunGravity = 1f;
    public float wallRunRotation;
    private float actualWallRotation;
    private bool onRamp;
    private bool jumping;
    private bool sliding;
    private bool crouching;
    private bool cancellingGrounded;
    private bool cancellingWall;
    private bool cancellingSurf;
    private bool touchingWall;
    private bool readyToJump = true;
    private bool readyToWallrun = true;
    private float delay = 5f;
    private int groundCancel;
    private int wallCancel;
    private int surfCancel;
    private int readyToCounterX;
    private int readyToCounterY;
    private int resetJumpCounter;

    public static PlayerMovement Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        playerHeight = GetComponent<CapsuleCollider>().bounds.size.y;
    }

    private void Start()
    {
        playerScale = transform.localScale;
        playerCollider = GetComponent<Collider>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        wallNormalVector = Vector3.up;
    }

    private void LateUpdate()
    {
        WallRunning();
        FindWallRunRotation();
        touchingWall = false;
    }

    private void Update()
    {
        if (dead) return;
        fallSpeed = rb.linearVelocity.y;
        lastMoveSpeed = rb.linearVelocity.XZVector();
    }

    public void SetInput(Vector2 dir, bool crouching, bool jumping)
    {
        x = dir.x;
        y = dir.y;
        this.crouching = crouching;
        this.jumping = jumping;
    }

    private void CheckInput()
    {
        if (crouching && !sliding) StartCrouch();
        if (!crouching && sliding) StopCrouch();
    }

    public void Movement(float x, float y)
    {
        UpdateCollisionChecks();
        this.x = x;
        this.y = y;
        if (dead) return;

        CheckInput();

        if (!grounded)
            rb.AddForce(Vector3.down * 2f);

        Vector2 vel = FindVelRelativeToLook();
        CounterMovement(x, y, vel);
        RampMovement(vel);

        if (readyToJump && jumping)
            Jump();

        if (crouching && grounded && readyToJump)
        {
            rb.AddForce(Vector3.down * 60f);
        }
        else
        {
            float inputX = x;
            float inputY = y;

            if (x > 0 && vel.x > maxSpeed) inputX = 0f;
            if (x < 0 && vel.x < -maxSpeed) inputX = 0f;
            if (y > 0 && vel.y > maxSpeed) inputY = 0f;
            if (y < 0 && vel.y < -maxSpeed) inputY = 0f;

            float airX = 1f;
            float airY = 1f;

            if (!grounded)
            {
                airX = 0.6f;
                airY = 0.6f;
                if (IsHoldingAgainstVerticalVel(vel))
                {
                    float f = Mathf.Abs(vel.y * 0.025f);
                    if (f < 0.5f) f = 0.5f;
                    airY = Mathf.Abs(f);
                }
            }
            if (grounded && crouching) airY = 0f;

            if (wallRunning)
            {
                airX = 0.3f;
                airY = 0.3f;
            }
            if (surfing)
            {
                airX = 0.7f;
                airY = 0.3f;
            }

            float tiny = 0.01f;
            rb.AddForce(orientation.forward * inputY * moveSpeed * 0.02f * airY);
            rb.AddForce(orientation.right * inputX * moveSpeed * 0.02f * airX);

            if (!grounded)
            {
                if (inputX != 0)
                    rb.AddForce(-orientation.forward * vel.y * moveSpeed * 0.02f * tiny);
                if (inputY != 0)
                    rb.AddForce(-orientation.right * vel.x * moveSpeed * 0.02f * tiny);
            }

            if (!readyToJump)
            {
                resetJumpCounter++;
                if (resetJumpCounter >= jumpCounterResetTime) readyToJump = true;
            }
        }
    }

    public void StartCrouch()
    {
        if (sliding) return;
        sliding = true;
        transform.localScale = crouchScale;
        float crouchOffset = playerHeight * (1f - crouchScale.y) * 0.5f;
        transform.position = new Vector3(transform.position.x, transform.position.y - crouchOffset, transform.position.z);
        if (rb.linearVelocity.magnitude > 0.5f && grounded)
        {
            rb.AddForce(orientation.forward * slideForce);
        }
    }

    public void StopCrouch()
    {
        sliding = false;
        float crouchOffset = playerHeight * (1f - crouchScale.y) * 0.5f;
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + crouchOffset, transform.position.z);
    }

    public void Jump()
    {
        if (!grounded && !wallRunning && !surfing && jumpsLeft <= 0) return;
        if (!readyToJump || !secondJump) return;

        readyToJump = false;
        jumpsLeft--;
        resetJumpCounter = 0;
        secondJump = false;

        rb.AddForce(Vector2.up * jumpForce * 1.5f, ForceMode.Impulse);
        rb.AddForce(normalVector * jumpForce * 0.5f, ForceMode.Impulse);

        if (rb.linearVelocity.y < 0.5f || rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (wallRunning)
        {
            rb.AddForce(wallNormalVector * jumpForce * 3f, ForceMode.Impulse);
            StopWallRun();
        }
    }

    private void CounterMovement(float x, float y, Vector2 mag)
    {
        if (!grounded || jumping) return;

        if (crouching)
        {
            rb.AddForce(moveSpeed * 0.02f * -rb.linearVelocity.normalized * slideCounterMovement);
            return;
        }

        if (Mathf.Abs(mag.x) > threshold && Mathf.Abs(x) < 0.05f && readyToCounterX > 1)
            rb.AddForce(moveSpeed * orientation.right * 0.02f * -mag.x * counterMovement);
        if (Mathf.Abs(mag.y) > threshold && Mathf.Abs(y) < 0.05f && readyToCounterY > 1)
            rb.AddForce(moveSpeed * orientation.forward * 0.02f * -mag.y * counterMovement);

        if (IsHoldingAgainstHorizontalVel(mag))
            rb.AddForce(moveSpeed * orientation.right * 0.02f * -mag.x * counterMovement * 2f);
        if (IsHoldingAgainstVerticalVel(mag))
            rb.AddForce(moveSpeed * orientation.forward * 0.02f * -mag.y * counterMovement * 2f);

        float flatSpeed = Mathf.Sqrt(rb.linearVelocity.x * rb.linearVelocity.x + rb.linearVelocity.z * rb.linearVelocity.z);
        if (flatSpeed > maxSpeed)
        {
            float yVel = rb.linearVelocity.y;
            Vector3 capped = rb.linearVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(capped.x, yVel, capped.z);
        }

        readyToCounterX = Mathf.Abs(x) < 0.05f ? readyToCounterX + 1 : 0;
        readyToCounterY = Mathf.Abs(y) < 0.05f ? readyToCounterY + 1 : 0;
    }

    private bool IsHoldingAgainstHorizontalVel(Vector2 vel)
    {
        if (vel.x < -threshold && x > 0) return true;
        if (vel.x > threshold && x < 0) return true;
        return false;
    }

    private bool IsHoldingAgainstVerticalVel(Vector2 vel)
    {
        if (vel.y < -threshold && y > 0) return true;
        if (vel.y > threshold && y < 0) return true;
        return false;
    }

    private void RampMovement(Vector2 mag)
    {
        if (grounded && onRamp && !crouching && !jumping
            && Mathf.Abs(x) < 0.05f && Mathf.Abs(y) < 0.05f)
        {
            rb.useGravity = false;
            if (rb.linearVelocity.y > 0)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, 0f);
            else if (rb.linearVelocity.y <= 0 && mag.magnitude < 1f)
                rb.linearVelocity = Vector3.zero;
        }
        else
        {
            rb.useGravity = true;
        }
    }

    public Vector2 FindVelRelativeToLook()
    {
        float angle = Mathf.DeltaAngle(orientation.eulerAngles.y, Mathf.Atan2(rb.linearVelocity.x, rb.linearVelocity.z) * Mathf.Rad2Deg);
        float num = 90f - angle;
        float mag = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        float y = mag * Mathf.Cos(angle * Mathf.Deg2Rad);
        return new Vector2(mag * Mathf.Cos(num * Mathf.Deg2Rad), y);
    }

    private bool IsFloor(Vector3 v) => Vector3.Angle(Vector3.up, v) < maxSlopeAngle;

    private void OnCollisionEnter(Collision other)
    {
        int layer = other.gameObject.layer;
        Vector3 normal = other.contacts[0].normal;
        if ((whatIsGround & (1 << layer)) == 0) return;

        if (IsFloor(normal))
        {
            jumpsLeft = maxJumps;
            secondJump = true;
            MoveCamera.Instance?.BobOnce(new Vector3(0f, fallSpeed, 0f));
            GunFloat.Instance?.LandBobOnce(new Vector3(0f, fallSpeed, 0f));
        }
    }

    private void OnCollisionStay(Collision other)
    {
        int layer = other.gameObject.layer;
        if ((whatIsGround & (1 << layer)) == 0) return;

        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;
            if (IsFloor(normal))
            {
                if (wallRunning)
                    StopWallRun();

                onRamp = Vector3.Angle(Vector3.up, normal) > 1f;
                grounded = true;
                normalVector = normal;
                cancellingGrounded = false;
                groundCancel = 0;
            }
            if (IsWall(normal))
            {
                StartWallRun(normal);
                onWall = true;
                touchingWall = true;
                cancellingWall = false;
                wallCancel = 0;
            }
            if (IsSurf(normal))
            {
                surfing = true;
                cancellingSurf = false;
                surfCancel = 0;
            }
        }
    }

    private void UpdateCollisionChecks()
    {
        if (!cancellingGrounded)
        {
            cancellingGrounded = true;
        }
        else
        {
            groundCancel++;
            if (groundCancel > delay)
                grounded = false;
        }

        if (!cancellingWall)
        {
            cancellingWall = true;
        }
        else
        {
            wallCancel++;
            if (wallCancel > delay)
            {
                onWall = false;
                StopWallRun();
            }
        }

        if (!cancellingSurf)
        {
            cancellingSurf = true;
        }
        else
        {
            surfCancel++;
            if (surfCancel > delay)
                surfing = false;
        }
    }

    private bool IsWall(Vector3 v)
    {
        return Mathf.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;
    }

    private bool IsSurf(Vector3 v)
    {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < 89f && angle > maxSlopeAngle;
    }

    private void StartWallRun(Vector3 normal)
    {
        if (!grounded && readyToWallrun)
        {
            wallNormalVector = normal;
            if (!wallRunning)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * 20f, ForceMode.Impulse);
            }
            wallRunning = true;
        }
    }

    private void WallRunning()
    {
        if (wallRunning && touchingWall)
        {
            rb.AddForce(-wallNormalVector * moveSpeed * 0.02f * 0.5f);
            rb.AddForce(Vector3.up * rb.mass * 500f * wallRunGravity * 0.02f);

            Vector3 wallForward = Vector3.Cross(wallNormalVector, Vector3.up).normalized;
            if (Vector3.Dot(wallForward, orientation.forward) < 0f)
                wallForward = -wallForward;
            rb.AddForce(wallForward * y * moveSpeed * 0.02f);
        }
    }

    private void StopWallRun()
    {
        if (wallRunning)
        {
            float wallDot = Vector3.Dot(rb.linearVelocity, -wallNormalVector);
            if (wallDot > 0f)
            {
                rb.linearVelocity -= -wallNormalVector * wallDot;
            }
        }
        wallRunning = false;
    }

    private void FindWallRunRotation()
    {
        if (!wallRunning)
        {
            wallRunRotation = 0f;
            return;
        }

        float current = playerCam.transform.rotation.eulerAngles.y;
        float wallAngle = Vector3.SignedAngle(Vector3.forward, wallNormalVector, Vector3.up);
        float num2 = Mathf.DeltaAngle(current, wallAngle);
        wallRunRotation = (-num2 / 90f) * 15f;

        if (!readyToWallrun) return;

        if ((Mathf.Abs(wallRunRotation) < 4f && y > 0f && Mathf.Abs(x) < 0.1f)
            || (Mathf.Abs(wallRunRotation) > 22f && y < 0f && Mathf.Abs(x) < 0.1f))
        {
            if (!cancellingWall)
            {
                cancellingWall = true;
                wallCancel = 0;
            }
        }
        else
        {
            cancellingWall = false;
            wallCancel = 0;
        }
    }

    public Vector3 GetVelocity() => rb.linearVelocity;
    public float GetFallSpeed() => rb.linearVelocity.y;
    public Collider GetPlayerCollider() => playerCollider;
    public Transform GetPlayerCamTransform() => playerCam;
    public Rigidbody GetRb() => rb;
    public bool IsCrouching() => crouching;
    public bool IsDead() => dead;
}
