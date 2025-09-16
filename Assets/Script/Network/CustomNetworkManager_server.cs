using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using NetworkMessages;
using System.Collections;
using UnityEngine.AI;

public class CustomNetworkManager_Server : NetworkManager
{
    [Header("Match-scoped managers")]
    [SerializeField] private GameObject missionManagerPrefab; // ← 인스펙터에 NetworkMissionManager 프리팹 연결
    public GameObject[] monsterPrefabs;
    public GameObject[] characterPrefabs;
    public GameObject roomPlayerPrefab;
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();
   private Dictionary<int, string> characterPrefabMap = new()
    {
        { 0, "Player_ver_AR" },
        { 1, "Player_ver_DMR" },
        { 2, "Player_ver_SG" },
        { 3, "Player_ver_SMG" }
    };

    private Transform[] spawnPoints;
    private Transform[] enemySpawnPoints;

    public GameObject MonsterSpawner;
    public GameObject GameManager;

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

        //  플레이어 스폰포인트 저장
        spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint")
                                .OrderBy(go => go.name)
                                .Select(go => go.transform)
                                .ToArray();
        Debug.Log($"[서버] PlayerSpawnPoint 개수: {spawnPoints.Length}");

        //  몬스터 스폰포인트 저장
        enemySpawnPoints = GameObject.FindGameObjectsWithTag("EnemySpawnPoint")
                                     .OrderBy(go => go.name)
                                     .Select(go => go.transform)
                                     .ToArray();
        Debug.Log($"[서버] EnemySpawnPoint 개수: {enemySpawnPoints.Length}");

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
        SendSelectionsTo(conn, msg.matchId);
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
        Debug.Log($"[서버] StartGame() 호출됨 - matchId: {matchId}");

        if (!matchRooms.TryGetValue(matchId, out var players) || players.Count == 0)
        {
            Debug.LogError($"[서버] 매치 ID({matchId})에 해당하는 방이 없거나 비어 있음");
            return;
        }

        if (!Guid.TryParse(matchId, out var parsedMatchId))
        {
            Debug.LogError($"[서버] matchId 파싱 실패: {matchId}");
            return;
        }

        var spawnerObj = Instantiate(GameManager);
        spawnerObj.GetComponent<NetworkMatch>().matchId = parsedMatchId;
        NetworkServer.Spawn(spawnerObj);

        foreach (var roomPlayer in players)
        {
            var conn = roomPlayer.connectionToClient;
            int index = roomPlayer.selectedCharacter;

            if (index < 0 || index >= characterPrefabs.Length)
            {
                Debug.LogError($"[서버] 잘못된 캐릭터 인덱스: {index}");
                continue;
            }

            GameObject prefab = characterPrefabs[index];
            if (prefab == null)
            {
                Debug.LogError($"[서버] characterPrefabs[{index}]가 null임");
                continue;
            }

            // 생성 위치
            Vector3 spawnPos = GetSpawnPosition(index);

            // 캐릭터 인스턴스 생성
            GameObject playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            Debug.Log($"[서버] 캐릭터 인스턴스 생성됨: {playerObj.name}");

            // matchId 설정
            if (playerObj.TryGetComponent(out NetworkMatch matchComp))
            {
                matchComp.matchId = parsedMatchId;
            }
            var stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.NickName = roomPlayer.playerName;
            }

            // 미리 게임 시작 알림
            roomPlayer.TargetStartGame(conn, index, matchId);
            Debug.Log($"[서버] TargetStartGame 호출 완료");

            // 기존 RoomPlayer 제거 → ReplacePlayer 순서 중요!
            NetworkServer.Destroy(roomPlayer.gameObject);

