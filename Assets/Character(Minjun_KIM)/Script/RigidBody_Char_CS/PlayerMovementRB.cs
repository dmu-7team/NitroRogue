using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementRB : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpForce = 5f;
    public LayerMask groundMask;
    public Transform groundCheck;
    public float groundDistance = 0.3f;

    [Header("Mouse Look")]
    public Transform cameraHolder;
    public float mouseSensitivity = 100f;

    [Header("Animator")]
    public Animator animator;

    private Rigidbody rb;
    private float xRotation = 0f;
    private bool isGrounded;
    private WeaponSystemRB weaponSystemrb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        weaponSystemrb = GetComponent<WeaponSystemRB>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleJump();
        HandleGroundCheck();

    }

    void FixedUpdate()
    {
        HandleMove();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMove()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = transform.right * x + transform.forward * z;
        bool isMoving = moveDirection.magnitude > 0.1f;

        // ✅ 기존과 동일한 달리기 조건
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && z > 0f && isMoving && !weaponSystemrb.IsReloading();
        float currentSpeed = isRunning ? runSpeed : moveSpeed;

        Vector3 velocity = moveDirection.normalized * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // 애니메이션 파라미터
        animator?.SetFloat("moveX", x);
        animator?.SetFloat("moveZ", z);
        animator?.SetBool("isMoving", isMoving);
        animator?.SetBool("isRunning", isRunning);
    }

    void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !weaponSystemrb.IsReloading())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator?.SetTrigger("ClickSpacebar");
        }
    }

    void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }
}
