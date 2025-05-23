using UnityEngine;
using Mirror;

public class JoinButtonHandler : MonoBehaviour
{
    // �ܺο��� matchId�� ������ �� �ְ� static �޼��� ����
    public static void JoinWithMatchId(string matchId)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            CustomNetworkManager.matchIdToJoin = matchId;
            NetworkManager.singleton.StartClient();
            Debug.Log($"[JoinButtonHandler] Ŭ���̾�Ʈ�� matchId ����: {matchId}");
        }
        else
        {
            Debug.LogWarning("[JoinButtonHandler] �̹� ��Ʈ��ũ�� ���� ���Դϴ�.");
        }
    }
}
