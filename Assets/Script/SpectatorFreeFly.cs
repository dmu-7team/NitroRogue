using UnityEngine;

public class SpectatorFreeFly : MonoBehaviour
{
    [Header("마우스/이동")]
    public float lookSensitivity = 2f;
    public float moveSpeed = 6f;
    public float runMultiplier = 2f;   // Shift로 가속
    public float verticalSpeed = 6f;   // Space/Ctrl 위아래

    [Header("설정")]
    public bool lockCursor = false;
    public float pitchClamp = 85f;

    float yaw = 0f, pitch = 0f;

    void OnEnable()
    {
        // 현재 각도로 초기화
        var e = transform.rotation.eulerAngles;
        yaw = e.y; pitch = e.x;

        if (lockCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    void OnDisable()
    {
        if (lockCursor) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void Update()
    {
        // ----- 마우스 룩 -----
        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // ----- 이동 -----
        float h = Input.GetAxis("Horizontal"); // A/D
        float v = Input.GetAxis("Vertical");   // W/S
        float up = 0f;
        if (Input.GetKey(KeyCode.Space)) up += 1f;
        if (Input.GetKey(KeyCode.LeftControl)) up -= 1f;

        float spd = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? runMultiplier : 1f);

        Vector3 move =
            transform.right * h +
            transform.forward * v +
            Vector3.up * up * (verticalSpeed / Mathf.Max(1f, moveSpeed));

        transform.position += move * spd * Time.deltaTime;
    }
}
