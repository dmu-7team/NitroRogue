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
        audioSource.playOnAwake = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // ★ UI 모달 아닐 때만 잠금
        if (!(UIManager.Instance && UIManager.Instance.IsModalUIMode))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleLook();
        HandleMove();
        // ★ UI 모달(승/패 패널)일 땐 입력/시점 완전 차단
        if (UIManager.Instance && UIManager.Instance.IsModalUIMode)
            return;
    }

    public void HandleLook()
    {
        // ★ 이중 안전장치 (혹시 Update에서 가드 빠져도 보호)
        if (UIManager.Instance && UIManager.Instance.IsModalUIMode) return;
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    public void HandleMove()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = transform.right * x + transform.forward * z;
        bool isMoving = moveDirection.magnitude > 0.1f;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && z > 0f && isMoving && !weaponSystem.IsReloading();
        float currentSpeed = isSprinting ? stats.MoveSpeed * 1.5f : stats.MoveSpeed;

        Vector3 velocity = moveDirection.normalized * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // 로컬 애니메이션 적용 + 서버 동기화
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
            animator?.SetTrigger("jump"); // 로컬용
            CmdPlayJumpAnim(); // 동기화용
        }
    }

    private void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }

    // ==============================
    // 애니메이션 동기화 (Move / Jump)
    // ==============================

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
        if (isLocalPlayer) return; // 로컬은 이미 실행했음
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
