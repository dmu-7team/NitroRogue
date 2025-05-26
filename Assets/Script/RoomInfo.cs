using System;
using System.Collections.Generic;
using Mirror;

[Serializable]
public struct RoomInfo
{
    public string matchId;
    public string roomName;
    public int currentPlayers;
    public int maxPlayers;
}
