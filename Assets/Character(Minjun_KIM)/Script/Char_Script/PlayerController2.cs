using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(WeaponSystem))]
public class PlayerController2 : MonoBehaviour
{
    private PlayerMovement movement;
    private WeaponSystem weaponSystem;

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        weaponSystem = GetComponent<WeaponSystem>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        movement.HandleMove();               // 이동
        weaponSystem.HandleFire();           // 사격
        weaponSystem.HandleReload();         // 재장전
    }

    void LateUpdate()
    {
        movement.HandleLook();               // 시점 회전 (애니메이션 이후 처리 → 카메라 흔들림 방지)
    }
}
