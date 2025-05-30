using System.Collections.Generic;
using UnityEngine;
using Mirror;
using NetworkMessages;

public class CustomNetworkManager_Server : NetworkManager
{
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();

    public override void Start()
    {
        base.Start();
        Debug.Log("[서버] Start() 호출됨");
        StartServer(); // 이거 안 부르면 아예 서버가 안 열림
    }


    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[서버] OnStartServer 진입 완료");

        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);
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
