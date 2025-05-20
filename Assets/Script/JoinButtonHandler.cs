using System;
using UnityEngine;
using Mirror;

public class JoinButtonHandler : MonoBehaviour
{
    public void OnClickJoin()
    {
        string matchId = Guid.NewGuid().ToString();
        CustomNetworkManager.matchIdToJoin = matchId;

        Debug.Log($"[JoinButton] �ڵ� ������ Match ID: {matchId}");

        if (!NetworkClient.active && !NetworkServer.active)
        {
            Debug.Log("[JoinButton] Host ����");
            NetworkManager.singleton.StartHost(); // �� ��ȯ ���Ե�
        }
        else if (!NetworkClient.active)
        {
            Debug.Log("[JoinButton] Client ����");
            NetworkManager.singleton.StartClient(); // �� ��ȯ ����
        }
    }
}
