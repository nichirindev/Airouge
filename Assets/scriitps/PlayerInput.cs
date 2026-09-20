using UnityEngine;

/// <summary>
/// Reads keyboard/mouse input and feeds it to PlayerMovement and the camera.
/// Attach to the same GameObject as PlayerMovement.
/// </summary>
public class PlayerInput : MonoBehaviour
{
    [Header("Camera Output")]
    public Vector3 cameraRot;

    [Header("Settings")]
    public bool active = true;
    private float sensitivity = 80f;
    private float sensMultiplier = 1f;

    private float xRotation;
    private float desiredX;
    private float x, y;
    private bool jumping;
    private bool crouching;
    private Transform playerCam;
    private Transform orientation;
    private PlayerMovement playerMovement;

    public static PlayerInput Instance { get; private set; }
    public Transform Orientation => orientation;

    private void Awake()
    {
        Instance = this;
        playerMovement = GetComponent<PlayerMovement>();
        playerCam = playerMovement.playerCam;
        orientation = playerMovement.orientation;
    }

    private void Update()
    {
        if (!active) return;
        MyInput();
        Look();
    }

    private void FixedUpdate()
    {
        if (!active) return;
        playerMovement.Movement(x, y);
    }

    private void MyInput()
    {
        if (playerMovement == null) return;

        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

        if (Input.GetButtonUp("Jump"))
            PlayerMovement.Instance.secondJump = true;

        playerMovement.SetInput(new Vector2(x, y), crouching, jumping);
    }

    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * 0.02f * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * 0.02f * sensMultiplier;

        desiredX = playerCam.localRotation.eulerAngles.y + mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraRot = new Vector3(xRotation, desiredX, 0f);
        orientation.localRotation = Quaternion.Euler(0f, desiredX, 0f);
    }

    public void StopCinematic(float x)
    {
        active = true;
        xRotation = x;
    }

    public void UpdateSensitivity(float s) => sensMultiplier = s;
    public Vector2 GetAxisInput() => new(x, y);
    public float GetMouseX() => Input.GetAxis("Mouse X") * sensitivity * 0.02f * sensMultiplier;
    public void SetMouseOffset(float o) => xRotation = o;
    public float GetMouseOffset() => xRotation;
}
