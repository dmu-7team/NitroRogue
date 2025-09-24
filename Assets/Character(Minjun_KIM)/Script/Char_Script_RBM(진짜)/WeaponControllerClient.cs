using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 단일 무기 컨트롤러(클라 처리):
/// - 입력/조준/발사/장전은 클라에서 처리
/// - 데미지 적용만 서버에 Command로 보고
/// - UI는 이벤트만 발행(직접 참조 없음)
/// </summary>
public class WeaponControllerClient : NetworkBehaviour
{
    [Header("Config & Refs")]
    [SerializeField] private WeaponConfig config;
    [SerializeField] private Transform    muzzle;
    [SerializeField] private Camera       playerCamera;
    [SerializeField] private Transform    cameraHolder, defaultCamPos, aimCamPos;
    [SerializeField] private float        camTransitionSpeed = 5f;

    [Header("VFX/SFX/Anim")]
    [SerializeField] private ParticleSystem muzzleFx;
    [SerializeField] private GameObject     bulletTrailPrefab; // LineRenderer 포함
    [SerializeField] private AudioSource    audioSource;
    [SerializeField] private Animator       animator;

    // === UI 이벤트 ===
    public event System.Action<int, int> AmmoChanged;      // (cur, max)
    public event System.Action<bool>     ScopedChanged;    // true: 스코프 켜짐
    public event System.Action<int>      WeaponChanged;    // weaponId
    public event System.Action<bool>     ReloadingChanged; // true: 장전 중

    // === 런타임 상태 ===
    private int         curAmmo;
    private bool        isReloading;
    private bool        isScoped;
    private float       defaultFOV;
    private PlayerStats stats;

    // 발사 스케줄링
    private float nextFireTime = 0f; // 다음 발사 가능 시각
    private int burstRemaining = 0;  // 남은 버스트 탄수

    public WeaponConfig Config => config;
    public int CurrentAmmo => curAmmo;
    public int MagSize => config ? config.magSize : 0;
    float FireInterval => 60f / Mathf.Max(1, config ? config.rpm : 600);

    void Start()
    {
        if (!isLocalPlayer) return;

        stats = GetComponent<PlayerStats>();
        defaultFOV = playerCamera ? playerCamera.fieldOfView : 60f;

        Equip(config);
        SetScoped(false);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleFireInput();
        HandleReloadInput();
    }
    void LateUpdate()
    {
        if (!isLocalPlayer) return;
        TickAim(Input.GetMouseButton(1));
    }

    // ───────── 장착/교체 ─────────
    public void Equip(WeaponConfig newConfig)
    {
        config = newConfig;
        if (!config) return;

        curAmmo = Mathf.Clamp(curAmmo, 0, config.magSize);
        if (curAmmo == 0) curAmmo = config.magSize;

        WeaponChanged?.Invoke(config.id);
        AmmoChanged?.Invoke(curAmmo, config.magSize);
    }

    // ───────── 발사 입력(스케줄러) ─────────
    bool CanFire()
    {
        if (!config) return false;
        if (isReloading) return false;
        if (curAmmo <= 0) return false;
        if (animator && animator.GetBool("isSprinting")) return false;
        if (Time.time < nextFireTime) return false;
        return true;
    }

