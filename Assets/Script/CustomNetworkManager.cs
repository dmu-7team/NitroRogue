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
            Debug.Log($"matchIdToJoin ������: {_matchIdToJoin}");
        }
    }

    public Dictionary<string, List<NetworkConnection>> matchRooms = new();
    public List<NetworkConnectionToClient> connectedClients = new();

    [SerializeField]
    private int serverPort = 7777; // ���� ��Ʈ
    [SerializeField]
    private int clientPort = 7778; // Ŭ���̾�Ʈ ��Ʈ

    protected new void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("�ߺ� NetworkManager ������. �ڵ� ���ŵ�.");
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
            Debug.Log($"TelepathyTransport ���� ��Ʈ ����: {serverPort}");
        }
        else
        {
            Debug.LogError("TelepathyTransport ������Ʈ�� ã�� �� �����ϴ�. Inspector���� �߰��ϼ���.");
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
            Debug.Log($"TelepathyTransport Ŭ���̾�Ʈ ��Ʈ ����: {clientPort}");
        }
        else
        {
            Debug.LogError("TelepathyTransport ������Ʈ�� ã�� �� �����ϴ�. Inspector���� �߰��ϼ���.");
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
            Debug.LogWarning($"[ServerAddPlayer] matchIdToJoin�� ���� �⺻�� ����: {matchIdToJoin}");
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
            Debug.Log($"[RoomPlayer] matchId ������: {player.matchId}");
        }
        else
        {
            Debug.LogError("�÷��̾� ������Ʈ���� RoomPlayer ������Ʈ�� ã�� �� �����ϴ�.");
        }

        Debug.Log($"[CustomNetworkManager] Match {matchIdToJoin}�� �÷��̾� �߰���");

        // �� ����Ʈ ���� ���
        if (conn.identity != null)
        {
            SendRoomListToAll();
        }
        else
        {
            Debug.LogWarning("conn.identity�� �غ���� �ʾҽ��ϴ�. SendRoomListToAll ȣ���� �����մϴ�.");
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
        Debug.Log("[����] �� ����Ʈ ��ü ���� �õ�");
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
                Debug.Log($"[����] �� ����Ʈ�� connId={connToClient.connectionId} ���� ����");
            }
            sync.TargetReceiveRoomList(conn, infoList);
            // UI ���� Ʈ����
            if (RoomListUI.Instance != null)
            {
                RoomListUI.Instance.RenderRoomList(infoList);
            }
        }
        else
        {
            Debug.LogWarning("RoomListSync ���� ����: conn �Ǵ� identity ����");
        }
    }
}