using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomInfoUI : MonoBehaviour
{
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText;
    public Button joinButton;

    private string matchId;

    public void Initialize(string roomName, string matchId, int currentPlayers, int maxPlayers)
    {
        this.matchId = matchId;

        if (roomNameText != null) roomNameText.text = roomName;
        if (playerCountText != null) playerCountText.text = $"{currentPlayers}/{maxPlayers}";

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinClicked);
        }
    }

    private void OnJoinClicked()
    {
        Debug.Log($"[RoomInfoUI] 참가 버튼 클릭: {matchId}");
        JoinButtonHandler.JoinWithMatchId(matchId);
    }
}
