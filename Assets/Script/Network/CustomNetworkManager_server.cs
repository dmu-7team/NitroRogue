using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using NetworkMessages;

public class CustomNetworkManager_Server : NetworkManager
{
    public GameObject[] characterPrefabs;
    public GameObject roomPlayerPrefab;
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();
    private Dictionary<int, string> characterPrefabMap = new()
    {
        { 0, "Player_ver_EF" },
        { 1, "Player_ver_RBM" },
        { 2, "Player_ver_RBM2" }
    };
    private Transform[] spawnPoints;

    public override void Start()
    {
        base.Start();
        Debug.Log("[서버] Start() 호출됨");
        StartServer();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[서버] OnStartServer 진입 완료");

        // 런타임 스폰포인트 자동 탐색
        spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint")
                                .OrderBy(go => go.name)
                                .Select(go => go.transform)
                                .ToArray();

        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
    }

    private void OnJoinMatchMessageReceived(NetworkConnectionToClient conn, JoinMatchMessage msg)
    {
        Debug.Log($"[서버] JoinMatch 요청: {msg.matchId} / {msg.roomName}");

        if (conn.identity != null)
            NetworkServer.Destroy(conn.identity.gameObject);

        if (!matchRooms.ContainsKey(msg.matchId))
            matchRooms[msg.matchId] = new List<RoomPlayer>();

        GameObject playerObj = Instantiate(roomPlayerPrefab);
        RoomPlayer roomPlayer = playerObj.GetComponent<RoomPlayer>();
        roomPlayer.matchId = msg.matchId;
        roomPlayer.roomName = msg.roomName;
        roomPlayer.playerName = $"플레이어{matchRooms[msg.matchId].Count + 1}";
        roomPlayer.isLeader = matchRooms[msg.matchId].Count == 0;

        NetworkServer.AddPlayerForConnection(conn, playerObj);
        matchRooms[msg.matchId].Add(roomPlayer);

        conn.Send(new JoinResultMessage
        {
            success = true,
            matchId = msg.matchId,
            roomName = msg.roomName
        });

        BroadcastPlayerList(msg.matchId);
    }

    private void OnRoomListRequestMessageReceived(NetworkConnectionToClient conn, RoomListRequestMessage msg)
    {
        List<RoomInfo> roomInfos = matchRooms.Select(pair => new RoomInfo
        {
            matchId = pair.Key,
            roomName = pair.Value.FirstOrDefault()?.roomName ?? "비어있는 방",
            currentPlayers = pair.Value.Count,
            maxPlayers = 4
        }).ToList();

        conn.Send(new RoomListSyncMessage { roomList = roomInfos });
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        string matchId = null;

        if (conn.identity != null)
        {
            RoomPlayer player = conn.identity.GetComponent<RoomPlayer>();
            if (player != null && matchRooms.ContainsKey(player.matchId))
            {
                matchId = player.matchId;
                matchRooms[matchId].Remove(player);
                if (matchRooms[matchId].Count == 0)
                    matchRooms.Remove(matchId);
                else if (player.isLeader)
                    matchRooms[matchId][0].isLeader = true;

                SendRoomListToAllClients();
                BroadcastPlayerList(matchId);
            }
        }

        base.OnServerDisconnect(conn);
    }

    public void StartGame(string matchId)
    {
        Debug.Log($"[서버] 게임 시작 요청: {matchId}");

        if (!matchRooms.TryGetValue(matchId, out var players))
        {
            Debug.LogError("[서버] 매치 ID에 해당하는 방 없음");
            return;
        }

        foreach (var roomPlayer in players)
        {
            var conn = roomPlayer.connectionToClient;
            int index = roomPlayer.selectedCharacter;

            if (!characterPrefabMap.TryGetValue(index, out var prefabName))
            {
                Debug.LogError($"[서버] 캐릭터 인덱스에 해당하는 프리팹 이름 없음: {index}");
                continue;
            }

            var prefab = spawnPrefabs.FirstOrDefault(p => p != null && p.name == prefabName);
            if (prefab == null)
            {
                Debug.LogError($"[서버] 캐릭터 프리팹을 찾을 수 없음: {prefabName}");
                continue;
            }

            Vector3 spawnPos = GetSpawnPosition(index);
            GameObject playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);

            NetworkServer.Spawn(playerObj, conn); // 핵심 추가!
            NetworkServer.Destroy(roomPlayer.gameObject);

            var replaceOptions = new ReplacePlayerOptions();
            NetworkServer.ReplacePlayerForConnection(conn, playerObj, replaceOptions);

            var newRoomPlayer = playerObj.GetComponent<RoomPlayer>();
            if (newRoomPlayer != null)
                newRoomPlayer.TargetStartGame(conn, index, matchId);
        }
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || index >= spawnPoints.Length)
            return new Vector3(index * 2f, 2f, 0f); // 높이 약간 띄움

        return spawnPoints[index].position;
    }

    public void BroadcastPlayerList(string matchId)
    {
        if (!matchRooms.ContainsKey(matchId)) return;

        foreach (var player in matchRooms[matchId])
        {
            List<RoomPlayer.PlayerInfo> infoList = matchRooms[matchId].Select(p => new RoomPlayer.PlayerInfo
            {
                name = p.playerName,
                isLeader = p.isLeader,
                isMe = p == player
            }).ToList();

            player.TargetRebuildPlayerList(infoList);
        }
    }

    private void SendRoomListToAllClients()
    {
        var roomInfos = matchRooms.Select(pair => new RoomInfo
        {
            matchId = pair.Key,
            roomName = pair.Value.FirstOrDefault()?.roomName ?? "",
            currentPlayers = pair.Value.Count,
            maxPlayers = 4
        }).ToList();

        var msg = new RoomListSyncMessage { roomList = roomInfos };
        NetworkServer.SendToAll(msg);
    }
}
