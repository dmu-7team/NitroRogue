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
            try
            {
                match.matchId = new System.Guid(matchId);
                Debug.Log($"[RoomPlayer] 서버에서 Match ID 설정됨: {matchId}");
            }
            catch (System.FormatException)
            {
                Debug.LogError($"[RoomPlayer] 잘못된 Match ID 형식: {matchId}");
            }
        }
    }
}
