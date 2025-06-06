using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Mirror;

public class WeaponSystemRBM : NetworkBehaviour
{
    public enum WeaponType { DMR, SMG, AR, SG } // GL, Sniper 제거

    [Header("Weapon Settings")]
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private Transform muzzle;
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
    [HideInInspector] private AimMode aimMode = AimMode.Zoom;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip smgSound;
    [SerializeField] private AudioClip arSound;
    [SerializeField] private AudioClip dmrSound;
    [SerializeField] private AudioClip shotgunSound;

    [Header("Trail Effect")]
    [SerializeField] public GameObject bulletTrailPrefab;

    private bool isReloading = false;
    private bool isScoped = false;
    private float defaultFOV;
    private PlayerStats stats;

    private float lastFireTime = 0f;
    private float smgFireInterval = 60f / 800f;
    private Coroutine burstCoroutine;

    void Start()
    {
        if (!isLocalPlayer) return;
        stats = GetComponent<PlayerStats>();
        UpdateAmmoUI();
        defaultFOV = playerCamera.fieldOfView;
        scopeOverlay?.SetActive(false);
        crosshair?.SetActive(true);

        // 무기별 조준 모드 고정
        if (weaponType == WeaponType.DMR)
            aimMode = AimMode.Scope;
        else
            aimMode = AimMode.Zoom;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleAim();

        if (Input.GetMouseButton(0) && !animator.GetBool("isRunning") && !isReloading && currentAmmo > 0)
        {
            if (weaponType == WeaponType.SMG)
                TryFireSMG();
        }

        if (Input.GetMouseButtonDown(0) && !animator.GetBool("isRunning") && !isReloading && currentAmmo > 0)
        {
            if (weaponType == WeaponType.AR)
                TryFireBurst();
            else if (weaponType != WeaponType.SMG)
                FireSingleShot();
        }
    }

    public void HandleFire()
    {
        if (!isLocalPlayer || animator.GetBool("isRunning") || isReloading || currentAmmo <= 0)
            return;

        switch (weaponType)
        {
            case WeaponType.SMG:
                if (Input.GetMouseButton(0) && Time.time - lastFireTime >= smgFireInterval)
                {
                    lastFireTime = Time.time;
                    FireSingleShot();
                }
                break;
            case WeaponType.AR:
                if (Input.GetMouseButtonDown(0) && burstCoroutine == null)
                    burstCoroutine = StartCoroutine(FireBurst());
                break;
            default:
                if (Input.GetMouseButtonDown(0))
                    FireSingleShot();
                break;
        }
    }

    void TryFireSMG()
    {
        if (Time.time - lastFireTime >= smgFireInterval)
        {
            lastFireTime = Time.time;
            FireSingleShot();
        }
    }

    void TryFireBurst()
    {
        if (burstCoroutine == null)
            burstCoroutine = StartCoroutine(FireBurst());
    }

    IEnumerator FireBurst()
    {
        int shots = Mathf.Min(3, currentAmmo);
        for (int i = 0; i < shots; i++)
        {
            FireSingleShot();
            yield return new WaitForSeconds(0.1f);
        }
        burstCoroutine = null;
    }

    void FireSingleShot()
    {
        currentAmmo--;
        UpdateAmmoUI();

        switch (weaponType)
        {
            case WeaponType.SMG: animator.SetTrigger("AutoShoot"); break;
            case WeaponType.AR: animator.SetTrigger("BurstShoot"); break;
            default: animator.SetTrigger("Shoot"); break;
        }

        FireBulletBasedOnType();
    }

    void FireBulletBasedOnType()
    {
        PlayMuzzleFlash();
        PlayWeaponSound();

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint = ray.origin + ray.direction * 100f;
        Vector3 fireDirection = (targetPoint - muzzle.position).normalized;

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
                    EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                    if (enemy != null)
                    {
                        float finalDamage = stats != null ? stats.AttackDamage : 1f;
                        CmdDealDamage(enemy.gameObject, finalDamage);
                    }
                    if (ShouldDrawTrail())
                        CreateBulletTrail(muzzle.position, hit.point);
                }
                else if (ShouldDrawTrail())
                {
                    CreateBulletTrail(muzzle.position, muzzle.position + spreadDir * 100f);
                }
            }
        }
        else
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    float finalDamage = stats != null ? stats.AttackDamage : 1f;
                    CmdDealDamage(enemy.gameObject, finalDamage);
                }
                if (ShouldDrawTrail())
                    CreateBulletTrail(muzzle.position, hit.point);
            }
            else if (ShouldDrawTrail())
            {
                CreateBulletTrail(muzzle.position, muzzle.position + fireDirection * 100f);
            }
        }
    }

    [Command]
    void CmdDealDamage(GameObject target, float damage)
    {
        if (target.TryGetComponent(out EnemyBase enemy))
            enemy.TakeDamage(damage, gameObject);
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

        Destroy(trail, 0.05f);
    }

    bool ShouldDrawTrail()
    {
        return true;
    }

    void PlayMuzzleFlash()
    {
        muzzleFlash?.Play();
    }

    void PlayWeaponSound()
    {
        switch (weaponType)
        {
            case WeaponType.SMG: audioSource.PlayOneShot(smgSound); break;
            case WeaponType.AR: audioSource.PlayOneShot(arSound); break;
            case WeaponType.DMR: audioSource.PlayOneShot(dmrSound); break;
            case WeaponType.SG: audioSource.PlayOneShot(shotgunSound); break;
        }
    }

    public void HandleReload()
    {
        if (!isLocalPlayer) return;

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

        if (aimMode == AimMode.Zoom)
        {
            Transform target = isAiming ? aimCamPos : defaultCamPos;
            cameraHolder.position = Vector3.Lerp(cameraHolder.position, target.position, Time.deltaTime * camTransitionSpeed);
            cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, target.rotation, Time.deltaTime * camTransitionSpeed);

            crosshair?.SetActive(true);
            scopeOverlay?.SetActive(false);
            playerCamera.fieldOfView = defaultFOV;
            isScoped = false;
        }
        else
        {
            if (isAiming && !isScoped)
                StartCoroutine(OnScoped());
            else if (!isAiming && isScoped)
                OnUnscoped();
        }
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
