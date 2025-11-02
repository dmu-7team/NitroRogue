using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using NetworkMessages;

public class CustomNetworkManager_Server : NetworkManager
{
    [Header("Match-scoped managers")]
    [SerializeField] private GameObject missionManagerPrefab;

    public GameObject[] characterPrefabs;
    public GameObject roomPlayerPrefab;

    [Header("매치 생성 설정 (이벤트 방식)")]
    public GameObject matchManagerPrefabs;
    public List<Transform> matchStartPoints;

    public Dictionary<string, List<RoomPlayer>> matchRooms = new();

    private List<Transform> availableStartPoints;
    private readonly Dictionary<Guid, List<RoomPlayer>> pendingMatches = new();
    private readonly HashSet<string> finishedMatches = new();
    private readonly Dictionary<Guid, Transform> matchStartPointMap = new();

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

        availableStartPoints = new List<Transform>(matchStartPoints);
        MatchManager.OnManagerReady += HandleManagerReady;
        MatchManager.OnMatchEnded += HandleMatchEnded;

        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        MatchManager.OnManagerReady -= HandleManagerReady;
        MatchManager.OnMatchEnded -= HandleMatchEnded;
    }

    private void OnJoinMatchMessageReceived(NetworkConnectionToClient conn, JoinMatchMessage msg)
    {
        Debug.Log($"[서버] JoinMatch 요청: {msg.matchId} / {msg.roomName} / nick='{msg.nickname}'");

        if (conn.identity != null)
            NetworkServer.Destroy(conn.identity.gameObject);

        if (!matchRooms.ContainsKey(msg.matchId))
            matchRooms[msg.matchId] = new List<RoomPlayer>();

        var playerObj = Instantiate(roomPlayerPrefab);
        var roomPlayer = playerObj.GetComponent<RoomPlayer>();
        roomPlayer.matchId = msg.matchId;
        roomPlayer.roomName = msg.roomName;
        roomPlayer.playerName = msg.nickname; // ★ SyncVar에 세팅
        roomPlayer.isLeader = matchRooms[msg.matchId].Count == 0;

        NetworkServer.AddPlayerForConnection(conn, playerObj);
        matchRooms[msg.matchId].Add(roomPlayer);

        conn.Send(new JoinResultMessage { success = true, matchId = msg.matchId, roomName = msg.roomName });

        BroadcastPlayerList(msg.matchId);
        SendSelectionsTo(conn, msg.matchId);
    }

    private void OnRoomListRequestMessageReceived(NetworkConnectionToClient conn, RoomListRequestMessage msg)
    {
        var roomInfos = matchRooms.Select(pair => new RoomInfo
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
        if (conn.identity && conn.identity.TryGetComponent(out RoomPlayer roomP))
        {
            string matchId = roomP.matchId;
            if (matchRooms.TryGetValue(matchId, out var list))
            {
                list.Remove(roomP);
                if (list.Count == 0) matchRooms.Remove(matchId);
                else if (roomP.isLeader) list[0].isLeader = true;

                SendRoomListToAllClients();
                BroadcastPlayerList(matchId);
            }
        }
        else if (conn.identity && conn.identity.TryGetComponent(out PlayerStats ps))
        {
            var nmComp = ps.GetComponent<NetworkMatch>();
            if (nmComp != null)
            {
                var guid = nmComp.matchId;
                bool anyConnInThisMatch = NetworkServer.connections.Values
                    .Any(c =>
                    {
                        var id = c?.identity;
                        return id && id.TryGetComponent(out PlayerStats p2) &&
                               p2.GetComponent<NetworkMatch>()?.matchId == guid;
                    });

                if (!anyConnInThisMatch)
                {
                    DespawnMatchObjects(guid);
                    RemoveMatch(guid);
                }
            }
        }

        base.OnServerDisconnect(conn);
    }

    public void StartGame(string matchId)
    {
        if (!Guid.TryParse(matchId, out var guid))
        {
            Debug.LogError($"[서버] matchId 파싱 실패: {matchId}");
            return;
        }

        if (MatchManager.ActiveMatches.ContainsKey(guid))
        {
            Debug.LogWarning($"[서버] StartGame 무시: 이미 MatchManager가 존재함 ({guid})");
            return;
        }

        if (pendingMatches.ContainsKey(guid))
        {
            Debug.LogWarning($"[서버] StartGame 무시: 이미 pendingMatches에 등록됨 ({guid})");
            return;
        }

        if (!matchRooms.TryGetValue(matchId, out var players) || players.Count == 0)
        {
            Debug.LogError($"[서버] 매치 ID({matchId})에 해당하는 방이 없거나 비어 있음");
            return;
        }

        if (availableStartPoints == null || availableStartPoints.Count == 0)
        {
            Debug.LogError("[서버] 새 매치를 생성할 비어있는 시작 지점이 없습니다!");
            return;
        }

        var startPoint = availableStartPoints[0];
        availableStartPoints.RemoveAt(0);

        pendingMatches[guid] = new List<RoomPlayer>(players);
        matchStartPointMap[guid] = startPoint;

        var matchInstance = Instantiate(matchManagerPrefabs, startPoint.position, startPoint.rotation);
        var mm = matchInstance.GetComponent<MatchManager>();
        mm.startPoint = startPoint;
        matchInstance.GetComponent<NetworkMatch>().matchId = guid;
        NetworkServer.Spawn(matchInstance);
    }

    [Server]
    private void HandleMatchEnded(Guid matchGuid, bool isVictory)
    {
        string matchIdStr = matchGuid.ToString();

        foreach (var ni in NetworkServer.spawned.Values.ToList())
        {
            if (!ni) continue;
            var nm = ni.GetComponent<NetworkMatch>();
            if (nm != null && nm.matchId == matchGuid)
                NetworkServer.Destroy(ni.gameObject);
        }

        if (matchStartPointMap.TryGetValue(matchGuid, out var point))
        {
            FreeUpMatchPoint(point);
            matchStartPointMap.Remove(matchGuid);
        }

        finishedMatches.Remove(matchIdStr);
        matchRooms.Remove(matchIdStr);

        SendRoomListToAllClients();
        Debug.Log($"[서버] Match {matchGuid} 종료 정리 완료 (Victory={isVictory}).");
    }

    [Server]
    private void HandleManagerReady(MatchManager readyManager)
    {
        var matchId = readyManager.GetComponent<NetworkMatch>().matchId;
        if (!pendingMatches.TryGetValue(matchId, out var playersToSpawn))
            return;

        Debug.Log($"[서버] HandleManagerReady -> match {matchId} 준비. {playersToSpawn.Count}명 스폰 예정");

        var newPlayerTransforms = new List<Transform>();
        Vector3 baseSpawnPos = readyManager.startPoint != null ? readyManager.startPoint.position : Vector3.zero;

        for (int i = 0; i < playersToSpawn.Count; i++)
        {
            var roomPlayer = playersToSpawn[i];
            if (roomPlayer == null) continue;

            var conn = roomPlayer.connectionToClient;
            int charIndex = roomPlayer.selectedCharacter;

            if (charIndex < 0 || charIndex >= characterPrefabs.Length)
            {
                Debug.LogError($"[서버] 잘못된 캐릭터 인덱스: {charIndex}");
                continue;
            }

            var playerPrefab = characterPrefabs[charIndex];
            if (playerPrefab == null)
            {
                Debug.LogError($"[서버] characterPrefabs[{charIndex}]가 null임");
                continue;
            }

            float spreadRadius = 1.0f;
            float angleRad = (i * 40f) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * spreadRadius;
            Vector3 spawnHere = baseSpawnPos + offset;
            spawnHere.y = baseSpawnPos.y;

            var playerObj = Instantiate(playerPrefab, spawnHere, Quaternion.identity);
            playerObj.GetComponent<NetworkMatch>().matchId = matchId;

            var stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.ServerResetAllStats();
                stats.ServerSetNickname(roomPlayer.playerName);   // ★★★ 핵심: Replace 전에 서버 권위로 닉 확정
            }

            roomPlayer.TargetStartGame(conn, charIndex, matchId.ToString());

            NetworkServer.Destroy(roomPlayer.gameObject);
            NetworkServer.ReplacePlayerForConnection(conn, playerObj, new ReplacePlayerOptions());

            if (stats != null)
                stats.TargetBindHUD(conn);

            newPlayerTransforms.Add(playerObj.transform);
        }

        readyManager.StartMatchWithPlayers(newPlayerTransforms);

        pendingMatches.Remove(matchId);

        var key = matchId.ToString();
        if (matchRooms.ContainsKey(key))
        {
            matchRooms.Remove(key);
            SendRoomListToAllClients();
        }

        if (missionManagerPrefab != null)
        {
            var mmObj = Instantiate(missionManagerPrefab);
            var mmMatch = mmObj.GetComponent<NetworkMatch>();
            if (mmMatch != null) mmMatch.matchId = matchId;
            NetworkServer.Spawn(mmObj);
            Debug.Log($"[서버] MissionManager 스폰 완료 (matchId={matchId})");
        }
        else
        {
            Debug.LogWarning("[서버] missionManagerPrefab 미지정 (선택 사항)");
        }
    }

    [Server]
    private List<PlayerStats> GetMatchPlayers(string matchIdStr)
    {
        var list = new List<PlayerStats>();
        if (!Guid.TryParse(matchIdStr, out var guid))
        {
            Debug.LogError($"[DeadCheck] Guid.Parse 실패: '{matchIdStr}'");
            return list;
        }

        foreach (var ni in NetworkServer.spawned.Values)
        {
            if (!ni) continue;
            var ps = ni.GetComponent<PlayerStats>();
            if (ps == null) continue;

            var nm = ni.GetComponent<NetworkMatch>();
            if (nm != null && nm.matchId == guid)
                list.Add(ps);
        }

        Debug.Log($"[DeadCheck] match={guid} players={list.Count} :: " +
                  string.Join(", ", list.Select(p => $"{p.Nickname}/{p.isAlive}")));

        return list;
    }


    //[Server]
    //public void ServerNotifyPlayerDead(string matchIdStr)
    //{
    //    if (string.IsNullOrEmpty(matchIdStr)) return;
    //    if (finishedMatches.Contains(matchIdStr)) return; // 이미 끝난 매치면 무시

    //    var players = GetMatchPlayers(matchIdStr);
    //    bool anyAlive = players.Any(p => p != null && p.isAlive);
    //    int aliveCount = players.Count(p => p != null && p.isAlive);
    //    if (aliveCount > 0) return; // ← 아직 살아있으면 종료 NO
    //    if (!anyAlive)
    //    {
    //        finishedMatches.Add(matchIdStr);

    //        foreach (var ps in players)
    //        {
    //            var conn = ps?.connectionToClient;
    //            if (conn != null) ps.TargetShowDefeat(conn); // PlayerStats.TargetRpc
    //        }

    //        // TODO: 매치 정리/리셋 필요 시 여기에
    //        // availableStartPoints.Add(해당 시작점) 등
    //    }
    //    if (Guid.TryParse(matchIdStr, out var g) && MatchManager.ActiveMatches.TryGetValue(g, out var mm))
    //    {
    //        mm.EndMatch(false);  // 패배 종료 → HandleMatchEnded로 정리
    //    }
    //}

    public void BroadcastPlayerList(string matchId)
    {
        if (!matchRooms.ContainsKey(matchId)) return;

        foreach (var player in matchRooms[matchId])
        {
            var infoList = matchRooms[matchId].Select(p => new RoomPlayer.PlayerInfo
            {
                name = p.playerName,                // ★ 서버 SyncVar 값 사용
                isLeader = p.isLeader,
                isMe = p == player
            }).ToList();

            player.TargetRebuildPlayerList(infoList);
        }
    }

    private (int[] selected, string[] names) BuildSelectionSnapshot(string matchId)
    {
        if (!matchRooms.TryGetValue(matchId, out var list) || list == null)
            return (Array.Empty<int>(), Array.Empty<string>());

        var players = list
            .Where(p => p != null && p.connectionToClient != null)
            .OrderBy(p => p.connectionToClient.connectionId)
            .ToArray();

        int[] selected = new int[players.Length];
        string[] names = new string[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            selected[i] = players[i].selectedCharacter;
            names[i] = players[i].playerName;      // ★ 여기도 동일
        }
        return (selected, names);
    }

    public void SendSelectionsTo(NetworkConnectionToClient conn, string matchId)
    {
        var (selected, names) = BuildSelectionSnapshot(matchId);
        var rp = conn?.identity ? conn.identity.GetComponent<RoomPlayer>() : null;
        if (rp == null) return;
        rp.TargetUpdateCharacterButtons(conn, matchId, selected, names);
        Debug.Log($"[Server→Target] snapshot to conn={conn.connectionId} matchId={matchId}");
    }

    public void BroadcastSelections(string matchId)
    {
        if (!matchRooms.TryGetValue(matchId, out var list) || list == null) return;

        var (selected, names) = BuildSelectionSnapshot(matchId);
        foreach (var rp in list)
        {
            var c = rp.connectionToClient;
            if (c != null)
                rp.TargetUpdateCharacterButtons(c, matchId, selected, names);
        }
        Debug.Log($"[Server→All] broadcast matchId={matchId}");
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

    [Server]
    public void FreeUpMatchPoint(Transform pointToFree)
    {
        if (pointToFree != null && availableStartPoints != null && !availableStartPoints.Contains(pointToFree))
        {
            availableStartPoints.Add(pointToFree);
            Debug.Log($"[서버] StartPoint 반환됨: {pointToFree.name}");
        }
    }

    [Server]
    public void RemoveMatch(System.Guid matchGuid)
    {
        string key = matchGuid.ToString();
        if (matchRooms.Remove(key))
        {
            Debug.Log($"[서버] matchRooms에서 {key} 제거");
            SendRoomListToAllClients();
        }
    }

    [Server]
    public void DespawnMatchObjects(System.Guid matchGuid)
    {
        var toDestroy = new List<NetworkIdentity>();
        foreach (var ni in NetworkServer.spawned.Values)
        {
            if (!ni) continue;
            var nm = ni.GetComponent<NetworkMatch>();
            if (nm != null && nm.matchId == matchGuid)
                toDestroy.Add(ni);
        }

        foreach (var ni in toDestroy)
            NetworkServer.Destroy(ni.gameObject);

        Debug.Log($"[서버] matchId={matchGuid} 오브젝트 {toDestroy.Count}개 정리 완료");
    }
}
