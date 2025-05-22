using System;

[Serializable]
public class RoomInfo
{
    public string matchId;
    public int currentPlayers; // �Ӽ� �̸��� currentPlayers�� ����
    public int maxPlayers;

    public RoomInfo() { }

    public RoomInfo(string matchId, int currentPlayers, int maxPlayers)
    {
        this.matchId = matchId;
        this.currentPlayers = currentPlayers;
        this.maxPlayers = maxPlayers;
    }
}