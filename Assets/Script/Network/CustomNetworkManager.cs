using Mirror;
using NetworkMessages;
using UnityEngine;

public class CustomNetworkManager : NetworkManager
{
    public static string matchIdToJoin;

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 클라이언트 전용 메시지 핸들러 등록
        NetworkClient.RegisterHandler<JoinResultMessage>(OnJoinResultMessageReceived);
        NetworkClient.RegisterHandler<RoomListSyncMessage>(msg => RoomListUI.Instance.OnRoomListSyncMessageReceived(msg));
    }

    private void OnJoinResultMessageReceived(JoinResultMessage msg)
    {
        if (msg.success)
        {
            Debug.Log($"[Client] 방 참가 성공: {msg.roomName} ({msg.matchId})");
        }
        else
        {
            Debug.LogWarning("[Client] 방 참가 실패");
        }
    }

    // 서버 관련 로직은 클라이언트용에서는 포함하지 않음
    public override void OnStartServer() { }
    public override void OnServerDisconnect(NetworkConnectionToClient conn) { }
    public override void OnServerAddPlayer(NetworkConnectionToClient conn) { }
}
