using Mirror;
using NetworkMessages;
using UnityEngine.SceneManagement;
using UnityEngine;

public class CustomNetworkManager : NetworkManager
{
    public static string matchIdToJoin;
    private static bool joinSent = false; //  중복 방지용

    public override void OnStartClient()
    {
        base.OnStartClient();

        joinSent = false; // 클라이언트 재시작 시 초기화

        NetworkClient.RegisterHandler<JoinResultMessage>(OnJoinResultMessageReceived);
        NetworkClient.RegisterHandler<RoomListSyncMessage>(msg => RoomListUI.Instance.OnRoomListSyncMessageReceived(msg));
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        if (!string.IsNullOrEmpty(RoomListUI.matchIdToJoin))
        {
            Debug.Log($"[Client] OnClientConnect → 자동 참가 요청: {RoomListUI.matchIdToJoin}");
            var joinMsg = new JoinMatchMessage
            {
                matchId = RoomListUI.matchIdToJoin,
                roomName = RoomListUI.matchIdToJoin
            };
            NetworkClient.Send(joinMsg);
        }
        else
        {
            Debug.Log("[Client] matchIdToJoin이 비어 있음 → 자동 참가 생략");
        }
    }



    private void OnJoinResultMessageReceived(JoinResultMessage msg)
    {
        if (msg.success)
        {
            Debug.Log($"[Client] 방 참가 성공: {msg.roomName} ({msg.matchId})");
            RoomUIManager.Instance.ShowRoom(msg.roomName);
        }
        else
        {
            Debug.LogWarning("[Client] 방 참가 실패");
        }
    }
}
