using Mirror;
using UnityEngine;
using System.Collections;
using BitWave_Labs.AnimatedTextReveal;

[RequireComponent(typeof(PlayerMovementRBM))]
[RequireComponent(typeof(WeaponSystemRBM))]
public class PlayerControllerRBM : NetworkBehaviour
{
    private PlayerMovementRBM movement;
    public GameObject cameraObject;

    public override void OnStartAuthority()
    {
        // 1. 다른 FPSCam 끄고 내 카메라만 살리기
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.gameObject.name == "FPSCam")
                cam.gameObject.SetActive(false);
        }

        // 2. 컴포넌트 캐싱
        movement = GetComponent<PlayerMovementRBM>();

        // 3. 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 4. 내 카메라만 활성화 + MainCamera 태그 부여
        if (cameraObject != null)
        {
            cameraObject.SetActive(true);
            cameraObject.tag = "MainCamera";
            Debug.Log("[PlayerController] FPS 카메라 활성화 및 태그 설정 완료");
        }
        else
        {
            Debug.LogWarning("[PlayerController] cameraObject 연결되지 않음!");
        }

        // 5. UI 전환 / 연출
        RoomUIManager.Instance?.SwitchToGameUI();
        Debug.Log("[PlayerController] 권한 있는 내 캐릭터로 전환됨: UI 및 카메라 설정 완료");

        AnimateText.Instance?.ShowMapName();
        AudioManager.Instance?.PlayGameBGM();
    }

    void Update()
    {
        if (!isLocalPlayer || movement == null) return;

        // ❌ 이동/시점은 여기서 다시 부르면 안 됨
        // movement.HandleMove();
        // movement.HandleLook();
    }

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

    private IEnumerator DestroyAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        NetworkServer.Destroy(obj);
    }
}
