using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class RoomListSync : NetworkBehaviour
{
    [TargetRpc]
    public void TargetReceiveRoomList(NetworkConnection conn, List<RoomInfo> roomList)
    {
        Debug.Log($"[RoomListSync] 방 리스트 수신: {roomList.Count} 개 방");
        foreach (var room in roomList)
        {
            Debug.Log($"Room: {room.matchId}, Players: {room.currentPlayers}/{room.maxPlayers}");
        }

        // UI 갱신
        if (RoomListUI.Instance != null)
        {
            RoomListUI.Instance.RenderRoomList(roomList);
        }
    }
}