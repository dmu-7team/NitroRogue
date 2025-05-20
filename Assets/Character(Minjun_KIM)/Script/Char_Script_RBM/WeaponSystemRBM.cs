using UnityEngine;
using Mirror;
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

    private float defaultFOV;

    void Start()
    {
        currentAmmo = maxAmmo;
        if (playerCamera != null) defaultFOV = playerCamera.fieldOfView;
    }

    void Update()
    {
        HandleAim();
    }

    public void HandleFire()
    {
        if (!Input.GetMouseButtonDown(0) || isReloading || currentAmmo <= 0 || animator.GetBool("isRunning"))
            return;

        currentAmmo--;
        animator.SetTrigger("Shoot");

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 fireDirection = ray.direction;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var pc = NetworkClient.localPlayer.GetComponent<PlayerControllerRBM>();

            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
            if (enemy != null)
            {
                pc?.CmdDealDamage(enemy.gameObject, weaponDamage);
            }

            pc?.CmdSpawnTrail(muzzle.position, hit.point);
        }
        else
        {
            var pc = NetworkClient.localPlayer.GetComponent<PlayerControllerRBM>();
            pc?.CmdSpawnTrail(muzzle.position, muzzle.position + fireDirection * 100f);
        }

        muzzleFlash?.Play();
        audioSource?.Play();
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

    private void HandleAim()
    {
        bool isAiming = Input.GetMouseButton(1);
        Transform target = isAiming ? aimCamPos : defaultCamPos;

        cameraHolder.position = Vector3.Lerp(cameraHolder.position, target.position, Time.deltaTime * camTransitionSpeed);
        cameraHolder.rotation = Quaternion.Lerp(cameraHolder.rotation, target.rotation, Time.deltaTime * camTransitionSpeed);

        if (playerCamera != null)
            playerCamera.fieldOfView = isAiming ? scopedFOV : defaultFOV;
    }
}
