using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomPlayer : NetworkBehaviour
{
    [SyncVar] public string matchId;
    [SyncVar(hook = nameof(OnRoomNameChanged))] public string roomName;
    [SyncVar] public int currentPlayers;
    [SyncVar] public int maxPlayers;
    [SyncVar] public bool isLeader = false;
    [SyncVar(hook = nameof(OnReadyChanged))] public bool isReady = false;
    [SyncVar] public string playerName = "플레이어";

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isLocalPlayer)
        {
            Invoke(nameof(UpdateRoomUI), 0.3f);
        }

        if (isLeader && RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.ShowStartButton(true);
        }

        gameObject.hideFlags = HideFlags.HideInHierarchy;
        gameObject.name = $"[RoomPlayer:{roomName}]";
    }

    private void OnRoomNameChanged(string oldName, string newName)
    {
        if (isLocalPlayer && RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.UpdateRoomName(newName);
            Debug.Log($"[RoomPlayer] 방 이름 변경됨: {newName}");
        }
    }

    private void OnReadyChanged(bool oldReady, bool newReady)
    {
        if (RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.UpdatePlayerReadyStatus(this, newReady);
        }
    }

    private void UpdateRoomUI()
    {
        if (RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.ShowRoom(roomName);
            Debug.Log($"[RoomPlayer] 룸 UI 갱신됨: {roomName}");
        }
        else
        {
            Debug.LogWarning("[RoomPlayer] RoomUIManager 인스턴스를 찾을 수 없음");
        }
    }

    public void SetMatchInfo(string id, string name)
    {
        matchId = id;
        roomName = name;
    }

    [Command]
    public void CmdStartGame()
    {
        if (!isLeader)
        {
            Debug.LogWarning("[RoomPlayer] 방장이 아니므로 게임 시작 불가");
            return;
        }

        Debug.Log("[RoomPlayer] 게임 시작 요청");

        if (NetworkManager.singleton is CustomNetworkManager_Server manager && manager.matchRooms.ContainsKey(matchId))
        {
            manager.StartGame(matchId);
        }
    }

    [TargetRpc]
    public void TargetLoadGameScene()
    {
        Debug.Log("[RoomPlayer] 클라이언트에서 게임 씬으로 전환");
        SceneManager.LoadScene("GameScene");
    }

    [Command]
    public void CmdSetReady(bool isReady)
    {
        Debug.Log($"[RoomPlayer] Ready 상태 설정: {isReady}");
        this.isReady = isReady;
    }
}
