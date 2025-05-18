using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(WeaponSystem))]
public class PlayerController2 : NetworkBehaviour
{
    private PlayerMovement movement;
    private WeaponSystem weaponSystem;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        movement = GetComponent<PlayerMovement>();
        weaponSystem = GetComponent<WeaponSystem>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isOwned) return;
        movement.HandleMove();               // �̵�
        weaponSystem.HandleFire();           // ���
        weaponSystem.HandleReload();         // ������
    }

    void LateUpdate()
    {
        if (!isOwned) return;
        movement.HandleLook();               // ���� ȸ�� (�ִϸ��̼� ���� ó�� �� ī�޶� ��鸲 ����)
    }
}
