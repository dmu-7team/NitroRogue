using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager
{
    // ��� Match ID�� ������ ����Ʈ
    public Dictionary<string, List<NetworkConnectionToClient>> matchRooms = new();

    // Ŭ���̾�Ʈ�� ���� ���� �����ϴ� Match ID
    public static string matchIdToJoin = "";

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string matchId = matchIdToJoin;

        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[CustomNetworkManager] Match ID�� ��� �ֽ��ϴ�.");
            matchId = "default";
        }

        if (!matchRooms.ContainsKey(matchId))
        {
            matchRooms[matchId] = new List<NetworkConnectionToClient>();
        }

        // ������ �ν��Ͻ� ����
        GameObject player = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, player);

        // �÷��̾�� Match ID ����
        RoomPlayer rp = player.GetComponent<RoomPlayer>();
        if (rp != null)
        {
            rp.matchId = matchId;
        }

        matchRooms[matchId].Add(conn);

        Debug.Log($"[CustomNetworkManager] Player joined match {matchId}. ���� �ο�: {matchRooms[matchId].Count}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // ���� ��� MatchID ã�� ����
        foreach (var kvp in matchRooms)
        {
            if (kvp.Value.Contains(conn))
            {
                kvp.Value.Remove(conn);
                Debug.Log($"[CustomNetworkManager] Player left match {kvp.Key}. ���� �ο�: {kvp.Value.Count}");

                if (kvp.Value.Count == 0)
                {
                    matchRooms.Remove(kvp.Key);
                    Debug.Log($"[CustomNetworkManager] Match {kvp.Key} ������.");
                }

                break;
            }
        }

        base.OnServerDisconnect(conn);
    }
}
