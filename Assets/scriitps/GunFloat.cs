using UnityEngine;

public class GunFloat : MonoBehaviour
{
    [Header("Spring Physics")]
    public float springStiffness = 180f;
    public float springDamping = 12f;

    [Header("Sway (Camera Rotation)")]
    public float posSwayAmount = 0.02f;
    public float posSwayMax = 0.06f;
    public float rotSwayAmount = 2f;
    public float rotSwayMax = 5f;

    [Header("Sway Bands (WASD Drift)")]
    public float bandAmount = 0.005f;
    public float bandSmooth = 4f;

    [Header("Idle Bob")]
    public float bobAmount = 0.0015f;
    public float bobSpeedX = 1.2f;
    public float bobSpeedY = 0.8f;
    public float bobActiveSpeedMult = 3f;

    [Header("Landing Bob")]
    public float landBobAmount = 0.15f;
    public float landBobSpeed = 15f;
    public float landBobClamp = 0.12f;

    [Header("Fire")]
    public Transform firePoint;
    public float damage = 20f;
    public float range = 200f;
    public float fireRate = 10f;
    public float spread = 0.01f;
    public float spreadIncrease = 0.005f;
    public float spreadRecovery = 5f;
    public float maxSpread = 0.08f;

    [Header("Fire Recoil")]
    public float fireKickBack = 0.04f;
    public float fireKickRotX = 2.5f;
    public float fireKickRotZ = 0.8f;
    public float fireRecoilReturnSpeed = 8f;

    [Header("Camera Recoil")]
    public float camRecoilUp = 1.2f;
    public float camRecoilSide = 0.3f;
    public float camRecoilSpeed = 10f;

    [Header("Muzzle Flash")]
    public GameObject muzzleFlashPrefab;
    public float flashDestroyDelay = 0.1f;

    [Header("Impact")]
    public GameObject impactEffectPrefab;

    private Vector3 initPos;
    private Quaternion initRot;

    private Vector3 posDisplace;
    private Vector3 posSpringVel;
    private Vector3 rotDisplace;
    private Vector3 rotSpringVel;

    private Vector3 bandOffset;
    private Vector3 bandVel;

    private Vector3 bobOffset;
    private float bobTimer;

    private Vector3 desiredLandBob;
    private Vector3 landBobOffset;

    private Vector3 fireRecoilOffset;
    private Vector3 fireRecoilVel;
    private Vector3 fireRecoilRotOffset;
    private Vector3 fireRecoilRotVel;
    private float currentSpread;
    private float fireCooldown;
    private Camera cam;
    private PlayerInput pInput;
    private PlayerMovement pMove;
    private float camRecoilX;
    private float camRecoilSideOffset;
    private float camRecoilVelX;
    private float camRecoilVelSide;
    private float prevCamRecoilX;
    private float prevCamRecoilSide;

    private Vector3 prevCamEuler;

    public static GunFloat Instance { get; private set; }

    private void Start()
    {
        Instance = this;
        initPos = transform.localPosition;
        initRot = transform.localRotation;
        cam = Camera.main;
        pInput = PlayerInput.Instance;
        pMove = PlayerMovement.Instance;
        prevCamEuler = cam.transform.eulerAngles;
        currentSpread = spread;

        if (firePoint == null)
            firePoint = transform;
    }

    private void Update()
    {
        fireCooldown -= Time.deltaTime;
        currentSpread = Mathf.MoveTowards(currentSpread, spread, spreadRecovery * Time.deltaTime);

        if (Input.GetMouseButton(0) && fireCooldown <= 0f)
            Fire();
    }

    private void LateUpdate()
    {
        Vector3 euler = cam.transform.eulerAngles;
        float dX = Mathf.DeltaAngle(prevCamEuler.y, euler.y);
        float dY = Mathf.DeltaAngle(prevCamEuler.x, euler.x);
        prevCamEuler = euler;

        UpdateSpringSway(dX, dY);
        UpdateBands();
        UpdateBob();
        UpdateLandingBob();
        UpdateFireRecoil();
        UpdateCameraRecoil();
        Apply();
    }

