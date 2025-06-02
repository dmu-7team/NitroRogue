using UnityEngine;
using TMPro;
using Mirror;
using NetworkMessages;

public class RoomUIManager : MonoBehaviour
{
    public static RoomUIManager Instance;

    [Header("UI References")]
    public GameObject mainMenuPanel;
    public GameObject roomPanel;
    public TextMeshProUGUI roomNameText;
    public GameObject startButton;

    [Header("플레이어 리스트 UI")]
    public Transform playerListParent;
    public GameObject playerListItemPrefab;



    private void Awake()
    {
        Debug.Log("[RoomUIManager] Awake 호출됨");

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainMenuPanel.SetActive(true);
        roomPanel.SetActive(false);
    }



    public void ShowRoom(string roomName = "")
    {
        mainMenuPanel.SetActive(false);
        roomPanel.SetActive(true);
        roomNameText.text = $"방 이름: {roomName}";
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        roomPanel.SetActive(false);
    }

    public void ShowStartButton(bool show)
    {
        if (startButton != null)
            startButton.SetActive(show);
    }
    public void UpdateRoomName(string newName)
    {
        if (roomNameText != null)
            roomNameText.text = $"방 이름: {newName}";
    }


    public void UpdatePlayerReadyStatus(RoomPlayer player, bool isReady)
    {
        Debug.Log($"[RoomUIManager] {player.roomName} 플레이어 준비 상태: {isReady}");

        // 실제로는 playerName, roomName 등을 이용해서 UI 요소 업데이트해야 함
        // 예시:
        // var uiElement = 플레이어 UI 리스트에서 player 식별;
        // uiElement.readyIcon.SetActive(isReady);
    }
    public void OnLeaveRoomButtonClicked()
    {
        Debug.Log("[RoomUIManager] 방 나가기 버튼 클릭됨");

        // 서버 연결 종료
        NetworkClient.Disconnect();

        // 자동 재접속 관련 값 리셋
        RoomListUI.matchIdToJoin = "";
        RoomListUI.enableAutoJoin = false;
        RoomListUI.triedAutoConnect = false;

        // UI 전환 및 새로고침
        RoomUIManager.Instance.ShowMainMenu();
        RoomListUI.Instance.RequestRoomListRefresh();
    }
    public void AddPlayerToList(string name, bool isLeader, bool isMe)
    {
        if (playerListItemPrefab == null || playerListParent == null)
        {
            Debug.LogWarning("[RoomUIManager] 플레이어 리스트 UI가 설정되지 않음");
            return;
        }

        GameObject item = Instantiate(playerListItemPrefab, playerListParent);
        var text = item.GetComponentInChildren<TextMeshProUGUI>();

        string label = name;
        if (isLeader) label += " (방장)";
        if (isMe) label += " (본인)";

        text.text = label;
    }




    private void OnJoinResultMessageReceived(JoinResultMessage msg)
    {
        if (msg.success)
        {
            Debug.Log($"[Client] 방 참가 성공: {msg.roomName} ({msg.matchId})");

            if (RoomUIManager.Instance == null)
            {
                Debug.LogError("[Client] RoomUIManager.Instance 가 null입니다.");
            }
            else
            {
                RoomUIManager.Instance.ShowRoom(msg.roomName); //  여기서 방 UI 띄움
            }
        }
        else
        {
            Debug.LogWarning("[Client] 방 참가 실패");
        }
    }
    public void UpdateRoomUI(string roomName)
    {
        if (RoomUIManager.Instance == null)
        {
            Debug.LogError("[RoomPlayer] RoomUIManager.Instance가 null입니다!");
            return;
        }

        RoomUIManager.Instance.ShowRoom(roomName);
    }

    public void ClearPlayerList()
    {
        foreach (Transform child in playerListParent)
        {
            Destroy(child.gameObject);
        }
    }



    public void RebuildPlayerList()
    {
        ClearPlayerList();

        var players = Object.FindObjectsByType<RoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            bool isMe = p.isLocalPlayer; // 본인인지 체크
            AddPlayerToList(p.playerName, p.isLeader, isMe);
        }
    }



}
