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
                Debug.Log($"[RoomPlayer] �������� Match ID ������: {matchId}");
            }
            catch (System.FormatException)
            {
                Debug.LogError($"[RoomPlayer] �߸��� Match ID ����: {matchId}");
            }
        }
    }
}
