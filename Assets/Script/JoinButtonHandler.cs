using UnityEngine;
using Mirror;
using NetworkMessages;

public class JoinButtonHandler : MonoBehaviour
{
    public static void JoinWithMatchId(string matchId)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            CustomNetworkManager.matchIdToJoin = matchId;

            NetworkClient.OnConnectedEvent += () =>
            {
                var msg = new JoinMatchMessage
                {
                    matchId = matchId,
                    roomName = "" // 필요 없다면 비워두기
                };
                NetworkClient.Send(msg);
                Debug.Log($"[JoinButtonHandler] 서버에 matchId 전송: {matchId}");
            };

            NetworkManager.singleton.networkAddress = "127.0.0.1";
            NetworkManager.singleton.StartClient();

            Debug.Log($"[JoinButtonHandler] 클라이언트 시작 - matchId: {matchId}");
        }
        else
        {
            Debug.LogWarning("[JoinButtonHandler] 이미 네트워크가 실행 중입니다.");
        }
    }
}
