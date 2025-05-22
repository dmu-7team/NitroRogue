using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class RoomListSync : NetworkBehaviour
{
    [TargetRpc]
    public void TargetReceiveRoomList(NetworkConnection conn, List<RoomInfo> roomList)
    {
        Debug.Log($"[RoomListSync] �� ����Ʈ ����: {roomList.Count} �� ��");
        foreach (var room in roomList)
        {
            Debug.Log($"Room: {room.matchId}, Players: {room.currentPlayers}/{room.maxPlayers}");
        }

        // UI ����
        if (RoomListUI.Instance != null)
        {
            RoomListUI.Instance.RenderRoomList(roomList);
        }
    }
}