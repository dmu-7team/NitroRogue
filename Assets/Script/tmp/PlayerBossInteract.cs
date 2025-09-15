using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어가 제단 근처에서 키를 누르면 서버에 보스 소환 요청.
/// </summary>
public class PlayerBossInteract : NetworkBehaviour
{
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private string altarTag = "BossAltar";
    [SerializeField] private KeyCode key = KeyCode.F;

    void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(key))
        {
            var altar = FindNearestWithTag(altarTag, interactRange);
            if (altar != null) CmdRequestBossSpawn();
        }
    }

    GameObject FindNearestWithTag(string tag, float maxDist)
    {
        GameObject[] arr = GameObject.FindGameObjectsWithTag(tag);
        if (arr == null || arr.Length == 0) return null;

        GameObject best = null; float bestD = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var go in arr)
        {
            if (!go) continue;
            float d = Vector3.Distance(p, go.transform.position);
            if (d <= maxDist && d < bestD) { bestD = d; best = go; }
        }
        return best;
    }

    [Command]
    void CmdRequestBossSpawn()
    {
        // 1. 커맨드를 보낸 플레이어(connectionToClient)의 matchId를 가져옵니다.
        Guid matchId = connectionToClient.identity.GetComponent<NetworkMatch>().matchId;

        // 2. Dictionary에서 해당 matchId를 사용하는 MatchManager를 찾습니다.
        if (MatchManager.ActiveMatches.TryGetValue(matchId, out MatchManager matchManager))
        {
            // 3. 찾은 MatchManager에 요청을 보냅니다.
            matchManager.RequestBossSpawn();
        }
    }
}
