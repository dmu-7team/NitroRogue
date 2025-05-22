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
                Debug.LogWarning($"[RoomPlayer] matchId�� ��ȿ���� �ʾ� �⺻������ ������: {matchId}");
            }
            match.matchId = System.Guid.Parse(matchId);
            Debug.Log($"[RoomPlayer] Match ID ������: {matchId}, isServer: {isServer}");
        }
        else
        {
            Debug.LogError("[RoomPlayer] NetworkMatch ������Ʈ�� ã�� �� �����ϴ�.");
        }
    }
}