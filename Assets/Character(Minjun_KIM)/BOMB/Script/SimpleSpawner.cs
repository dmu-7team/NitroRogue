using UnityEngine;
using Mirror;

public class SimpleSpawner : NetworkBehaviour
{
    [Header("테스트할 프리팹")]
    public GameObject prefabToTest;

    void Update()
    {
        // 로컬 플레이어만 입력을 받음
        if (!isLocalPlayer) return;

        // 'T' 키를 누르면 테스트 스폰 명령을 서버에 보냄
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (prefabToTest != null)
            {
                Debug.Log("[클라이언트] 테스트 스폰 명령을 보냅니다.");
                CmdSpawnTestPrefab();
            }
            else
            {
                Debug.LogError("[클라이언트] 테스트할 프리팹이 지정되지 않았습니다!");
            }
        }
    }

    [Command]
    void CmdSpawnTestPrefab()
    {
        // 플레이어 위치보다 2미터 앞에 프리팹을 생성
        Vector3 spawnPosition = transform.position + transform.forward * 2;
        GameObject instance = Instantiate(prefabToTest, spawnPosition, Quaternion.identity);

        // 네트워크에 스폰
        NetworkServer.Spawn(instance);

        Debug.Log("[서버] 테스트 프리팹 스폰 완료!");
    }
}