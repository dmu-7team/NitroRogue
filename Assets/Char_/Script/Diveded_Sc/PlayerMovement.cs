using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : PlayerStats
{
    [Header("Movement")]
    public CharacterController controller;
    public Transform cameraHolder;
    public float runSpeed = 8f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 100f;

    [Header("Animator")]
    public Animator animator;

    private float xRotation;
    private Vector3 velocity;
    private WeaponSystem weaponSystem;

    public override void Start()
    {
        base.Start();
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

        bool isRunning = Input.GetKey(KeyCode.LeftShift) && z > 0f && isMoving && !weaponSystem.IsReloading();
        float currentSpeed = isRunning ? runSpeed : moveSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) && !weaponSystem.IsReloading())
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("ClickSpacebar");
        }

        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * velocity.y * Time.deltaTime);

        animator.SetFloat("moveX", x);
        animator.SetFloat("moveZ", z);
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isRunning", isRunning);
    }
}
