using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementRBM : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpForce = 5f;
    public LayerMask groundMask;
    public Transform groundCheck;
    public float groundDistance = 1f;

    [Header("Mouse Look")]
    public Transform cameraHolder;
    public float mouseSensitivity = 100f;

    [Header("Animator")]
    public Animator animator;

    private Rigidbody rb;
    private float xRotation = 0f;
    private float yaw = 0f;    // ← 새로 추가: 좌우 누적
    private bool isGrounded;
    private WeaponSystemRBM weaponSystem;
    private PlayerStats stats;

    [Header("Sound")]
    public AudioClip[] footstepClips;
    [SerializeField] private float walkFootstepInterval = 0.65f;
    [SerializeField] private float runFootstepInterval = 0.37f;

    private AudioSource audioSource;
    private float footstepTimer = 0f;

    void Start()
    {
        if (!isLocalPlayer) return;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        weaponSystem = GetComponent<WeaponSystemRBM>();
        stats = GetComponent<PlayerStats>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null) audioSource.playOnAwake = false;

        // 현재 Y 각도부터 시작하도록 초기화
        yaw = transform.eulerAngles.y;

        // 커서 잠금
        if (!(UIManager.Instance && UIManager.Instance.IsModalUIMode))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // 🔒 모달(승/패 패널 등)일 때 입력/시점 완전 차단
        if (UIManager.Instance && UIManager.Instance.IsModalUIMode)
            return;

        HandleLook();
        HandleMove();
    }

    public void HandleLook()
    {
        // (2중 가드 — 안전)
        if (UIManager.Instance && UIManager.Instance.IsModalUIMode) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 상하 회전: 카메라 pitch
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 좌우 회전: 본체 yaw
        yaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // 디버깅 원하면:
        // Debug.Log($"mx={mouseX:F3}, yaw={yaw:F1}, nowY={transform.eulerAngles.y:F1}");
    }

    public void HandleMove()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = transform.right * x + transform.forward * z;
        bool isMoving = moveDirection.magnitude > 0.1f;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift)
                           && z > 0f
                           && isMoving
                           && !weaponSystem.IsReloading();

        float currentSpeed = isSprinting
            ? stats.MoveSpeed * 1.5f
            : stats.MoveSpeed;

        Vector3 velocity = moveDirection.normalized * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // 애니 동기화
        SetMoveAnim(isMoving, isSprinting);

        animator?.SetFloat("moveX", x);
        animator?.SetFloat("moveZ", z);

        if (isMoving && isGrounded)
        {
            footstepTimer -= Time.deltaTime;
            float interval = isSprinting ? runFootstepInterval : walkFootstepInterval;

            if (footstepTimer <= 0f)
            {
                PlayFootstepSound();
                footstepTimer = interval;
            }
        }
        else
        {
            footstepTimer = 0f;
        }

        HandleJump();
        HandleGroundCheck();
    }

    private void PlayFootstepSound()
    {
        if (footstepClips.Length == 0 || audioSource == null) return;
        int index = Random.Range(0, footstepClips.Length);
        audioSource.PlayOneShot(footstepClips[index]);
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !weaponSystem.IsReloading())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator?.SetTrigger("jump"); // 로컬 애니
            CmdPlayJumpAnim();            // 네트워크 브로드캐스트
        }
    }

    private void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }

    // 애니메이션 동기화
    void SetMoveAnim(bool isMoving, bool isSprinting)
    {
        animator?.SetBool("isMoving", isMoving);
        animator?.SetBool("isSprinting", isSprinting);

        CmdSetMoveAnim(isMoving, isSprinting);
    }

    [Command]
    void CmdSetMoveAnim(bool isMoving, bool isSprinting)
    {
        RpcSetMoveAnim(isMoving, isSprinting);
    }

    [ClientRpc]
    void RpcSetMoveAnim(bool isMoving, bool isSprinting)
    {
        if (isLocalPlayer) return; // 로컬은 이미 처리함
        animator?.SetBool("isMoving", isMoving);
        animator?.SetBool("isSprinting", isSprinting);
    }

    [Command]
    void CmdPlayJumpAnim()
    {
        RpcPlayJumpAnim();
    }

    [ClientRpc]
    void RpcPlayJumpAnim()
    {
        if (isLocalPlayer) return;
        animator?.SetTrigger("jump");
    }
}
