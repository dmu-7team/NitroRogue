using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Mirror;
using NetworkMessages;

public class RoomInfoUI : MonoBehaviour
{
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText; // 새로 추가
    public Button joinButton;

    private string matchId;
    private string roomName;

    public void SetInfo(RoomInfo info)
    {
        matchId = info.matchId;
        roomName = info.roomName;

        if (roomNameText != null)
            roomNameText.text = roomName;

        if (playerCountText != null)
            playerCountText.text = $"{info.currentPlayers}/{info.maxPlayers}";

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked()
    {
        if (!NetworkClient.isConnected)
        {
            Debug.LogWarning("[RoomInfoUI] 클라이언트가 서버에 연결되어 있지 않습니다.");
            return;
        }

        RoomListUI.matchIdToJoin = matchId;
        RoomListUI.enableAutoJoin = true;

        JoinMatchMessage msg = new JoinMatchMessage
        {
            matchId = matchId,
            roomName = roomName
        };

        NetworkClient.Send(msg);
        Debug.Log($"[RoomInfoUI] 참가 요청 전송됨: {roomName} ({matchId})");
    }
}
