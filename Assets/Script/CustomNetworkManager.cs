using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;

public struct EmptyMessage : NetworkMessage { }

public class CustomNetworkManager : NetworkManager
{
    public static string matchIdToJoin;

    private Dictionary<string, List<NetworkConnection>> matchRooms = new();
    private List<NetworkConnectionToClient> connectedClients = new();

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<EmptyMessage>(OnRefreshRoomList);
    }

    private void OnRefreshRoomList(NetworkConnectionToClient conn, EmptyMessage msg)
    {
        Debug.Log("[서버] 클라이언트 요청으로 방 리스트 전송");
        SendRoomListToClient(conn);
    }

    private void SendRoomListToClient(NetworkConnection conn)
    {
        if (conn == null || conn.identity == null)
        {
            Debug.LogWarning("RoomListSync 전송 실패: conn 또는 identity 없음");
            return;
        }

        List<RoomInfo> roomList = new();
        foreach (var match in matchRooms)
        {
            if (match.Value.Count > 0)
            {
                RoomInfo info = new()
                {
                    matchId = match.Key,
                    currentPlayers = match.Value.Count,
                    maxPlayers = maxConnections
                };
                roomList.Add(info);
            }
        }

     
    }

    private void SendRoomListToAll()
    {
        Debug.Log("[서버] 방 리스트 전체 전송 시도");

        foreach (var conn in connectedClients)
        {
            SendRoomListToClient(conn);
        }
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        connectedClients.Add(conn);
        Debug.Log($"[CustomNetworkManager] 연결됨: {conn.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        connectedClients.Remove(conn);
        foreach (var match in matchRooms)
        {
            match.Value.Remove(conn);
        }

        SendRoomListToAll();
        base.OnServerDisconnect(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[ServerAddPlayer] matchIdToJoin: {matchIdToJoin}, Connection ID: {conn.connectionId}");

        GameObject player = Instantiate(playerPrefab);
        RoomPlayer roomPlayer = player.GetComponent<RoomPlayer>();

        if (string.IsNullOrEmpty(matchIdToJoin))
        {
            Debug.LogWarning("[RoomPlayer] matchId가 유효하지 않아 기본값으로 설정됨.");
            matchIdToJoin = Guid.NewGuid().ToString();
        }

        roomPlayer.matchId = matchIdToJoin;

        if (!matchRooms.ContainsKey(matchIdToJoin))
            matchRooms[matchIdToJoin] = new List<NetworkConnection>();

        matchRooms[matchIdToJoin].Add(conn);

        NetworkServer.AddPlayerForConnection(conn, player);

        Debug.Log($"[CustomNetworkManager] Match {matchIdToJoin}에 플레이어 추가됨");
        SendRoomListToAll();
    }

    public void StartClientWithCustomPort()
    {
        StartClient();
    }

    // 필요에 따라 추가 함수 넣기
}
