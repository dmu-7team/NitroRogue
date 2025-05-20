using UnityEngine;
using Mirror;
using UnityEngine.UI;
using System.Collections;

public class WeaponSystemRBM : MonoBehaviour
{
    public enum WeaponType { DMR, SMG, AR, Sniper, SG, GL }

    [Header("Weapon Settings")]
    public WeaponType weaponType;
    public Transform muzzle;
    public GameObject bulletTrailPrefab;
    public int maxAmmo = 30;
    public float bulletForce = 100f;
    public int weaponDamage = 1;

    private int currentAmmo;
    private bool isReloading = false;
    private bool isOnCooldown = false;

    [Header("Camera & Aiming")]
    public Camera playerCamera;
    public Transform cameraHolder;
    public Transform defaultCamPos;
    public Transform aimCamPos;
    public float camTransitionSpeed = 5f;
    public float scopedFOV = 30f;
    private float defaultFOV;

    public enum AimMode { Zoom, Scope }

    [Header("Aiming Mode")]
    public AimMode aimMode = AimMode.Zoom;

    [Header("UI")]
    public GameObject scopeOverlay;
    public GameObject crosshair;
    public Text ammoText;

    [Header("Animator & FX")]
    public Animator animator;
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;

    void Start()
    {
        currentAmmo = maxAmmo;
        if (playerCamera != null)
            defaultFOV = playerCamera.fieldOfView;

        scopeOverlay?.SetActive(false);
        crosshair?.SetActive(true);
        UpdateAmmoUI();
    }

    void Update()
    {
        HandleAim();

        switch (weaponType)
        {
            case WeaponType.SMG:
                if (Input.GetMouseButton(0))
                    TryFire();
                break;
            case WeaponType.AR:
            case WeaponType.DMR:
            case WeaponType.SG:
            case WeaponType.Sniper:
                if (Input.GetMouseButtonDown(0))
                    TryFire();
                break;
        }
    }

    void TryFire()
    {
        if (isReloading || currentAmmo <= 0 || animator.GetBool("isRunning") || isOnCooldown)
            return;

        switch (weaponType)
        {
            case WeaponType.AR:
                StartCoroutine(FireBurst(3, 0.1f));
                break;
            case WeaponType.Sniper:
                FireSingleShot();
                StartCoroutine(Cooldown(1.0f)); // SR ÄðÅ¸ÀÓ
                break;
            case WeaponType.SG:
                FireShotgun();
                break;
            default:
                FireSingleShot();
                break;
        }
    }

    void FireSingleShot()
    {
        currentAmmo--;
        UpdateAmmoUI();
        animator.SetTrigger("Shoot");
        FireRay(playerCamera.transform.forward);
        PlayEffects();
    }

    IEnumerator FireBurst(int shots, float interval)
    {
        isOnCooldown = true;
        for (int i = 0; i < shots && currentAmmo > 0; i++)
        {
            FireSingleShot();
            yield return new WaitForSeconds(interval);
        }
        isOnCooldown = false;
    }

    void FireShotgun()
    {
        currentAmmo--;
        UpdateAmmoUI();
        animator.SetTrigger("Shoot");
        PlayEffects();

        int pelletCount = 8;
        float spreadAngle = 6f;

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 spreadDir = ApplySpread(playerCamera.transform.forward, spreadAngle);
            FireRay(spreadDir);
        }
    }

    void FireRay(Vector3 direction)
    {
        Ray ray = new Ray(playerCamera.transform.position, direction);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var pc = NetworkClient.localPlayer.GetComponent<PlayerControllerRBM>();
            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
            if (enemy != null)
                pc?.CmdDealDamage(enemy.gameObject, weaponDamage);

            pc?.CmdSpawnTrail(muzzle.position, hit.point);
        }
        else
        {
            var pc = NetworkClient.localPlayer.GetComponent<PlayerControllerRBM>();
            pc?.CmdSpawnTrail(muzzle.position, muzzle.position + direction * 100f);
        }
    }

    Vector3 ApplySpread(Vector3 direction, float angle)
    {
        float yaw = Random.Range(-angle, angle);
        float pitch = Random.Range(-angle, angle);
        return Quaternion.Euler(pitch, yaw, 0) * direction;
    }

    IEnumerator Cooldown(float delay)
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(delay);
        isOnCooldown = false;
    }

    public void HandleReload()
    {
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo && !isReloading)
        {
            animator.SetTrigger("Reload");
            StartCoroutine(ReloadAfterDelay(2.667f));
        }
    }

    private IEnumerator ReloadAfterDelay(float delay)
    {
        isReloading = true;
        yield return new WaitForSeconds(delay);
        currentAmmo = maxAmmo;
        isReloading = false;
        UpdateAmmoUI();
    }

    public bool IsReloading() => isReloading;

    void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = currentAmmo + " / " + maxAmmo;
    }

    private void HandleAim()
    {
        bool isAiming = Input.GetMouseButton(1);

        switch (aimMode)
        {
            case AimMode.Zoom:
                HandleZoom(isAiming);
                break;
            case AimMode.Scope:
                if (isAiming && !isScoped)
                    StartCoroutine(OnScoped());
                else if (!isAiming && isScoped)
                    OnUnscoped();
                break;
        }
    }

    private void HandleZoom(bool isAiming)
    {
        Transform target = isAiming ? aimCamPos : defaultCamPos;
        cameraHolder.position = Vector3.Lerp(cameraHolder.position, target.position, Time.deltaTime * camTransitionSpeed);
        cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, target.rotation, Time.deltaTime * camTransitionSpeed);

        crosshair?.SetActive(true);
        scopeOverlay?.SetActive(false);
        if (playerCamera != null)
            playerCamera.fieldOfView = isAiming ? scopedFOV : defaultFOV;
        isScoped = false;
    }

    private bool isScoped = false;

    private IEnumerator OnScoped()
    {
        yield return new WaitForSeconds(0.1f);
        scopeOverlay?.SetActive(true);
        crosshair?.SetActive(false);
        playerCamera.fieldOfView = scopedFOV;
        isScoped = true;

        cameraHolder.position = Vector3.Lerp(cameraHolder.position, aimCamPos.position, Time.deltaTime * camTransitionSpeed);
        cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, aimCamPos.rotation, Time.deltaTime * camTransitionSpeed);
    }

    private void OnUnscoped()
    {
        StopAllCoroutines();
        scopeOverlay?.SetActive(false);
        crosshair?.SetActive(true);
        playerCamera.fieldOfView = defaultFOV;
        isScoped = false;
        StartCoroutine(SmoothUnscope());
    }

    private IEnumerator SmoothUnscope()
    {
        float t = 0f;
        Vector3 startPos = cameraHolder.position;
        Quaternion startRot = cameraHolder.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * camTransitionSpeed;
            cameraHolder.position = Vector3.Lerp(startPos, defaultCamPos.position, t);
            cameraHolder.rotation = Quaternion.Lerp(startRot, defaultCamPos.rotation, t);
            yield return null;
        }
    }

    void PlayEffects()
    {
        muzzleFlash?.Play();
        audioSource?.Play();
    }
}
