using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkMatch))]
public class RoomPlayer : NetworkBehaviour
{
    [SyncVar]
    public string matchId;

    [SyncVar]
    public string roomName;

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady;

    public override void OnStartServer()
    {
        base.OnStartServer();
        var match = GetComponent<NetworkMatch>();
        if (match != null)
        {
            if (string.IsNullOrEmpty(matchId) || !System.Guid.TryParse(matchId, out System.Guid validGuid))
            {
                matchId = System.Guid.NewGuid().ToString();
                Debug.LogWarning($"[RoomPlayer] matchId가 유효하지 않아 기본값으로 설정됨: {matchId}");
            }
            match.matchId = System.Guid.Parse(matchId);
            Debug.Log($"[RoomPlayer] Match ID 설정됨: {matchId}, isServer: {isServer}");
        }
        else
        {
            Debug.LogError("[RoomPlayer] NetworkMatch 컴포넌트를 찾을 수 없습니다.");
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log("[RoomPlayer] OnStartLocalPlayer 호출됨");
    }

    // 클라이언트에서 Ready 버튼 눌렀을 때 호출
    [Command]
    public void CmdSetReady(bool ready)
    {
        isReady = ready;
        Debug.Log($"[RoomPlayer] 서버에 Ready 설정: {ready}");

        var manager = NetworkManager.singleton as CustomNetworkManager;
        manager?.CheckIfAllReady(matchId);
    }

    //  isReady 값이 바뀌었을 때 클라이언트에서 실행됨
    void OnReadyChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[RoomPlayer] Ready 상태 변경됨: {newValue}");
        // 필요 시 UI 업데이트 가능
    }

    // 서버가 게임 시작할 때 개별 클라이언트에 알려주는 RPC
    [TargetRpc]
    public void TargetStartGame()
    {
        Debug.Log("[RoomPlayer] 게임 시작됨! UI 갱신 등 실행 가능");
        GameObject.Find("ReadyButton")?.SetActive(false);
        GameObject.Find("GameUI")?.SetActive(true);
    }
}
