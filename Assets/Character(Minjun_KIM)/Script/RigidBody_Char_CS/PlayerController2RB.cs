using UnityEngine;

[RequireComponent(typeof(PlayerMovementRB))]
[RequireComponent(typeof(WeaponSystemRB))]
public class PlayerController2RB : MonoBehaviour
{
    private PlayerMovementRB movement;
    private WeaponSystemRB weaponSystemrb;

    void Start()
    {
        movement = GetComponent<PlayerMovementRB>();
        weaponSystemrb = GetComponent<WeaponSystemRB>();
    }

    void Update()
    {
        weaponSystemrb.HandleFire();     // 사격
        weaponSystemrb.HandleReload();   // 재장전
    }
}
