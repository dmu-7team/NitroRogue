using UnityEngine;
using Mirror;
using Unity.AppUI.UI;
using System.Collections;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(WeaponSystem))]
public class PlayerController2 : NetworkBehaviour
{
    private PlayerMovement movement;
    private WeaponSystem weaponSystem;

    void Start()
    {
        if (!isLocalPlayer) return;

        movement = GetComponent<PlayerMovement>();
        weaponSystem = GetComponent<WeaponSystem>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        movement.HandleMove();
        movement.HandleLook();
        weaponSystem.HandleFire();
        weaponSystem.HandleReload();
        weaponSystem.HandleAim();
    }

    //  Mirror 커맨드: 서버에 데미지 요청 (WeaponSystem에서 호출됨)
    [Command]
    public void CmdDealDamage(GameObject enemyObj, float damage)
    {
        if (enemyObj == null)
        {
            Debug.LogWarning("[CMD]  enemyObj is null");
            return;
        }

        EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            Debug.Log($"[CMD] EnemyBase 확인: {enemy.name}");
            enemy.TakeDamage(damage, gameObject);
        }
        else
        {
            Debug.LogWarning("[CMD]  EnemyBase 없음");
        }
    }

    [Command]
    public void CmdSpawnTrail(Vector3 start, Vector3 end)
    {
        // 서버에서 직접 bulletTrailPrefab을 가져와야 함
        WeaponSystem weapon = GetComponent<WeaponSystem>();
        if (weapon == null)
        {
            Debug.LogError("[CMD] WeaponSystem이 없습니다.");
            return;
        }

        GameObject trail = Instantiate(weapon.bulletTrailPrefab, start, Quaternion.identity);
        NetworkServer.Spawn(trail);

        var lr = trail.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        StartCoroutine(DestroyAfter(trail, 0.1f));
    }



    private IEnumerator DestroyAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        NetworkServer.Destroy(obj);
    }
}