    private void Fire()
    {
        fireCooldown = 1f / fireRate;
        currentSpread = Mathf.Min(currentSpread + spreadIncrease, maxSpread);

        Vector3 dir = cam.transform.forward;
        dir += cam.transform.up * Random.Range(-currentSpread, currentSpread);
        dir += cam.transform.right * Random.Range(-currentSpread, currentSpread);
        dir.Normalize();

        if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, range))
        {
            if (impactEffectPrefab != null)
                Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }

        FireRecoil();

        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            Destroy(flash, flashDestroyDelay);
        }
    }

    private void FireRecoil()
    {
        fireRecoilOffset = new Vector3(0f, 0f, -fireKickBack);
        fireRecoilVel = Vector3.zero;
        fireRecoilRotOffset = new Vector3(-fireKickRotX, 0f, Random.Range(-fireKickRotZ, fireKickRotZ));
        fireRecoilRotVel = Vector3.zero;

        camRecoilX = -camRecoilUp;
        camRecoilSideOffset = Random.Range(-camRecoilSide, camRecoilSide);
    }

    private void UpdateFireRecoil()
    {
        fireRecoilOffset = Vector3.SmoothDamp(fireRecoilOffset, Vector3.zero, ref fireRecoilVel, 1f / fireRecoilReturnSpeed);
        fireRecoilRotOffset = Vector3.SmoothDamp(fireRecoilRotOffset, Vector3.zero, ref fireRecoilRotVel, 1f / fireRecoilReturnSpeed);
    }

    private void UpdateCameraRecoil()
    {
        camRecoilX = Mathf.SmoothDamp(camRecoilX, 0f, ref camRecoilVelX, 1f / camRecoilSpeed);
        camRecoilSideOffset = Mathf.SmoothDamp(camRecoilSideOffset, 0f, ref camRecoilVelSide, 1f / camRecoilSpeed);
    }

    private void UpdateSpringSway(float dX, float dY)
    {
        float tx = Mathf.Clamp(-dX * posSwayAmount, -posSwayMax, posSwayMax);
        float ty = Mathf.Clamp(-dY * posSwayAmount, -posSwayMax, posSwayMax);
        float rx = Mathf.Clamp(-dY * rotSwayAmount, -rotSwayMax, rotSwayMax);
        float ry = Mathf.Clamp(dX * rotSwayAmount, -rotSwayMax, rotSwayMax);

        posDisplace.x = Spring(posDisplace.x, tx, ref posSpringVel.x);
        posDisplace.y = Spring(posDisplace.y, ty, ref posSpringVel.y);
        rotDisplace.x = Spring(rotDisplace.x, rx, ref rotSpringVel.x);
        rotDisplace.y = Spring(rotDisplace.y, ry, ref rotSpringVel.y);
    }

    private void UpdateBands()
    {
        Vector2 inp = pInput.GetAxisInput();
        float tx = Mathf.Clamp(-inp.x * bandAmount, -bandAmount, bandAmount);
        float ty = Mathf.Clamp(-inp.y * bandAmount, -bandAmount, bandAmount);
        float t = 1f / bandSmooth;
        bandOffset.x = Mathf.SmoothDamp(bandOffset.x, tx, ref bandVel.x, t);
        bandOffset.y = Mathf.SmoothDamp(bandOffset.y, ty, ref bandVel.y, t);
    }

    private void UpdateBob()
    {
        float moving = pInput.GetAxisInput().magnitude > 0.1f ? bobActiveSpeedMult : 1f;
        bobTimer += Time.deltaTime * moving;

        float bx = Mathf.Sin(bobTimer * bobSpeedX) * bobAmount;
        float by = Mathf.Abs(Mathf.Sin(bobTimer * bobSpeedY)) * bobAmount;
        bobOffset.x = Mathf.Lerp(bobOffset.x, bx, Time.deltaTime * 8f);
        bobOffset.y = Mathf.Lerp(bobOffset.y, by, Time.deltaTime * 8f);
    }

    private void UpdateLandingBob()
    {
        desiredLandBob = Vector3.Lerp(desiredLandBob, Vector3.zero, Time.deltaTime * landBobSpeed * 0.5f);
        landBobOffset = Vector3.Lerp(landBobOffset, desiredLandBob, Time.deltaTime * landBobSpeed);
    }

    public void LandBobOnce(Vector3 fallVelocity)
    {
        float strength = Mathf.Clamp(Mathf.Abs(fallVelocity.y) * landBobAmount, 0f, landBobClamp);
        desiredLandBob = new Vector3(0f, -strength, 0f);
    }

    private void Apply()
    {
        transform.localPosition = initPos
            + posDisplace
            + bandOffset
            + bobOffset
            + fireRecoilOffset
            + landBobOffset;

        transform.localRotation = initRot
            * Quaternion.Euler(
                rotDisplace.x + fireRecoilRotOffset.x,
                rotDisplace.y + fireRecoilRotOffset.y,
                fireRecoilRotOffset.z
            );

        pInput.SetMouseOffset(pInput.GetMouseOffset() - prevCamRecoilX + camRecoilX);
        pInput.Orientation.localRotation *= Quaternion.Euler(0f, -prevCamRecoilSide + camRecoilSideOffset, 0f);
        prevCamRecoilX = camRecoilX;
        prevCamRecoilSide = camRecoilSideOffset;
    }

    private float Spring(float current, float target, ref float vel)
    {
        float disp = current - target;
        vel += (-springStiffness * disp - springDamping * vel) * Time.deltaTime;
        return current + vel * Time.deltaTime;
    }
}
