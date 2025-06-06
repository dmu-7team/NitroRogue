using Mirror;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerMovementRBM))]
[RequireComponent(typeof(WeaponSystemRBM))]
public class PlayerControllerRBM : NetworkBehaviour
{
    private PlayerMovementRBM movement;
    private WeaponSystemRBM weaponSystem;
    public GameObject cameraObject;

    public override void OnStartAuthority()
    {
        // 1. 카메라 충돌 방지 - 모든 FPSCam 끄고 내 것만 켜기
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.gameObject.name == "FPSCam")
                cam.gameObject.SetActive(false);
        }

        // 2. 내 컴포넌트 설정
        movement = GetComponent<PlayerMovementRBM>();
        weaponSystem = GetComponent<WeaponSystemRBM>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 3. 내 카메라만 활성화
        if (cameraObject != null)
        {
            cameraObject.SetActive(true);
            cameraObject.tag = "MainCamera";  // 내 것만 MainCamera 태그
            Debug.Log("[PlayerController] FPS 카메라 활성화 및 태그 설정 완료");
        }
        else
        {
            Debug.LogWarning("[PlayerController] cameraObject 연결되지 않음!");
        }

        // 4. UI 전환
        RoomUIManager.Instance?.SwitchToGameUI();
        Debug.Log("[PlayerController] 권한 있는 내 캐릭터로 전환됨: UI 및 카메라 설정 완료");
    }



    void Update()
    {
        if (!isLocalPlayer || movement == null || weaponSystem == null) return;

        movement.HandleMove();
        movement.HandleLook();
        weaponSystem.HandleFire();
        weaponSystem.HandleReload();
    }


    // [WeaponSystemRB]에서 호출되는 Mirror 명령
    [Command]
    public void CmdDealDamage(GameObject enemyObj, float damage)
    {
        if (enemyObj == null)
        {
            Debug.LogWarning("[CMD] enemyObj is null");
            return;
        }

        EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, gameObject);
        }
        else
        {
            Debug.LogWarning("[CMD] EnemyBase 컴포넌트를 찾을 수 없습니다.");
        }
    }

    [Command]
    public void CmdSpawnTrail(Vector3 start, Vector3 end)
    {
        if (weaponSystem == null || weaponSystem.bulletTrailPrefab == null)
        {
            Debug.LogWarning("[CMD] Trail Prefab이 없습니다.");
            return;
        }

        GameObject trail = Instantiate(weaponSystem.bulletTrailPrefab, start, Quaternion.identity);
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