    void HandleFireInput()
    {
        // 진행 중 버스트 우선
        if (burstRemaining > 0 && Time.time >= nextFireTime)
        {
            DoFireOnce();
            burstRemaining--;
            nextFireTime = (burstRemaining > 0)
                ? Time.time + Mathf.Max(0.01f, config.burstDelay)
                : Time.time + FireInterval;
            return;
        }

        if (!CanFire()) return;

        bool fireHeld = Input.GetMouseButton(0);
        bool fireDown = Input.GetMouseButtonDown(0);

        switch (config.fireMode)
        {
            case FireMode.Auto:    if (fireHeld) { DoFireOnce(); nextFireTime = Time.time + FireInterval; } break;
            case FireMode.Semi:    if (fireDown) { DoFireOnce(); nextFireTime = Time.time + FireInterval; } break;
            case FireMode.Shotgun: if (fireDown) { DoFireShotgun(); nextFireTime = Time.time + FireInterval; } break;
            case FireMode.Burst: 
                if (fireDown)
                {
                    // 발사할 총알 수 정함
                    burstRemaining = Mathf.Min(config.burstCount, curAmmo);
                    if (burstRemaining <= 0) return;

                    // 첫 발 즉시 발사
                    DoFireOnce();
                    burstRemaining--;

                    //버스트 탄이 남으면 버스트딜레이, 아니면 RPM 딜레이
                    nextFireTime = (burstRemaining > 0)
                        ? Time.time + Mathf.Max(0.01f, config.burstDelay)
                        : Time.time + FireInterval;
                } 
                break;
        }
    }

    // ───────── 실제 발사 실행부 ─────────
    void DoFireOnce()
    {
        if (curAmmo <= 0 || isReloading || !config) return;

        curAmmo = Mathf.Max(0, curAmmo - 1);
        AmmoChanged?.Invoke(curAmmo, config.magSize);

        PlayShootAnim();
        PlayFxAndRaycast(single: true);
    }

    void DoFireShotgun()
    {
        if (curAmmo <= 0 || isReloading || !config) return;

        curAmmo = Mathf.Max(0, curAmmo - 1);
        AmmoChanged?.Invoke(curAmmo, config.magSize);

        PlayShootAnim();
        PlayFxAndRaycast(single: false);
    }

    void PlayShootAnim()
    {
        if (!animator || !config) return;

        // 간단 매핑: SMG=3, AR=2, 그 외=1
        float mode = (config.type == WeaponType.SMG) ? 3f :
                     (config.type == WeaponType.AR) ? 2f : 1f;

        animator.SetFloat("shootMode", mode);
        animator.SetTrigger("shoot");

        CmdReportShootAnim(mode);
    }

    [Command] void CmdReportShootAnim(float mode) => RpcPlayShootAnim(mode);

    [ClientRpc]
    void RpcPlayShootAnim(float mode)
    {
        if (isLocalPlayer) return;
        if (!animator) return;
        animator.SetFloat("shootMode", mode);
        animator.SetTrigger("shoot");
    }

