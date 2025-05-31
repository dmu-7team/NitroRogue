using System.Collections.Generic;
using UnityEngine;
using Mirror;
using NetworkMessages;
using UnityEditor.EditorTools;


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

        // 기존 플레이어 오브젝트가 존재하면 제거
        if (conn.identity != null)
        {
            Debug.LogWarning("[서버] 기존 플레이어 오브젝트 제거");
            NetworkServer.Destroy(conn.identity.gameObject);
        }

        if (!matchRooms.ContainsKey(msg.matchId))
        {
            matchRooms[msg.matchId] = new List<RoomPlayer>();
        }

        GameObject playerObj = Instantiate(playerPrefab);
        RoomPlayer roomPlayer = playerObj.GetComponent<RoomPlayer>();

        if (roomPlayer != null)
        {
            roomPlayer.roomName = msg.roomName;
            roomPlayer.matchId = msg.matchId;

            //  중복 방지: 이 전에 반드시 AddPlayerForConnection 하기
            NetworkServer.AddPlayerForConnection(conn, playerObj);

            matchRooms[msg.matchId].Add(roomPlayer); //  방 인원수 반영
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


    // 예시: CustomNetworkManager_Server.cs
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log($"[서버] 클라이언트 연결 해제됨: {conn.connectionId}");

        if (conn.identity != null)
        {
            var player = conn.identity.GetComponent<RoomPlayer>();
            if (player != null && matchRooms.ContainsKey(player.matchId))
            {
                matchRooms[player.matchId].Remove(player);

                Debug.Log($"[서버] 플레이어 퇴장 처리 완료: {player.matchId} / 남은 인원 {matchRooms[player.matchId].Count}");

                // 클라이언트들에게 최신 방 리스트 전송
                List<RoomInfo> roomInfos = new();

                foreach (var pair in matchRooms)
                {
                    if (pair.Value.Count > 0)
                    {
                        roomInfos.Add(new RoomInfo
                        {
                            matchId = pair.Key,
                            roomName = pair.Value[0].roomName,
                            currentPlayers = pair.Value.Count,
                            maxPlayers = 4
                        });
                    }
                }

                RoomListSyncMessage syncMsg = new RoomListSyncMessage { roomList = roomInfos };
                NetworkServer.SendToAll(syncMsg);
            }
        }
    }



}
