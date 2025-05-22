using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager
{
    public static CustomNetworkManager Instance { get; private set; }

    private static string _matchIdToJoin;
    public static string matchIdToJoin
    {
        get => _matchIdToJoin;
        set
        {
            _matchIdToJoin = value;
            Debug.Log($"matchIdToJoin 설정됨: {_matchIdToJoin}");
        }
    }

    public Dictionary<string, List<NetworkConnection>> matchRooms = new();
    public List<NetworkConnectionToClient> connectedClients = new();

    [SerializeField]
    private int serverPort = 7777; // 서버 포트
    [SerializeField]
    private int clientPort = 7778; // 클라이언트 포트

    protected new void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("중복 NetworkManager 감지됨. 자동 제거됨.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    protected new void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Start()
    {
        var transport = GetComponent<TelepathyTransport>();
        if (transport != null)
        {
            transport.port = (ushort)serverPort;
            Debug.Log($"TelepathyTransport 서버 포트 설정: {serverPort}");
        }
        else
        {
            Debug.LogError("TelepathyTransport 컴포넌트를 찾을 수 없습니다. Inspector에서 추가하세요.");
            return;
        }
        base.Start();
    }

    public void StartClientWithCustomPort()
    {
        var transport = GetComponent<TelepathyTransport>();
        if (transport != null)
        {
            transport.port = (ushort)clientPort;
            Debug.Log($"TelepathyTransport 클라이언트 포트 설정: {clientPort}");
        }
        else
        {
            Debug.LogError("TelepathyTransport 컴포넌트를 찾을 수 없습니다. Inspector에서 추가하세요.");
            return;
        }
        StartClient();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        if (string.IsNullOrEmpty(matchIdToJoin))
        {
            matchIdToJoin = System.Guid.NewGuid().ToString();
            Debug.LogWarning($"[ServerAddPlayer] matchIdToJoin이 없어 기본값 설정: {matchIdToJoin}");
        }

        Debug.Log($"[ServerAddPlayer] matchIdToJoin: {matchIdToJoin}, Connection ID: {conn.connectionId}");

        connectedClients.Add(conn);

        if (!matchRooms.ContainsKey(matchIdToJoin))
            matchRooms[matchIdToJoin] = new List<NetworkConnection>();

        matchRooms[matchIdToJoin].Add(conn);

        var player = conn.identity.GetComponent<RoomPlayer>();
        if (player != null)
        {
            player.matchId = matchIdToJoin;
            Debug.Log($"[RoomPlayer] matchId 설정됨: {player.matchId}");
        }
        else
        {
            Debug.LogError("플레이어 오브젝트에서 RoomPlayer 컴포넌트를 찾을 수 없습니다.");
        }

        Debug.Log($"[CustomNetworkManager] Match {matchIdToJoin}에 플레이어 추가됨");

        // 방 리스트 전송 대기
        if (conn.identity != null)
        {
            SendRoomListToAll();
        }
        else
        {
            Debug.LogWarning("conn.identity가 준비되지 않았습니다. SendRoomListToAll 호출을 지연합니다.");
            StartCoroutine(DelayedSendRoomList(conn));
        }
    }

    private System.Collections.IEnumerator DelayedSendRoomList(NetworkConnectionToClient conn)
    {
        yield return new WaitForSeconds(0.1f);
        if (conn.identity != null)
        {
            SendRoomListToAll();
        }
    }

    public void SendRoomListToAll()
    {
        Debug.Log("[서버] 방 리스트 전체 전송 시도");
        foreach (var conn in connectedClients)
        {
            SendRoomListToClient(conn);
        }
    }

    public void SendRoomListToClient(NetworkConnection conn)
    {
        List<RoomInfo> infoList = new();

        foreach (var kvp in matchRooms)
        {
            infoList.Add(new RoomInfo(kvp.Key, kvp.Value.Count, maxConnections));
        }

        if (conn != null && conn.identity != null && conn.identity.TryGetComponent(out RoomListSync sync))
        {
            if (conn is NetworkConnectionToClient connToClient)
            {
                Debug.Log($"[서버] 방 리스트를 connId={connToClient.connectionId} 에게 전송");
            }
            sync.TargetReceiveRoomList(conn, infoList);
            // UI 갱신 트리거
            if (RoomListUI.Instance != null)
            {
                RoomListUI.Instance.RenderRoomList(infoList);
            }
        }
        else
        {
            Debug.LogWarning("RoomListSync 전송 실패: conn 또는 identity 없음");
        }
    }
}