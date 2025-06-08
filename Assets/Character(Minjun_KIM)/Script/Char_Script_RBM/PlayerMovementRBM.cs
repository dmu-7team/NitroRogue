using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementRBM : MonoBehaviour
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
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        weaponSystem = GetComponent<WeaponSystemRBM>();
        stats = GetComponent<PlayerStats>();

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void HandleLook()
    {
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

        bool isRunning = Input.GetKey(KeyCode.LeftShift) && z > 0f && isMoving && !weaponSystem.IsReloading();
        //float currentSpeed = isRunning ? runSpeed : moveSpeed;
        float currentSpeed = isRunning ? stats.MoveSpeed * 1.5f : stats.MoveSpeed;
        Vector3 velocity = moveDirection.normalized * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        animator?.SetFloat("moveX", x);
        animator?.SetFloat("moveZ", z);
        animator?.SetBool("isMoving", isMoving);
        animator?.SetBool("isRunning", isRunning);

        if (isMoving && isGrounded)
        {
            footstepTimer -= Time.deltaTime;
            float interval = isRunning ? runFootstepInterval : walkFootstepInterval;

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

        //일단 그냥 랜덤으로 해놨긴 했는데 향후 바닥에 따라 발소리 다르게 하는것도 좋을 것 같음
        Debug.Log("발소리");
        int index = Random.Range(0, footstepClips.Length);
        audioSource.PlayOneShot(footstepClips[index]);
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !weaponSystem.IsReloading())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator?.SetTrigger("ClickSpacebar");
        }
    }

    private void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }
}
