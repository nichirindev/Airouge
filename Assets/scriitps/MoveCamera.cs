using UnityEngine;

/// <summary>
/// Follows the player with smooth bob and crouch tilt.
/// Attach to a Camera or Camera parent GameObject.
/// </summary>
public class MoveCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public PlayerInput playerInput;
    public Camera mainCam;

    [Header("Settings")]
    public bool cinematic;

    private float bobSpeed = 15f;
    private float bobMultiplier = 1f;
    private Vector3 desiredBob;
    private Vector3 bobOffset;
    private float desiredTilt;
    private float tilt;

    public static MoveCamera Instance { get; private set; }

    private void Start() => Instance = this;

    private void LateUpdate()
    {
        UpdateBob();
        transform.position = player.position + bobOffset;

        if (cinematic) return;

        Vector3 cameraRot = playerInput.cameraRot;
        cameraRot.x = Mathf.Clamp(cameraRot.x, -90f, 90f);

        desiredTilt = PlayerMovement.Instance.IsCrouching() ? 6f : 0f;
        if (PlayerMovement.Instance.wallRunning)
        {
            desiredTilt = PlayerMovement.Instance.wallRunRotation;
        }
        tilt = Mathf.Lerp(tilt, desiredTilt, Time.deltaTime * 8f);

        transform.rotation = Quaternion.Euler(cameraRot.x, cameraRot.y, tilt);
    }

    public void BobOnce(Vector3 bobDirection)
    {
        desiredBob = ClampVector(bobDirection * 0.15f, -3f, 3f) * bobMultiplier;
    }

    private void UpdateBob()
    {
        desiredBob = Vector3.Lerp(desiredBob, Vector3.zero, Time.deltaTime * bobSpeed * 0.5f);
        bobOffset = Vector3.Lerp(bobOffset, desiredBob, Time.deltaTime * bobSpeed);
    }

    public void UpdateFov(float f) => mainCam.fieldOfView = f;

    private static Vector3 ClampVector(Vector3 vec, float min, float max) =>
        new(Mathf.Clamp(vec.x, min, max), Mathf.Clamp(vec.y, min, max), Mathf.Clamp(vec.z, min, max));
}
