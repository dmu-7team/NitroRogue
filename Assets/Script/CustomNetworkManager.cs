using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CustomNetworkManager : NetworkManager
{
    // 모든 Match ID별 참가자 리스트
    public Dictionary<string, List<NetworkConnectionToClient>> matchRooms = new();

    // 클라이언트가 접속 전에 설정하는 Match ID
    public static string matchIdToJoin = "";

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string matchId = matchIdToJoin;

        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[CustomNetworkManager] Match ID가 비어 있습니다.");
            matchId = "default";
        }

        if (!matchRooms.ContainsKey(matchId))
        {
            matchRooms[matchId] = new List<NetworkConnectionToClient>();
        }

        // 프리팹 인스턴스 생성
        GameObject player = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, player);

        // 플레이어에게 Match ID 전달
        RoomPlayer rp = player.GetComponent<RoomPlayer>();
        if (rp != null)
        {
            rp.matchId = matchId;
        }

        matchRooms[matchId].Add(conn);

        Debug.Log($"[CustomNetworkManager] Player joined match {matchId}. 현재 인원: {matchRooms[matchId].Count}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // 나간 사람 MatchID 찾고 제거
        foreach (var kvp in matchRooms)
        {
            if (kvp.Value.Contains(conn))
            {
                kvp.Value.Remove(conn);
                Debug.Log($"[CustomNetworkManager] Player left match {kvp.Key}. 현재 인원: {kvp.Value.Count}");

                if (kvp.Value.Count == 0)
                {
                    matchRooms.Remove(kvp.Key);
                    Debug.Log($"[CustomNetworkManager] Match {kvp.Key} 삭제됨.");
                }

                break;
            }
        }

        base.OnServerDisconnect(conn);
    }
}