    void PlayFxAndRaycast(bool single)
    {
        muzzleFx?.Play();
        if (audioSource && config.fireClip) audioSource.PlayOneShot(config.fireClip);
        if (!playerCamera || !muzzle) return;

        if (config.fireMode == FireMode.Shotgun && !single)
        {
            var ends = new List<Vector3>(config.pelletCount);

            for (int i = 0; i < config.pelletCount; i++)
            {
                Vector3 dir = Spread(playerCamera.transform.forward, config.spreadDeg);
                Vector3 end = ShootRay(playerCamera.transform.position, dir);
                DrawTrail(muzzle.position, end);
                ends.Add(end);
            }

            if (ends.Count > 0) CmdReportShotgunFX(ends.ToArray());
        }
        else
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Vector3 end = ShootRay(ray.origin, ray.direction);
            DrawTrail(muzzle.position, end);
            CmdReportShotFX(end);
        }
    }

    Vector3 ShootRay(Vector3 origin, Vector3 dir)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, 100f))
        {
            TryDealDamage(hit);
            return hit.point;
        }
        return muzzle ? muzzle.position + dir * 100f : origin + dir * 100f;
    }

    void TryDealDamage(in RaycastHit hit)
    {
        var enemy = hit.collider.GetComponentInParent<EnemyBase>();
        if (!enemy) return;

        float dmg = stats ? stats.AttackDamage : 1f;
        CmdDealDamage(enemy.gameObject, dmg);
    }

    [Command]
    void CmdDealDamage(GameObject target, float damage)
    {
        if (target && target.TryGetComponent(out EnemyBase enemy))
            enemy.TakeDamage(damage, gameObject);
    }

    [Command] void CmdReportShotFX(Vector3 end) => RpcPlayShotFX(end);
    [Command] void CmdReportShotgunFX(Vector3[] ends) => RpcPlayShotgunFX(ends);

    [ClientRpc]
    void RpcPlayShotFX(Vector3 end)
    {
        if (isLocalPlayer) return;
        muzzleFx?.Play();
        if (audioSource && config && config.fireClip) audioSource.PlayOneShot(config.fireClip);
        if (bulletTrailPrefab && muzzle) DrawTrail(muzzle.position, end);
    }

    [ClientRpc]
    void RpcPlayShotgunFX(Vector3[] ends)
    {
        if (isLocalPlayer) return;
        muzzleFx?.Play();
        if (audioSource && config && config.fireClip) audioSource.PlayOneShot(config.fireClip);
        if (bulletTrailPrefab && muzzle && ends != null)
        {
            for (int i = 0; i < ends.Length; i++)
                DrawTrail(muzzle.position, ends[i]);
        }
    }

    // ───────── 조준/스코프 ─────────
    public void TickAim(bool isAiming)
    {
        if (!config) return;

        Transform target = isAiming ? aimCamPos : defaultCamPos;
        if (cameraHolder && target)
        {
            cameraHolder.position = Vector3.Lerp(cameraHolder.position, target.position, Time.deltaTime * camTransitionSpeed);

            //cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, target.rotation, Time.deltaTime * camTransitionSpeed);

            cameraHolder.rotation = playerCamera.transform.rotation;
        }

        if (config.aimMode == AimMode.Zoom)
        {
            SetScoped(false);
        }
        else
        {
            if (isAiming && !isScoped) StartCoroutine(CoScopedOn());
            else if (!isAiming && isScoped) SetScoped(false);
        }
    }

    IEnumerator CoScopedOn()
    {
        yield return new WaitForSeconds(0.1f);
        SetScoped(true);
    }

    void SetScoped(bool on)
    {
        isScoped = on;
        ScopedChanged?.Invoke(on);
        if (playerCamera) playerCamera.fieldOfView = on ? config.scopedFOV : defaultFOV;
    }

    // ───────── 장전 ─────────
    void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R)) TryReload();
    }

    public void TryReload()
    {
        if (isReloading || !config) return;
        if (curAmmo >= config.magSize) return;

        ReloadingChanged?.Invoke(true);
        if (audioSource && config.reloadClip) audioSource.PlayOneShot(config.reloadClip);

        animator?.SetTrigger("reload");
        CmdReportReloadAnim();

        StartCoroutine(CoReload(config.reloadTime));
    }

    [Command] void CmdReportReloadAnim() => RpcPlayReloadAnim();
    [ClientRpc]
    void RpcPlayReloadAnim()
    {
        if (isLocalPlayer) return;
        animator?.SetTrigger("reload");
    }

    IEnumerator CoReload(float delay)
    {
        isReloading = true;
        yield return new WaitForSeconds(delay);
        curAmmo = config.magSize;
        isReloading = false;

        AmmoChanged?.Invoke(curAmmo, config.magSize);
        ReloadingChanged?.Invoke(false);
    }

    // ───────── 비주얼 헬퍼 ─────────
    void DrawTrail(Vector3 start, Vector3 end)
    {
        if (!bulletTrailPrefab) return;
        var trail = Instantiate(bulletTrailPrefab, start, Quaternion.identity);
        if (trail.TryGetComponent(out LineRenderer lr))
        {
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }
        Destroy(trail, 0.05f);
    }

    Vector3 Spread(Vector3 dir, float deg)
    {
        float yaw = Random.Range(-deg, deg);
        float pitch = Random.Range(-deg, deg);
        return Quaternion.Euler(pitch, yaw, 0) * dir;
    }

    // ─────────웨폰 정보 ──────────
    public void EmitAll()
    {
        AmmoChanged?.Invoke(curAmmo, config.magSize);
        ScopedChanged?.Invoke(isScoped);
        WeaponChanged?.Invoke(config.id);
        ReloadingChanged?.Invoke(isReloading);
    }
}
