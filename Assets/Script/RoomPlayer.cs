using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkMatch))]
public class RoomPlayer : NetworkBehaviour
{
    [SyncVar]
    public string matchId;

    public override void OnStartServer()
    {
        base.OnStartServer();
        var match = GetComponent<NetworkMatch>();
        if (match != null)
        {
            if (string.IsNullOrEmpty(matchId) || !System.Guid.TryParse(matchId, out System.Guid validGuid))
            {
                matchId = System.Guid.NewGuid().ToString();
                Debug.LogWarning($"[RoomPlayer] matchId가 유효하지 않아 기본값으로 설정됨: {matchId}");
            }
            match.matchId = System.Guid.Parse(matchId);
            Debug.Log($"[RoomPlayer] Match ID 설정됨: {matchId}, isServer: {isServer}");
        }
        else
        {
            Debug.LogError("[RoomPlayer] NetworkMatch 컴포넌트를 찾을 수 없습니다.");
        }
    }
}