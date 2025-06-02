using Mirror;
using UnityEngine;
using System.Collections.Generic;
using NetworkMessages;

public class CustomNetworkManager_Server : NetworkManager
{
    public Dictionary<string, List<RoomPlayer>> matchRooms = new();

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

        NetworkServer.RegisterHandler<JoinMatchMessage>(OnJoinMatchMessageReceived);
        NetworkServer.RegisterHandler<RoomListRequestMessage>(OnRoomListRequestMessageReceived);

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

        foreach (var player in matchRooms[matchId])
        {
            player.TargetLoadGameScene();
        }
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
