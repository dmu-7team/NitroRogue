using UnityEngine;
using Mirror;

public class Gwanjeon : MonoBehaviour
{
    [Header("따라갈 미니맵 카메라")]
    public Camera miniMapCamera;

    [Header("팔로우 설정")]
   // 위에서 내려다보는 높이
    public float followLerp = 10f;        // 위치 보간(클수록 더 빠르게 붙음)
    public bool rotateWithPlayer = false; // true면 플레이어 방향에 맞춰 미니맵 회전

    private Transform target;             // 로컬 플레이어 Transform

    void LateUpdate()
    {
        // 대상이 없거나 비활성화되면 재탐색
        if (target == null || !target.gameObject.activeInHierarchy)
            Gwanjeon_FindLocalPlayer();

        if (miniMapCamera == null || target == null) return;

        // --- 위치 따라가기 ---
        Vector3 desired = target.position;
       
        miniMapCamera.transform.position =
            Vector3.Lerp(miniMapCamera.transform.position, desired, Time.deltaTime * followLerp);

        // --- 회전 설정 ---
        if (rotateWithPlayer)
            miniMapCamera.transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f); // 플레이어 방향 기준
        else
            miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 고정 북쪽
    }

    void Gwanjeon_FindLocalPlayer()
    {
        // 씬 내 "Player" 태그 오브젝트들 중 로컬 플레이어 찾기
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            var ni = p.GetComponent<NetworkIdentity>();
            if (ni != null && ni.isLocalPlayer)
            {
                target = p.transform;
                return;
            }
        }
        // 못 찾으면 유지 (다음 LateUpdate에서 다시 시도)
    }
}
