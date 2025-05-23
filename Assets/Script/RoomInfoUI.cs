using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Mirror;
using System;

public class RoomInfoUI : MonoBehaviour
{
    public TMP_Text roomNameText;
    public TMP_Text roomStateText;
    public Button joinButton;

    private string matchId;

    public void SetRoomInfo(string matchId, int current, int max, bool isOpen)
    {
        this.matchId = matchId;

        if (roomNameText != null)
            roomNameText.text = $"Room {matchId.Substring(0, 6)}";

        if (roomStateText != null)
            roomStateText.text = $"{current}/{max}";

        if (joinButton != null)
        {
            joinButton.interactable = isOpen;
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnClickJoin);
        }
    }

    private void OnClickJoin()
    {
        if (!Guid.TryParse(matchId, out _))
        {
            Debug.LogError("[RoomInfoUI] ��ȿ���� ���� matchId");
            return;
        }

        Debug.Log($"[RoomInfoUI] Join Ŭ���� - matchId: {matchId}");
        CustomNetworkManager.matchIdToJoin = matchId;

        var networkManager = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
        if (networkManager != null)
        {
            networkManager.StartClientWithCustomPort();
            Debug.Log($"Ŭ���̾�Ʈ ���� �õ�: {matchId}");
        }
        else
        {
            Debug.LogError("CustomNetworkManager�� ã�� �� �����ϴ�.");
        }
    }
}