            // 교체 및 스폰
            NetworkServer.ReplacePlayerForConnection(conn, playerObj, new ReplacePlayerOptions());
            NetworkServer.Spawn(playerObj);
            Debug.Log($"[서버] ReplacePlayer + Spawn 완료: {playerObj.name}");
        }

        //var spawnerObj = Instantiate(MonsterSpawner);
        //if (spawnerObj.TryGetComponent(out NetworkMatch spawnmatch))
        //{
        //    spawnmatch.matchId = parsedMatchId;
        //}
        //NetworkServer.Spawn(spawnerObj);

        if (missionManagerPrefab != null)
        {
            var mmObj = Instantiate(missionManagerPrefab);

            // 매치 분리(InterestManagement)를 쓰고 있으니 같은 matchId 부여
            var mmMatch = mmObj.GetComponent<NetworkMatch>();
            if (mmMatch != null) mmMatch.matchId = parsedMatchId;

            NetworkServer.Spawn(mmObj);
            Debug.Log($"[서버] MissionManager 스폰 완료 (matchId={parsedMatchId})");
        }
        else
        {
            Debug.LogError("[서버] missionManagerPrefab 이 비어있습니다. 인스펙터에 연결하세요.");
        }
        // 몬스터 생성 지연
        //StartCoroutine(SpawnEnemiesAfterDelay(matchId, 1f));
        //Debug.Log("[서버] 모든 플레이어 처리 완료 → 몬스터 스폰 대기 중...");
    }

    private IEnumerator SpawnEnemiesAfterDelay(string matchId, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnEnemiesForMatch(matchId);
    }




    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || index >= spawnPoints.Length)
            return new Vector3(index * 2f, 0f, 0f);

        return spawnPoints[index].position; //  Y축 고정 제거
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
    private Vector3 GetEnemySpawnPosition(int index)
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0 || index >= enemySpawnPoints.Length)
            return new Vector3(index * 3f, 1f, 5f); // 기본 위치: 좌우로 벌리고 살짝 앞으로

        return enemySpawnPoints[index].position;
    }

    public void SpawnEnemiesForMatch(string matchId)
    {
        Debug.Log($"[몬스터스폰] SpawnEnemiesForMatch() 진입: matchId = {matchId}");

        if (!Guid.TryParse(matchId, out var guid))
        {
            Debug.LogError($"[몬스터스폰] matchId Guid 변환 실패: {matchId}");
            return;
        }

        if (!matchRooms.TryGetValue(matchId, out var players))
        {
            Debug.LogError($"[몬스터스폰] 해당 matchId의 플레이어 목록 없음: {matchId}");
            return;
        }

        Debug.Log($"[몬스터스폰] 플레이어 수: {players.Count}, 몬스터 프리팹 수: {monsterPrefabs.Length}");

        for (int i = 0; i < monsterPrefabs.Length && i < players.Count; i++)
        {
            var prefab = monsterPrefabs[i];
            if (prefab == null) continue;

            Vector3 playerPos = players[i] != null ? players[i].transform.position : Vector3.zero;

            // 플레이어 주변 랜덤 위치
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 3f;
            offset.y = 0f; // 수직 무시
            Vector3 tryPos = playerPos + offset;

            Vector3 spawnPos = tryPos; // 기본값은 보정 실패 시 fallback용

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
                Debug.Log($"[몬스터스폰] NavMesh 보정 위치: {spawnPos}");
            }
            else
            {
                spawnPos.y = 0f; // <= 이 줄 추가
                Debug.LogWarning($"[몬스터스폰] NavMesh 보정 실패, 기본 위치로 강제 소환: {spawnPos}");
            }


            GameObject monster = Instantiate(prefab, spawnPos, Quaternion.identity);
            Debug.Log($"[몬스터스폰] {prefab.name} → {spawnPos} 위치에 Instantiate 성공");

            var match = monster.GetComponent<NetworkMatch>();
            if (match != null)
                match.matchId = guid;

            NetworkServer.Spawn(monster);
            Debug.Log($"[몬스터스폰] NetworkServer.Spawn 완료: {monster.name}");
        }

        Debug.Log("[몬스터스폰] 플레이어 주변 몬스터 스폰 완료");
    }




    // ★★★ [추가] 현재 방의 스냅샷 만들기
    private (int[] selected, string[] names) BuildSelectionSnapshot(string matchId)
    {
        if (!matchRooms.TryGetValue(matchId, out var list) || list == null)
            return (Array.Empty<int>(), Array.Empty<string>());

        var players = list
            .Where(p => p != null && p.connectionToClient != null)
            .OrderBy(p => p.connectionToClient.connectionId)   // 순서 고정
            .ToArray();

        int[] selected = new int[players.Length];
        string[] names = new string[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            selected[i] = players[i].selectedCharacter;
            names[i] = players[i].playerName;
        }
        return (selected, names);
    }

    // ★★★ [추가] 방에 "방금 들어온 1명"에게만 스냅샷 푸시
    public void SendSelectionsTo(NetworkConnectionToClient conn, string matchId)
    {
        var (selected, names) = BuildSelectionSnapshot(matchId);

        var rp = conn?.identity ? conn.identity.GetComponent<RoomPlayer>() : null;
        if (rp == null) return;

        rp.TargetUpdateCharacterButtons(conn, selected, names);    // RoomPlayer의 TargetRpc
        Debug.Log($"[Server→Target] snapshot to conn={conn.connectionId} matchId={matchId} names={string.Join(", ", names)}");
    }

    // ★★★ [추가] 같은 방 모두에게 브로드캐스트
    public void BroadcastSelections(string matchId)
    {
        if (!matchRooms.TryGetValue(matchId, out var list) || list == null) return;

        var (selected, names) = BuildSelectionSnapshot(matchId);
        foreach (var rp in list)
        {
            var c = rp.connectionToClient;
            if (c != null)
                rp.TargetUpdateCharacterButtons(c, selected, names);
        }
        Debug.Log($"[Server→All] broadcast matchId={matchId} names={string.Join(", ", names)}");
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
