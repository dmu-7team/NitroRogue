using System;
using UnityEngine;
using Mirror;

public class JoinButtonHandler : MonoBehaviour
{
    public void OnClickJoin()
    {
        string matchId = Guid.NewGuid().ToString();
        CustomNetworkManager.matchIdToJoin = matchId;

        Debug.Log($"[JoinButton] 자동 생성된 Match ID: {matchId}");

        if (!NetworkClient.active && !NetworkServer.active)
        {
            Debug.Log("[JoinButton] Host 시작");
            NetworkManager.singleton.StartHost(); // 씬 전환 포함됨
        }
        else if (!NetworkClient.active)
        {
            Debug.Log("[JoinButton] Client 시작");
            NetworkManager.singleton.StartClient(); // 씬 전환 없음
        }
    }
}
