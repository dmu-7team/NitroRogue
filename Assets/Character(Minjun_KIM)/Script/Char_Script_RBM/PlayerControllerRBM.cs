using Mirror;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerMovementRBM))]
[RequireComponent(typeof(WeaponSystemRBM))]
public class PlayerControllerRBM : NetworkBehaviour
{
    private PlayerMovementRBM movement;
    private WeaponSystemRBM weaponSystem;

    public override void OnStartAuthority()
    {
        if (!isLocalPlayer) return;

        movement = GetComponent<PlayerMovementRBM>();
        weaponSystem = GetComponent<WeaponSystemRBM>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        movement.HandleLook();
        //weaponSystem.HandleFire();
        weaponSystem.HandleReload();
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        movement.HandleMove();
    }

    [Command]
    public void CmdDealDamage(GameObject enemyObj, float damage)
    {
        if (enemyObj == null)
        {
            Debug.LogWarning("[CMD] Enemy object is null.");
            return;
        }

        EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            Debug.Log($"[CMD] Enemy confirmed: {enemy.name}");
            enemy.TakeDamage(damage, gameObject);
        }
        else
        {
            Debug.LogWarning("[CMD] EnemyBase component not found.");
        }
    }

    [Command]
    public void CmdSpawnTrail(Vector3 start, Vector3 end)
    {
        WeaponSystemRBM weapon = GetComponent<WeaponSystemRBM>();
        if (weapon == null || weapon.bulletTrailPrefab == null)
        {
            Debug.LogError("[CMD] WeaponSystemRBM or bulletTrailPrefab is missing.");
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
