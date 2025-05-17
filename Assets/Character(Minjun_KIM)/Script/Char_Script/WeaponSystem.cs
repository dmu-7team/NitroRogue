using UnityEngine;
using System.Collections;
using Mirror;

public class WeaponSystem : MonoBehaviour
{
    [Header("Weapon Settings")]
    public Transform muzzle;
    public GameObject bulletTrailPrefab;
    public int maxAmmo = 30;
    public float bulletForce = 100f;
    public float weaponDamage = 1;

    [Header("Camera & Aiming")]
    public Camera playerCamera;
    public Transform cameraHolder;
    public Transform defaultCamPos;
    public Transform aimCamPos;
    public float camTransitionSpeed = 5f;
    public float scopedFOV = 30f;

    [Header("Animator & FX")]
    public Animator animator;
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;

    private int currentAmmo;
    private bool isReloading = false;
    private bool isScoped = false;
    private float defaultFOV;

    void Start()
    {
        currentAmmo = maxAmmo;

        if (playerCamera != null)
            defaultFOV = playerCamera.fieldOfView;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                Debug.LogWarning("[WeaponSystem] AudioSource가 없습니다. 사운드는 재생되지 않습니다.");
        }
    }

    public void HandleFire()
    {
        if (!Input.GetMouseButtonDown(0) || animator.GetBool("isRunning") || isReloading || currentAmmo <= 0)
            return;

        currentAmmo--;
        animator.SetTrigger("Shoot");

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 direction = ray.direction;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var pc = NetworkClient.localPlayer.GetComponent<PlayerController2>();

            // 몬스터에 맞았을 경우에만 데미지 적용
            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
            if (enemy != null)
            {
                pc?.CmdDealDamage(enemy.gameObject, weaponDamage);
            }

            pc?.CmdSpawnTrail(muzzle.position, hit.point);
        }
        else
        {
            var pc = NetworkClient.localPlayer.GetComponent<PlayerController2>();
            pc?.CmdSpawnTrail(muzzle.position, muzzle.position + direction * 100f);
        }

        muzzleFlash?.Play();

        if (audioSource != null)
            audioSource.Play();
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
    }

    public bool IsReloading()
    {
        return isReloading;
    }

    public void HandleAim()
    {
        bool isAiming = Input.GetMouseButton(1);
        Transform target = isAiming ? aimCamPos : defaultCamPos;

        cameraHolder.position = Vector3.Lerp(cameraHolder.position, target.position, Time.deltaTime * camTransitionSpeed);
        cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, target.rotation, Time.deltaTime * camTransitionSpeed);

        if (playerCamera != null)
            playerCamera.fieldOfView = isAiming ? scopedFOV : defaultFOV;

        isScoped = isAiming;
    }
}
