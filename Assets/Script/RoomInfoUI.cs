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
            Debug.LogError("[RoomInfoUI] 유효하지 않은 matchId");
            return;
        }

        Debug.Log($"[RoomInfoUI] Join 클릭됨 - matchId: {matchId}");
        CustomNetworkManager.matchIdToJoin = matchId;

        var networkManager = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
        if (networkManager != null)
        {
            networkManager.StartClientWithCustomPort();
            Debug.Log($"클라이언트 조인 시도: {matchId}");
        }
        else
        {
            Debug.LogError("CustomNetworkManager를 찾을 수 없습니다.");
        }
    }
}
