using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using NetworkMessages;

public class CustomNetworkManager_Server : NetworkManager
{
    [Header("Match-scoped managers")]
    [SerializeField] private GameObject missionManagerPrefab; // 선택: 매치별 매니저

    public GameObject[] characterPrefabs;     // 인게임 플레이어 프리팹들 (NetworkIdentity+NetworkMatch+PlayerStats 포함)
    public GameObject roomPlayerPrefab;     // 로비용 RoomPlayer 프리팹 (NetworkIdentity 포함)

    [Header("매치 생성 설정 (이벤트 방식)")]
    public GameObject matchManagerPrefabs;    // ★ MatchManager 프리팹 (NetworkIdentity+NetworkMatch+MatchManager 포함)
    public List<Transform> matchStartPoints;  // ★ 여러 매치가 동시에 열릴 경우, 맵이 놓일 시작 지점 목록

    public Dictionary<string, List<RoomPlayer>> matchRooms = new();

    // 내부 상태
    private List<Transform> availableStartPoints;
    private readonly Dictionary<Guid, List<RoomPlayer>> pendingMatches = new();
    private readonly HashSet<string> finishedMatches = new(); // 전원사망 1회 방송 방지
    private readonly Dictionary<Guid, Transform> matchStartPointMap = new();  // ★ 추가: 매치 시작 지점 기억
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
        MatchManager.OnManagerReady += HandleManagerReady; // ★ 이벤트 구독
        MatchManager.OnMatchEnded += HandleMatchEnded;
        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        MatchManager.OnManagerReady -= HandleManagerReady; // ★ 이벤트 해제
        MatchManager.OnMatchEnded -= HandleMatchEnded;
    }

    // ===== 로비 진입/리스트 =====
    private void OnJoinMatchMessageReceived(NetworkConnectionToClient conn, JoinMatchMessage msg)
    {
        Debug.Log($"[서버] JoinMatch 요청: {msg.matchId} / {msg.roomName}");

        if (conn.identity != null)
            NetworkServer.Destroy(conn.identity.gameObject);

        if (!matchRooms.ContainsKey(msg.matchId))
            matchRooms[msg.matchId] = new List<RoomPlayer>();

        var playerObj = Instantiate(roomPlayerPrefab);
        var roomPlayer = playerObj.GetComponent<RoomPlayer>();
        roomPlayer.matchId = msg.matchId;
        roomPlayer.roomName = msg.roomName;
        roomPlayer.playerName = $"플레이어{matchRooms[msg.matchId].Count + 1}";
        roomPlayer.isLeader = matchRooms[msg.matchId].Count == 0;

        NetworkServer.AddPlayerForConnection(conn, playerObj);
        matchRooms[msg.matchId].Add(roomPlayer);

        conn.Send(new JoinResultMessage { success = true, matchId = msg.matchId, roomName = msg.roomName });

        BroadcastPlayerList(msg.matchId);
        SendSelectionsTo(conn, msg.matchId); // 선택 UI 스냅샷
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
        string matchId = null;

        if (conn.identity != null)
        {
            var player = conn.identity.GetComponent<RoomPlayer>();
            if (player != null && matchRooms.ContainsKey(player.matchId))
            {
                matchId = player.matchId;
                matchRooms[matchId].Remove(player);
                if (matchRooms[matchId].Count == 0) matchRooms.Remove(matchId);
                else if (player.isLeader) matchRooms[matchId][0].isLeader = true;

                SendRoomListToAllClients();
                BroadcastPlayerList(matchId);
            }
        }

        base.OnServerDisconnect(conn);
    }

    // ===== 매치 시작(로비 리더가 호출) → MatchManager 프리팹 스폰 =====
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

        if (availableStartPoints == null || availableStartPoints.Count == 0)
        {
            Debug.LogError("[서버] 새 매치를 생성할 비어있는 시작 지점이 없습니다! (Inspector에 matchStartPoints 등록)");
            return;
        }

        // 비어있는 위치 하나 할당
        var startPoint = availableStartPoints[0];
        availableStartPoints.RemoveAt(0);

        pendingMatches[parsedMatchId] = new List<RoomPlayer>(players);
        // ★ 추가: 이 매치의 시작 지점 기록 → 종료 시 반납
        matchStartPointMap[parsedMatchId] = startPoint;

        // ★ MatchManager 프리팹 스폰 (여기서는 '맵 인스턴스' 자체만 스폰)
        var matchInstance = Instantiate(matchManagerPrefabs, startPoint.position, startPoint.rotation);
        matchInstance.GetComponent<NetworkMatch>().matchId = parsedMatchId;
        NetworkServer.Spawn(matchInstance);
    }
    [Server]
    private void HandleMatchEnded(Guid matchGuid, bool isVictory)
    {
        string matchIdStr = matchGuid.ToString();

        // 1) 해당 matchId의 네트워크 오브젝트 전부 제거
        foreach (var ni in NetworkServer.spawned.Values.ToList())
        {
            if (!ni) continue;
            var nm = ni.GetComponent<NetworkMatch>();
            if (nm != null && nm.matchId == matchGuid)
                NetworkServer.Destroy(ni.gameObject);
        }
        
        // 2) 시작 지점 반환
        if (matchStartPointMap.TryGetValue(matchGuid, out var point))
        {
            FreeUpMatchPoint(point);
            matchStartPointMap.Remove(matchGuid);
        }

        // 3) 내부 상태 리셋
        finishedMatches.Remove(matchIdStr);
        SendRoomListToAllClients(); // ★ 방 리스트 갱신 메시지 발송
        Debug.Log($"[서버] Match {matchGuid} 종료 정리 완료 (Victory={isVictory}).");
    }

    // ===== MatchManager가 “준비됨”을 알리면 실제 인게임 플레이어를 교체/스폰 =====
    [Server]
    private void HandleManagerReady(MatchManager readyManager)
    {
        var matchId = readyManager.GetComponent<NetworkMatch>().matchId;
        if (!pendingMatches.TryGetValue(matchId, out var playersToSpawn)) return;

        Debug.Log($"Manager for match [{matchId}] is ready. Spawning {playersToSpawn.Count} players...");

        var newPlayerTransforms = new List<Transform>();

        foreach (var roomPlayer in playersToSpawn)
        {
            if (roomPlayer == null) continue;

            var conn = roomPlayer.connectionToClient;
            int index = roomPlayer.selectedCharacter;

            if (index < 0 || index >= characterPrefabs.Length)
            {
                Debug.LogError($"[서버] 잘못된 캐릭터 인덱스: {index}");
                continue;
            }

            var playerPrefab = characterPrefabs[index];
            if (playerPrefab == null)
            {
                Debug.LogError($"[서버] characterPrefabs[{index}]가 null임");
                continue;
            }

            var playerObj = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            playerObj.GetComponent<NetworkMatch>().matchId = matchId;

            var stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.NickName = roomPlayer.playerName;
                stats.isAlive = true;               // 전원사망 판정에 사용
                stats.matchIdStr = matchId.ToString(); // 문자열 GUID
            }

            // 클라에 “게임 시작” 타겟 알림 (자기 캐릭터 인덱스/매치ID)
            roomPlayer.TargetStartGame(conn, index, matchId.ToString());

            // RoomPlayer 파괴 → 인게임 Player로 교체
            NetworkServer.Destroy(roomPlayer.gameObject);
            NetworkServer.ReplacePlayerForConnection(conn, playerObj, new ReplacePlayerOptions());

            newPlayerTransforms.Add(playerObj.transform);
        }

        // ★ 여기서 실제 매치(맵) 로직 시작
        readyManager.StartMatchWithPlayers(newPlayerTransforms);

        // 대기열 정리
        pendingMatches.Remove(matchId);

        // (선택) 미션 매니저 스폰
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

    // ===== 패배 브로드캐스트 (전원 사망 1회 판정) =====
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
            {
                list.Add(ps);
            }
        }

        // 진단 로그
        Debug.Log($"[DeadCheck] match={guid} players={list.Count} :: " +
                  string.Join(", ", list.Select(p => $"{p.NickName}/{p.isAlive}")));

        return list;
    }


    [Server]
    public void ServerNotifyPlayerDead(string matchIdStr)
    {
        if (string.IsNullOrEmpty(matchIdStr)) return;
        if (finishedMatches.Contains(matchIdStr)) return; // 이미 끝난 매치면 무시

        var players = GetMatchPlayers(matchIdStr);
        bool anyAlive = players.Any(p => p != null && p.isAlive);
        int aliveCount = players.Count(p => p != null && p.isAlive);
        if (aliveCount > 0) return; // ← 아직 살아있으면 종료 NO
        if (!anyAlive)
        {
            finishedMatches.Add(matchIdStr);

            foreach (var ps in players)
            {
                var conn = ps?.connectionToClient;
                if (conn != null) ps.TargetShowDefeat(conn); // PlayerStats.TargetRpc
            }

            // TODO: 매치 정리/리셋 필요 시 여기에
            // availableStartPoints.Add(해당 시작점) 등
        }
        if (Guid.TryParse(matchIdStr, out var g) && MatchManager.ActiveMatches.TryGetValue(g, out var mm))
        {
            mm.EndMatch(false);  // 패배 종료 → HandleMatchEnded로 정리
        }
    }

    // ===== 로비 UI 유틸 (선택) =====
    public void BroadcastPlayerList(string matchId)
    {
        if (!matchRooms.ContainsKey(matchId)) return;

        foreach (var player in matchRooms[matchId])
        {
            var infoList = matchRooms[matchId].Select(p => new RoomPlayer.PlayerInfo
            {
                name = p.playerName,
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
            names[i] = players[i].playerName;
        }
        return (selected, names);
    }

    public void SendSelectionsTo(NetworkConnectionToClient conn, string matchId)
    {
        var (selected, names) = BuildSelectionSnapshot(matchId);

        RoomPlayer rp = (conn != null && conn.identity != null)
            ? conn.identity.GetComponent<RoomPlayer>()
            : null;
        if (rp == null) return;

        rp.TargetUpdateCharacterButtons(conn, selected, names);
        Debug.Log($"[Server→Target] snapshot to conn={conn.connectionId} matchId={matchId} names={string.Join(", ", names)}");
    }

    public void BroadcastSelections(string matchId)
    {
        if (!matchRooms.TryGetValue(matchId, out var list) || list == null) return;

        var (selected, names) = BuildSelectionSnapshot(matchId);
        foreach (var rp in list)
        {
            var c = rp.connectionToClient;
            if (c != null) rp.TargetUpdateCharacterButtons(c, selected, names);
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

    // 매치가 끝나면 MatchManager가 호출해서 시작 지점 반환하는 용도(옵션)
    [Server]
    public void FreeUpMatchPoint(Transform pointToFree)
    {
        if (pointToFree != null && availableStartPoints != null && !availableStartPoints.Contains(pointToFree))
        {
            availableStartPoints.Add(pointToFree);
            Debug.Log($"[서버] StartPoint 반환됨: {pointToFree.name}");
        }
    }
}
