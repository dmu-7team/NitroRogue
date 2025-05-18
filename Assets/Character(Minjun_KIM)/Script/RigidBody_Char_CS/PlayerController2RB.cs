using UnityEngine;

[RequireComponent(typeof(PlayerMovementRB))]
[RequireComponent(typeof(WeaponSystem))]
public class PlayerController2RB : MonoBehaviour
{
    private PlayerMovementRB movement;
    private WeaponSystem weaponSystem;

    void Start()
    {
        movement = GetComponent<PlayerMovementRB>();
        weaponSystem = GetComponent<WeaponSystem>();
    }

    void Update()
    {
        weaponSystem.HandleFire();     // ���
        weaponSystem.HandleReload();   // ������
    }
}
