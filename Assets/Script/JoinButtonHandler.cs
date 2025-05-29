using UnityEngine;
using Mirror;

public class JoinButtonHandler : MonoBehaviour
{
    public static void JoinWithMatchId(string matchId)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            CustomNetworkManager.matchIdToJoin = matchId;
            NetworkManager.singleton.StartClient();
            Debug.Log($"[JoinButtonHandler] 클라이언트로 matchId 접속: {matchId}");
        }
        else
        {
            Debug.LogWarning("[JoinButtonHandler] 이미 네트워크가 실행 중입니다.");
        }
    }
}
