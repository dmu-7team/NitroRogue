using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{


    public static string matchIdToJoin;

    // 서버에서 관리할 Room 정보
    public Dictionary<string, List<NetworkConnection>> matchRooms = new();
    public List<NetworkConnectionToClient> connectedClients = new();

    // 서버 전용 실행
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[서버] Dedicated Server 시작됨");

        // 서버가 시작되면 방 정보 초기화
        matchRooms.Clear();
        connectedClients.Clear();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        connectedClients.Add(conn);
        Debug.Log($"[서버] 클라이언트 접속됨: {conn.address}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GameObject player = Instantiate(playerPrefab);
        RoomPlayer roomPlayer = player.GetComponent<RoomPlayer>();

        if (!string.IsNullOrEmpty(matchIdToJoin))
        {
            roomPlayer.matchId = matchIdToJoin;
            roomPlayer.roomName = "Room_" + matchIdToJoin[..4];

            var match = player.GetComponent<NetworkMatch>();
            if (Guid.TryParse(matchIdToJoin, out var guid))
            {
                match.matchId = guid;
            }

            if (!matchRooms.ContainsKey(matchIdToJoin))
            {
                matchRooms[matchIdToJoin] = new List<NetworkConnection>();
            }
            matchRooms[matchIdToJoin].Add(conn);

            Debug.Log($"[서버] {conn.connectionId} → Match {matchIdToJoin} 입장");

            // 여기 추가: 현재 씬이 Room이 아니라면 전환
            if (SceneManager.GetActiveScene().name != "Room")
            {
                Debug.Log("[서버] Room 씬으로 전환 시도");
                ServerChangeScene("Room");
            }
        }

        NetworkServer.AddPlayerForConnection(conn, player);
    }





    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);

        foreach (var kvp in matchRooms)
        {
            if (kvp.Value.Contains(conn))
            {
                kvp.Value.Remove(conn);
                Debug.Log($"[서버] {conn.connectionId} → Match {kvp.Key}에서 제거됨");

                if (kvp.Value.Count == 0)
                {
                    Debug.Log($"[서버] Match {kvp.Key} 비었음 → 정리 대상");
                    // 선택적으로 matchRooms에서 완전히 삭제
                    matchRooms.Remove(kvp.Key);
                }

                break;
            }
        }
    }

    public void CheckIfAllReady(string matchId)
    {
        if (!matchRooms.ContainsKey(matchId)) return;

        var connList = matchRooms[matchId];
        if (connList.Count < 2)
        {
            Debug.Log($"[서버] Match {matchId} 인원이 부족해서 게임 시작 안됨 ({connList.Count}명)");
            return; // 최소 인원 조건
        }

        foreach (var conn in connList)
        {
            if (conn.identity == null) return;

            var player = conn.identity.GetComponent<RoomPlayer>();
            if (player == null || !player.isReady)
            {
                Debug.Log($"[서버] Match {matchId} → 아직 Ready 아닌 인원 있음");
                return;
            }
        }

        Debug.Log($"[서버] Match {matchId} → 모든 인원이 Ready 상태 → 게임 시작!");
        StartGameForMatch(matchId);
    }

    public void StartGameForMatch(string matchId)
    {
        if (!matchRooms.ContainsKey(matchId)) return;

        foreach (var conn in matchRooms[matchId])
        {
            if (conn.identity != null)
            {
                var player = conn.identity.GetComponent<RoomPlayer>();
                player.TargetStartGame();
            }
        }
    }


    // 클라이언트 접속 준비
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[클라이언트] 서버에 연결됨");

        // 서버에 matchId 보내기 (필요 시 메시지로 요청 가능)
        if (!string.IsNullOrEmpty(matchIdToJoin))
        {
            Debug.Log($"[클라이언트] 요청하는 Match ID: {matchIdToJoin}");
        }
    }
}
