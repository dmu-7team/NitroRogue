using Mirror;
using UnityEngine;
using System.Collections.Generic;
using NetworkMessages;
using System.Linq;
using System;

public class CustomNetworkManager_Server : NetworkManager
{
    public GameObject[] characterPrefabs;
    public Transform[] spawnPoints;
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();
    public GameObject playerPrefab_EF;
    public GameObject playerPrefab_RBM;
    public GameObject playerPrefab_RBM2;
    private Transform[] runtimeSpawnPoints;

    [SerializeField] private GameObject roomPlayerPrefab;

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

        // 스폰포인트 직접 생성
        spawnPoints = new Transform[3];
        spawnPoints[0] = CreateSpawnPoint(new Vector3(0f, 0f, 0f));
        spawnPoints[1] = CreateSpawnPoint(new Vector3(2f, 0f, 0f));
        spawnPoints[2] = CreateSpawnPoint(new Vector3(-2f, 0f, 0f));

        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
    }


    // 빈 GameObject로 스폰포인트 생성
    private Transform CreateSpawnPoint(Vector3 position)
    {
        GameObject go = new GameObject("SpawnPoint");
        go.transform.position = position;
        return go.transform;
    }

    private void OnJoinMatchMessageReceived(NetworkConnectionToClient conn, JoinMatchMessage msg)
    {
        Debug.Log($"[서버] JoinMatch 요청: {msg.matchId} / {msg.roomName}");

        if (conn.identity != null)
        {
            NetworkServer.Destroy(conn.identity.gameObject);
        }

        if (!matchRooms.ContainsKey(msg.matchId))
        {
            matchRooms[msg.matchId] = new List<RoomPlayer>();
        }

        GameObject playerObj = Instantiate(roomPlayerPrefab);
        RoomPlayer roomPlayer = playerObj.GetComponent<RoomPlayer>();
        roomPlayer.matchId = msg.matchId;
        roomPlayer.roomName = msg.roomName;
        roomPlayer.playerName = $"플레이어{matchRooms[msg.matchId].Count + 1}";
        if (matchRooms[msg.matchId].Count == 0)
        {
            roomPlayer.isLeader = true;
        }

        NetworkServer.AddPlayerForConnection(conn, playerObj);
        matchRooms[msg.matchId].Add(roomPlayer);

        JoinResultMessage result = new()
        {
            success = true,
            matchId = msg.matchId,
            roomName = msg.roomName
        };
        conn.Send(result);
        BroadcastPlayerList(msg.matchId);

    }

    private void OnRoomListRequestMessageReceived(NetworkConnectionToClient conn, RoomListRequestMessage msg)
    {
        List<RoomInfo> roomInfos = new();

        foreach (var pair in matchRooms)
        {
            roomInfos.Add(new RoomInfo
            {
                matchId = pair.Key,
                roomName = pair.Value.Count > 0 ? pair.Value[0].roomName : "비어있는 방",
                currentPlayers = pair.Value.Count,
                maxPlayers = 4
            });
        }

        RoomListSyncMessage syncMsg = new RoomListSyncMessage { roomList = roomInfos };
        conn.Send(syncMsg);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        string matchId = null;

        if (conn.identity != null)
        {
            RoomPlayer player = conn.identity.GetComponent<RoomPlayer>();
            if (player != null && matchRooms.ContainsKey(player.matchId))
            {
                matchId = player.matchId; // 저장해두기

                matchRooms[matchId].Remove(player);
                if (matchRooms[matchId].Count == 0)
                {
                    matchRooms.Remove(matchId);
                }
                else if (player.isLeader)
                {
                    matchRooms[matchId][0].isLeader = true;
                }

                SendRoomListToAllClients();
            }
        }

        //  base 전에 matchId 기준으로 브로드캐스트
        if (!string.IsNullOrEmpty(matchId))
        {
            BroadcastPlayerList(matchId);
        }

        base.OnServerDisconnect(conn);
    }


    private void SendRoomListToAllClients()
    {
        List<RoomInfo> roomInfos = new();
        foreach (var pair in matchRooms)
        {
            roomInfos.Add(new RoomInfo
            {
                matchId = pair.Key,
                roomName = pair.Value.Count > 0 ? pair.Value[0].roomName : "",
                currentPlayers = pair.Value.Count,
                maxPlayers = 4
            });
        }
        RoomListSyncMessage msg = new RoomListSyncMessage { roomList = roomInfos };
        NetworkServer.SendToAll(msg);
    }

    public bool HasMatch(string matchId)
    {
        return matchRooms.ContainsKey(matchId);
    }

    public void StartGame(string matchId)
    {
        Debug.Log($"[서버] 게임 시작: {matchId}");

        if (!matchRooms.ContainsKey(matchId)) return;

        // 씬 전환 (씬 이름은 정확히 빌드 세팅에 등록된 이름)
        ServerChangeScene("Game");
    }
    public override void OnServerSceneChanged(string sceneName)
    {
        if (sceneName != "Game") return;

        // 1. 런타임에 스폰 포인트 수집
        runtimeSpawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint")
            .OrderBy(go => go.name)
            .Select(go => go.transform)
            .ToArray();

        foreach (var kvp in matchRooms)
        {
            string matchId = kvp.Key;
            List<RoomPlayer> roomPlayers = kvp.Value;

            for (int i = 0; i < roomPlayers.Count; i++)
            {
                RoomPlayer player = roomPlayers[i];

                GameObject prefab = GetPrefabForCharacter(player.selectedCharacter);
                if (prefab == null)
                {
                    Debug.LogError($"[서버] 선택된 캐릭터 {player.selectedCharacter} 프리팹 없음");
                    continue;
                }

                Vector3 spawnPos;
                if (runtimeSpawnPoints != null && runtimeSpawnPoints.Length > i)
                    spawnPos = runtimeSpawnPoints[i].position;
                else
                    spawnPos = new Vector3(i * 2f, 0f, 0f);

                GameObject gamePlayer = Instantiate(prefab, spawnPos, Quaternion.identity);

                if (gamePlayer.TryGetComponent(out NetworkMatch match))
                {
                    match.matchId = Guid.Parse(matchId);
                }

                NetworkServer.AddPlayerForConnection(player.connectionToClient, gamePlayer);
            }

            // 추가: 씬 내 MonsterSpawner 찾아서 matchId 지정
            MonsterSpawner spawner = FindFirstObjectByType<MonsterSpawner>();
            if (spawner != null)
            {
                spawner.matchId = matchId;
                Debug.Log($"[서버] MonsterSpawner에 matchId 설정 완료: {matchId}");
            }
            else
            {
                Debug.LogWarning("[서버] MonsterSpawner를 찾을 수 없습니다.");
            }
        }

        matchRooms.Clear();
    }




    private GameObject GetPrefabForCharacter(int index)
    {
        switch (index)
        {
            case 0: return playerPrefab_EF;
            case 1: return playerPrefab_RBM;
            case 2: return playerPrefab_RBM2;
            default: return null;
        }
    }
    private Vector3 GetSpawnPosition(int index)
    {
        // 배열이 유효하지 않으면 안전한 기본 위치 제공
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return new Vector3(index * 2f, 1f, 0f);  // y=1로 띄우자
        }

        if (index >= 0 && index < spawnPoints.Length && spawnPoints[index] != null)
        {
            return spawnPoints[index].position;
        }

        Debug.LogWarning("[서버] 유효하지 않은 스폰 위치 요청됨 → 기본 위치 사용");
        return new Vector3(index * 2f, 1f, 0f);
    }




    public void BroadcastPlayerList(string matchId)
    {
        if (!matchRooms.ContainsKey(matchId)) return;

        foreach (var player in matchRooms[matchId])
        {
            List<RoomPlayer.PlayerInfo> infoList = new();

            foreach (var p in matchRooms[matchId])
            {
                infoList.Add(new RoomPlayer.PlayerInfo
                {
                    name = p.playerName,
                    isLeader = p.isLeader,
                    isMe = p == player
                });
            }

            player.TargetRebuildPlayerList(infoList);
        }
    }
   




}
