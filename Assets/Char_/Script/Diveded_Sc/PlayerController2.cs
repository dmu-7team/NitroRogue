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
        movement.HandleMove();               // �̵�
        weaponSystem.HandleFire();           // ���
        weaponSystem.HandleReload();         // ������
    }

    void LateUpdate()
    {
        movement.HandleLook();               // ���� ȸ�� (�ִϸ��̼� ���� ó�� �� ī�޶� ��鸲 ����)
    }
}
