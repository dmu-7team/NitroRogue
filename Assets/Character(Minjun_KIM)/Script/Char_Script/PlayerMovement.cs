using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] public CharacterController controller;
    [SerializeField] public Transform cameraHolder;
    [SerializeField] public float moveSpeed = 5f;
    [SerializeField] public float runSpeed = 8f;
    [SerializeField] public float gravity = -9.81f;
    [SerializeField] public float jumpHeight = 1.2f;

    [Header("Mouse Look")]
    [SerializeField] public float mouseSensitivity = 100f;

    [Header("Animator")]
    [SerializeField] public Animator animator;

    private float xRotation;
    private Vector3 velocity;
    private WeaponSystem weaponSystem;

    void Start()
    {
        weaponSystem = GetComponent<WeaponSystem>();
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

        Vector3 move = transform.right * x + transform.forward * z;
        bool isMoving = move.magnitude > 0.1f;

        // ✅ 재장전 중일 땐 달리기 안 됨
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && z > 0f && isMoving && !weaponSystem.IsReloading();
        float currentSpeed = isRunning ? runSpeed : moveSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);

        // 점프 (재장전 중엔 불가)
        if (Input.GetKeyDown(KeyCode.Space) && !weaponSystem.IsReloading())
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("ClickSpacebar");
        }

        // 중력 적용
        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * velocity.y * Time.deltaTime);

        // 애니메이션 파라미터 설정
        animator.SetFloat("moveX", x);
        animator.SetFloat("moveZ", z);
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isRunning", isRunning);
    }
}
