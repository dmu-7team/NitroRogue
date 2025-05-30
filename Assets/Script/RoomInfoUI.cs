using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using NetworkMessages; // RoomInfo 구조체를 위해 필요

public class RoomInfoUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText;
    public Button joinButton;

    private string matchId;

    public void SetRoomInfo(string roomName, string matchId, int currentPlayers, int maxPlayers)
    {
        this.matchId = matchId;

        if (roomNameText != null)
            roomNameText.text = roomName;

        if (playerCountText != null)
            playerCountText.text = $"{currentPlayers} / {maxPlayers}";

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(JoinRoom);
        }
    }

    //  RoomInfo를 받는 버전
    public void SetInfo(RoomInfo info)
    {
        SetRoomInfo(info.roomName, info.matchId, info.currentPlayers, info.maxPlayers);
    }

    private void JoinRoom()
    {
        Debug.Log($"[RoomInfoUI] 참가 시도 - matchId: {matchId}");
        CustomNetworkManager.matchIdToJoin = matchId;

        if (!NetworkClient.active && !NetworkServer.active)
        {
            NetworkManager.singleton.networkAddress = "127.0.0.1";
            NetworkManager.singleton.StartClient();
        }
        else
        {
            Debug.LogWarning("[RoomInfoUI] 이미 네트워크 연결 중이므로 클라이언트 시작 생략");
        }
    }
}

