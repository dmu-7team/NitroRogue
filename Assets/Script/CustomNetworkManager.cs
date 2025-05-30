using System.Collections.Generic;
using UnityEngine;
using Mirror;
using NetworkMessages; // 메시지 네임스페이스 사용

public class CustomNetworkManager : NetworkManager
{
    // 방 ID와 플레이어 목록 매핑
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();
    public static string matchIdToJoin; // 원하는 방에 Join할 때 사용하는 matchId


    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<JoinResultMessage>(OnJoinResultMessageReceived);
        NetworkClient.RegisterHandler<RoomListSyncMessage>(msg => RoomListUI.Instance.OnRoomListSyncMessageReceived(msg));
    }

    private void OnJoinMatchMessageReceived(NetworkConnectionToClient conn, JoinMatchMessage msg)
    {
        if (!matchRooms.ContainsKey(msg.matchId))
        {
            matchRooms[msg.matchId] = new List<RoomPlayer>();
        }

        GameObject playerObj = Instantiate(playerPrefab);
        RoomPlayer roomPlayer = playerObj.GetComponent<RoomPlayer>();

        roomPlayer.matchId = msg.matchId;
        roomPlayer.roomName = msg.roomName;
        roomPlayer.maxPlayers = 4;
        roomPlayer.currentPlayers = matchRooms[msg.matchId].Count + 1;

        NetworkServer.AddPlayerForConnection(conn, playerObj);
        matchRooms[msg.matchId].Add(roomPlayer);

        JoinResultMessage result = new()
        {
            success = true,
            matchId = msg.matchId,
            roomName = msg.roomName
        };
        conn.Send(result);
    }

    private void OnJoinResultMessageReceived(JoinResultMessage msg)
    {
        if (msg.success)
            Debug.Log($"[Client] 방 참가 성공: {msg.roomName} ({msg.matchId})");
        else
            Debug.LogWarning("[Client] 방 참가 실패");
    }

    private void OnRoomListRequestMessageReceived(NetworkConnectionToClient conn, RoomListRequestMessage msg)
    {
        List<RoomInfo> roomInfos = new();

        foreach (var pair in matchRooms)
        {
            if (pair.Value.Count == 0) continue;

            RoomPlayer rp = pair.Value[0];
            roomInfos.Add(new RoomInfo
            {
                matchId = rp.matchId,
                roomName = rp.roomName,
                currentPlayers = pair.Value.Count,
                maxPlayers = rp.maxPlayers
            });
        }

        RoomListSyncMessage syncMsg = new RoomListSyncMessage { roomList = roomInfos };
        conn.Send(syncMsg);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null)
        {
            RoomPlayer player = conn.identity.GetComponent<RoomPlayer>();
            if (matchRooms.ContainsKey(player.matchId))
            {
                matchRooms[player.matchId].Remove(player);
                if (matchRooms[player.matchId].Count == 0)
                    matchRooms.Remove(player.matchId);
            }
        }

        base.OnServerDisconnect(conn);
    }
}
