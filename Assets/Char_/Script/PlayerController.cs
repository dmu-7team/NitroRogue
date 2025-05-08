using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public CharacterController controller;
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;
    private Vector3 velocity;

    [Header("Camera")]
    public Transform cameraHolder;
    public Camera playerCamera;
    public float mouseSensitivity = 100f;
    private float xRotation = 0f;

    [Header("Shooting")]
    public Transform muzzle;
    public GameObject bulletPrefab;
    public float bulletForce = 20f;
    public int currentAmmo = 30;
    public int maxAmmo = 30;

    [Header("UI")]
    public Text ammoText;
    public Image crosshair;

    [Header("Animation")]
    public Animator animator;

    private bool isReloading = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UpdateAmmoUI();
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleShooting();
        HandleReloadInput();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * moveSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) && !isReloading)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("ClickSpacebar");
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * velocity.y * Time.deltaTime);

        bool isMoving = move.magnitude > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving;

        animator.SetFloat("moveX", x);
        animator.SetFloat("moveZ", z);
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isRunning", isRunning);
    }

    void HandleShooting()
    {
        if (Input.GetMouseButtonDown(0) && !animator.GetBool("isRunning") && currentAmmo > 0 && !isReloading)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 fireDirection;

            if (Physics.Raycast(ray, out RaycastHit hitInfo, 100f))
                fireDirection = (hitInfo.point - muzzle.position).normalized;
            else
                fireDirection = ray.direction;

            Quaternion rot = Quaternion.LookRotation(fireDirection);
            GameObject bullet = Instantiate(bulletPrefab, muzzle.position, rot);

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = fireDirection * bulletForce;

            animator.SetTrigger("Shoot");
            currentAmmo--;
            UpdateAmmoUI();
        }
    }

    void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo && !isReloading)
        {
            animator.SetTrigger("Reload");
            StartCoroutine(ReloadAfterDelay(2.667f)); // 애니메이션 길이와 동일
        }
    }

    IEnumerator ReloadAfterDelay(float delay)
    {
        isReloading = true;
        Debug.Log("🔄 재장전 시작 (딜레이: " + delay + "s)");
        yield return new WaitForSeconds(delay);
        currentAmmo = maxAmmo;
        isReloading = false;
        UpdateAmmoUI();
        Debug.Log("✅ 재장전 완료");
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = currentAmmo + " / " + maxAmmo;
    }
}
