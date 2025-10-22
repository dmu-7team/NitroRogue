// NetworkMessages.cs
using Mirror;
using System;
using System.Collections.Generic;

namespace NetworkMessages
{
    [Serializable]
    public struct JoinMatchMessage : NetworkMessage
    {
        public string matchId;
        public string roomName;
        public string nickname;
    }

    [Serializable]
    public struct JoinResultMessage : NetworkMessage
    {
        public bool success;
        public string matchId;
        public string roomName;
    }

    [Serializable]
    public struct RoomListRequestMessage : NetworkMessage { }

    [Serializable]
    public struct RoomListSyncMessage : NetworkMessage
    {
        public List<RoomInfo> roomList;
    }

    [Serializable]
    public struct RoomInfo
    {
        public string matchId;
        public string roomName;
        public int currentPlayers;
        public int maxPlayers;
    }
}
