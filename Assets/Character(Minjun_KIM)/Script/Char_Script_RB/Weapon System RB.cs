using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WeaponSystemRB : MonoBehaviour
{
    public enum WeaponType { DMR, SMG, AR, Sniper, SG, GL }

    [Header("Weapon Settings")]
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletForce = 100f;
    [SerializeField] private int weaponDamage = 1;
    [SerializeField] private int currentAmmo = 30;
    [SerializeField] private int maxAmmo = 30;

    [Header("UI")]
    [SerializeField] private Text ammoText;
    [SerializeField] private GameObject scopeOverlay;
    [SerializeField] private GameObject crosshair;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Transform defaultCamPos;
    [SerializeField] private Transform aimCamPos;
    [SerializeField] private float camTransitionSpeed = 5f;
    [SerializeField] private float scopedFOV = 30f;

    public enum AimMode { Zoom, Scope }

    [Header("Aiming Mode")]
    [SerializeField] private AimMode aimMode = AimMode.Zoom;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip smgSound;
    [SerializeField] private AudioClip arSound;
    [SerializeField] private AudioClip dmrSound;
    [SerializeField] private AudioClip sniperSound;
    [SerializeField] private AudioClip shotgunSound;
    [SerializeField] private AudioClip glSound;

    [Header("Trail Effect")]
    [SerializeField] private GameObject bulletTrailPrefab;

    private bool isReloading = false;
    private bool isScoped = false;
    private float defaultFOV;

    void Start()
    {
        UpdateAmmoUI();
        defaultFOV = playerCamera.fieldOfView;
        scopeOverlay?.SetActive(false);
        crosshair?.SetActive(true);
    }

    void Update()
    {
        HandleAim();
    }

    public void HandleFire()
    {
        if (!Input.GetMouseButtonDown(0) || animator.GetBool("isRunning") || currentAmmo <= 0 || isReloading)
            return;

        currentAmmo--;
        UpdateAmmoUI();
        animator.SetTrigger("Shoot");

        FireBulletBasedOnType();
    }

    void FireBulletBasedOnType()
    {
        PlayMuzzleFlash();
        PlayWeaponSound();

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint = ray.origin + ray.direction * 100f;
        Vector3 fireDirection = (targetPoint - muzzle.position).normalized;

        /*
        // ❌ GL은 현재 사용하지 않음
        if (weaponType == WeaponType.GL && bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(fireDirection));
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            bulletScript?.Init(fireDirection, bulletForce, true);
            return;
        }
        */

        if (weaponType == WeaponType.SG)
        {
            int pelletCount = 8;
            float spreadAngle = 6f;

            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 spreadDir = ApplySpread(playerCamera.transform.forward, spreadAngle);
                Ray pelletRay = new Ray(playerCamera.transform.position, spreadDir);

                if (Physics.Raycast(pelletRay, out RaycastHit hit, 100f))
                {
                    EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
                    if (enemy != null)
                        enemy.TakeDamage(weaponDamage);

                    if (ShouldDrawTrail())
                        CreateBulletTrail(muzzle.position, hit.point);
                }
                else
                {
                    if (ShouldDrawTrail())
                        CreateBulletTrail(muzzle.position, muzzle.position + spreadDir * 100f);
                }
            }
        }
        else
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
                if (enemy != null)
                    enemy.TakeDamage(weaponDamage);

                if (ShouldDrawTrail())
                    CreateBulletTrail(muzzle.position, hit.point);
            }
            else
            {
                if (ShouldDrawTrail())
                    CreateBulletTrail(muzzle.position, muzzle.position + fireDirection * 100f);
            }
        }
    }

    Vector3 ApplySpread(Vector3 direction, float angle)
    {
        float yaw = Random.Range(-angle, angle);
        float pitch = Random.Range(-angle, angle);
        return Quaternion.Euler(pitch, yaw, 0) * direction;
    }

    void CreateBulletTrail(Vector3 start, Vector3 end)
    {
        if (bulletTrailPrefab == null) return;

        GameObject trail = Instantiate(bulletTrailPrefab, start, Quaternion.identity);
        LineRenderer lr = trail.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        float trailLifetime = (weaponType == WeaponType.Sniper) ? 0.2f : 0.05f;
        Destroy(trail, trailLifetime);
    }

    bool ShouldDrawTrail()
    {
        return weaponType == WeaponType.SMG || weaponType == WeaponType.AR ||
               weaponType == WeaponType.DMR || weaponType == WeaponType.Sniper ||
               weaponType == WeaponType.SG;
    }

    void PlayMuzzleFlash()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();
    }

    void PlayWeaponSound()
    {
        switch (weaponType)
        {
            case WeaponType.SMG: audioSource.PlayOneShot(smgSound); break;
            case WeaponType.AR: audioSource.PlayOneShot(arSound); break;
            case WeaponType.DMR: audioSource.PlayOneShot(dmrSound); break;
            case WeaponType.Sniper: audioSource.PlayOneShot(sniperSound); break;
            case WeaponType.SG: audioSource.PlayOneShot(shotgunSound); break;
            case WeaponType.GL: audioSource.PlayOneShot(glSound); break;
        }
    }

    public void HandleReload()
    {
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo && !isReloading)
        {
            animator.SetTrigger("Reload");
            StartCoroutine(ReloadAfterDelay(2.667f));
        }
    }

    IEnumerator ReloadAfterDelay(float delay)
    {
        isReloading = true;
        yield return new WaitForSeconds(delay);
        currentAmmo = maxAmmo;
        isReloading = false;
        UpdateAmmoUI();
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = currentAmmo + " / " + maxAmmo;
    }

    public bool IsReloading() => isReloading;

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
        playerCamera.fieldOfView = defaultFOV;
        isScoped = false;
    }

    IEnumerator OnScoped()
    {
        yield return new WaitForSeconds(0.1f);
        scopeOverlay?.SetActive(true);
        crosshair?.SetActive(false);
        playerCamera.fieldOfView = scopedFOV;
        isScoped = true;

        cameraHolder.position = Vector3.Lerp(cameraHolder.position, aimCamPos.position, Time.deltaTime * camTransitionSpeed);
        cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, aimCamPos.rotation, Time.deltaTime * camTransitionSpeed);
    }

    void OnUnscoped()
    {
        StopAllCoroutines();
        scopeOverlay?.SetActive(false);
        crosshair?.SetActive(true);
        playerCamera.fieldOfView = defaultFOV;
        isScoped = false;

        StartCoroutine(SmoothUnscope());
    }

    IEnumerator SmoothUnscope()
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
}
