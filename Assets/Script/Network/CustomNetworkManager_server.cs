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
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();

    [Header("매치 생성 설정")]
    public GameObject matchManagerPrefabs;
    public List<Transform> matchStartPoints;
    private List<Transform> availableStartPoints;
    private Dictionary<Guid, List<RoomPlayer>> pendingMatches = new Dictionary<Guid, List<RoomPlayer>>();

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

        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
        MatchManager.OnManagerReady -= HandleManagerReady;
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

        // 사용 가능한 시작 지점이 있는지 확인합니다.
        if (availableStartPoints.Count == 0)
        {
            Debug.LogError("[서버] 새 매치를 생성할 비어있는 시작 지점이 없습니다!");
            // TODO: 플레이어에게 서버가 꽉 찼다고 알리는 로직
            return;
        }

        // 비어있는 위치 중 하나를 선택하고 목록에서 '사용 중'으로 변경합니다.
        Transform startPoint = availableStartPoints[0];
        availableStartPoints.RemoveAt(0);

        pendingMatches[parsedMatchId] = new List<RoomPlayer>(players);

        // 선택된 위치에 'MatchManager' 프리팹을 생성합니다.
        GameObject matchInstance = Instantiate(matchManagerPrefabs, startPoint.position, startPoint.rotation);
        matchInstance.GetComponent<NetworkMatch>().matchId = parsedMatchId;
        NetworkServer.Spawn(matchInstance);
    }


    // MatchManager가 준비되었다는 신호를 받았을 때 실행될 메서드
    [Server]
    private void HandleManagerReady(MatchManager readyManager)
    {
        Guid matchId = readyManager.GetComponent<NetworkMatch>().matchId;
        if (!pendingMatches.TryGetValue(matchId, out var playersToSpawn)) return;

        Debug.Log($"Manager for match [{matchId}] is ready. Spawning {playersToSpawn.Count} players...");

        List<Transform> newPlayerTransforms = new List<Transform>();

        // NetworkManager는 플레이어 생성(RoomPlayer -> InGame Player)까지만 담당합니다.
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

            GameObject playerPrefab = characterPrefabs[index];
            if (playerPrefab == null)
            {
                Debug.LogError($"[서버] characterPrefabs[{index}]가 null임");
                continue;
            }

            GameObject playerObj = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            playerObj.GetComponent<NetworkMatch>().matchId = matchId;
            
            var stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.NickName = roomPlayer.playerName;
            }

            // 미리 게임 시작 알림
            roomPlayer.TargetStartGame(conn, index, matchId.ToString());
            Debug.Log($"[서버] TargetStartGame 호출 완료");

            // 기존 RoomPlayer 제거 → ReplacePlayer 순서 중요!
            NetworkServer.Destroy(roomPlayer.gameObject);
            NetworkServer.ReplacePlayerForConnection(conn, playerObj, new ReplacePlayerOptions());

            newPlayerTransforms.Add(playerObj.transform);
        }

        // 생성된 플레이어 목록을 MatchManager에게 넘겨주며 모든 책임을 위임합니다.
        readyManager.StartMatchWithPlayers(newPlayerTransforms);

        // 대기열에서 제거
        pendingMatches.Remove(matchId);

        if (missionManagerPrefab != null)
        {
            var mmObj = Instantiate(missionManagerPrefab);

            // 매치 분리(InterestManagement)를 쓰고 있으니 같은 matchId 부여
            var mmMatch = mmObj.GetComponent<NetworkMatch>();
            if (mmMatch != null) mmMatch.matchId = matchId;

            NetworkServer.Spawn(mmObj);
            Debug.Log($"[서버] MissionManager 스폰 완료 (matchId={matchId})");
        }
        else
        {
            Debug.LogError("[서버] missionManagerPrefab 이 비어있습니다. 인스펙터에 연결하세요.");
        }
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

    // 매치가 끝나면 MatchManager가 이 메서드를 호출하여 위치를 반납합니다.
    [Server]
    public void FreeUpMatchPoint(Transform pointToFree)
    {
        if (pointToFree != null && !availableStartPoints.Contains(pointToFree))
        {
            availableStartPoints.Add(pointToFree);
            Debug.Log($"Point {pointToFree.name} at {pointToFree.position} is now available.");
        }
    }
}
