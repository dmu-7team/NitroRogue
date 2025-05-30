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

        if (!matchRooms.ContainsKey(msg.matchId))
        {
            matchRooms[msg.matchId] = new List<RoomPlayer>();
        }

        //  RoomPlayer 생성 및 연결
        GameObject playerObj = Instantiate(playerPrefab);
        RoomPlayer roomPlayer = playerObj.GetComponent<RoomPlayer>();

        if (roomPlayer != null)
        {
            roomPlayer.roomName = msg.roomName;
            roomPlayer.matchId = msg.matchId;

            NetworkServer.AddPlayerForConnection(conn, playerObj);

            matchRooms[msg.matchId].Add(roomPlayer); //  이거 추가해야 currentPlayers가 올라감
        }

        JoinResultMessage result = new()
        {
            success = true,
            matchId = msg.matchId,
            roomName = msg.roomName
        };
        conn.Send(result);

        Debug.Log($"[서버] JoinMatch 완료: {msg.matchId} / 클라에 응답 전송됨");
    }


    private void OnRoomListRequestMessageReceived(NetworkConnectionToClient conn, RoomListRequestMessage msg)
    {
        Debug.Log("[서버] 방 리스트 요청 수신됨");

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

        Debug.Log($"[서버] 방 리스트 전송됨: {roomInfos.Count}개");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log($"[서버] 클라이언트 연결 해제됨: {conn.connectionId}");
        // 여기선 Player 제거 안 해도 됨 (없으니까)
    }
}
