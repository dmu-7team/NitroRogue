using Mirror;
using UnityEngine;

public class RoomPlayer : NetworkBehaviour
{
    [SyncVar] public string matchId;
    [SyncVar] public string roomName;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[RoomPlayer] Match ID 설정됨: {matchId}, isServer: {isServer}");
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log("[RoomPlayer] OnStartLocalPlayer 호출됨");
    }

    public void SetMatchId(string id)
    {
        matchId = id;
        roomName = "Room-" + id.Substring(0, 4);
    }
    [Command]
    public void CmdSetReady(bool isReady)
    {
        Debug.Log($"[RoomPlayer] Ready 상태 설정: {isReady}");
        // 예시: 여기선 아무 동작 안 하지만, 서버에서 체크할 수도 있음
    }
}
