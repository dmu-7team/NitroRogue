using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
    [Header("Room Settings")]
    public string roomSceneName = "RoomScene";

    //  방 정보 매핑 (matchId → RoomInfo)
    private Dictionary<string, RoomInfo> roomDict = new Dictionary<string, RoomInfo>();

    // 연결된 클라이언트가 요청한 matchId 기록
    private Dictionary<NetworkConnectionToClient, string> pendingMatchIds = new Dictionary<NetworkConnectionToClient, string>();
    public static string matchIdToJoin;
    public override void Start()
    {
#if UNITY_SERVER
        Debug.Log("[서버] UNITY_SERVER에서 StartServer() 실행");
        StartServer();
#endif
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[서버] Dedicated Server 시작됨");

        NetworkServer.RegisterHandler<JoinRoomMessage>(OnJoinRoomMessageReceived);
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"[서버] 클라이언트 접속됨: {conn.address}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != roomSceneName)
        {
            Debug.LogWarning("[서버] RoomScene이 아니므로 플레이어를 생성하지 않음");
            return;
        }

        if (!pendingMatchIds.TryGetValue(conn, out string matchId) || string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[서버] matchId 정보가 없어 플레이어를 할당하지 않음");
            return;
        }

        GameObject player = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, player);

        Debug.Log($"[서버] 플레이어 입장. Match ID: {matchId}");

        pendingMatchIds.Remove(conn);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        pendingMatchIds.Clear();
        roomDict.Clear();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[클라이언트] 서버에 연결됨");

        if (!string.IsNullOrEmpty(RoomListUI.matchIdToJoin))
        {
            JoinRoomMessage msg = new JoinRoomMessage
            {
                matchId = RoomListUI.matchIdToJoin
            };
            NetworkClient.Send(msg);
            Debug.Log($"[클라이언트] 요청하는 Match ID: {msg.matchId}");
        }
    }

    public override void OnClientDisconnect()
    {
        Debug.LogWarning("[클라이언트] 서버와의 연결 끊김");
        base.OnClientDisconnect();
    }

    private void OnJoinRoomMessageReceived(NetworkConnectionToClient conn, JoinRoomMessage msg)
    {
        Debug.Log($"[서버] 클라이언트가 JoinRoom 요청: {msg.matchId}");

        if (!pendingMatchIds.ContainsKey(conn))
            pendingMatchIds.Add(conn, msg.matchId);

        //  matchId가 유효할 때만 씬 전환
        if (!string.IsNullOrEmpty(msg.matchId) && SceneManager.GetActiveScene().name != roomSceneName)
        {
            Debug.Log("[서버] RoomScene으로 전환 시도");
            ServerChangeScene(roomSceneName);
        }
    }

}

public struct JoinRoomMessage : NetworkMessage
{
    public string matchId;
}


