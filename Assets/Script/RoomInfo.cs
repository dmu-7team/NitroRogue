using System;

[Serializable]
public class RoomInfo
{
    public string matchId;
    public int currentPlayers; // 속성 이름은 currentPlayers로 유지
    public int maxPlayers;

    public RoomInfo() { }

    public RoomInfo(string matchId, int currentPlayers, int maxPlayers)
    {
        this.matchId = matchId;
        this.currentPlayers = currentPlayers;
        this.maxPlayers = maxPlayers;
    }
}