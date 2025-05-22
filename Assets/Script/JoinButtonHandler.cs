using UnityEngine;
using Mirror;
using System;

public class JoinButtonHandler : MonoBehaviour
{
    public void OnClickJoin()
    {
        string matchId = Guid.NewGuid().ToString();
        CustomNetworkManager.matchIdToJoin = matchId;

        if (NetworkManager.singleton != null)
        {
            NetworkManager.singleton.StartClient();
            Debug.Log($"[JoinButton] �ڵ� ������ Match ID: {matchId}");
        }
        else
        {
            Debug.LogError("NetworkManager.singleton�� null�Դϴ�.");
        }
    }

}
