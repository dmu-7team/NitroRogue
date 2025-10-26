using UnityEngine;
using TMPro;
using Mirror;
using NetworkMessages;
using UnityEngine.UI; // ← 버튼 포함한 UI 컴포넌트용
using System.Linq;
using Unity.VisualScripting;

public class RoomUIManager : MonoBehaviour
{
    public static RoomUIManager Instance;
    
    [Header("UI References")]

    public GameObject mainMenuPanel;
    public GameObject lobyPanel;
    public GameObject gameHudCanvas;
    public GameObject roomPanel;
    public TextMeshProUGUI roomNameText;
    public GameObject startButton;
    public GameObject createRoomPopup; // <- 이 줄 추가

    [Header("플레이어 리스트 UI")]
    public Transform playerListParent;
    public GameObject playerListItemPrefab;
    [SerializeField] private GameObject mainMenuCanvas;

    public GameObject createRoomPanel;  // Panel
    public GameObject roomUI;           // RoomUI
    public GameObject background;       // Background

    private void Awake()
    {
        Debug.Log("[RoomUIManager] Awake 호출됨");

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainMenuPanel.SetActive(true);
        lobyPanel.SetActive(false);
        roomPanel.SetActive(false);
    }

    public void ShowLoby()
    {
        mainMenuPanel.SetActive(false);
        lobyPanel.SetActive(true);
        roomPanel.SetActive(false);
    }
    public void ShowRoom(string roomName = "")
    {
        _startRequested = false;                     // ★ 리셋
        mainMenuPanel.SetActive(false);
        lobyPanel.SetActive(false);
        roomPanel.SetActive(true);
        roomNameText.text = $"방 이름: {roomName}";
    }
    public void ShowMainMenu()
    {
        _startRequested = false;                     // ★ 리셋
        mainMenuPanel.SetActive(true);
        lobyPanel.SetActive(false);
        roomPanel.SetActive(false);
        Debug.Log("[UI] 메인 로비 패널 복귀");
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
        _startRequested = false;
        // 서버 연결 종료
        NetworkClient.Disconnect();

        // 자동 재접속 관련 값 리셋
        RoomListUI.matchIdToJoin = "";
        RoomListUI.enableAutoJoin = false;
        RoomListUI.triedAutoConnect = false;
        SpectatorManager.Instance?.ResetAll();
        // UI 전환 및 새로고침
        RoomUIManager.Instance.ShowLoby();
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

    bool _startRequested = false;
    public void OnClickGameStart()
    {
        if (_startRequested) return;
        _startRequested = true;
        var conn = NetworkClient.connection;
        Debug.Log($"[DEBUG] 연결된 클라이언트: {conn}");

        var localPlayer = conn.identity?.GetComponent<RoomPlayer>();
        if (localPlayer == null)
        {
            Debug.LogWarning("[RoomUIManager] 로컬 플레이어를 찾을 수 없습니다.");
            _startRequested = false;
            return;
        }

        Debug.Log($"[DEBUG] 현재 플레이어는 리더인가? {localPlayer.isLeader}");

        if (localPlayer.isLeader)
        {
            Debug.Log($"[RoomUIManager] 리더 상태 확인됨, matchId: {localPlayer.matchId}");
            localPlayer.CmdStartGame();
        }
        else
        {
            Debug.Log("[RoomUIManager] 리더가 아니라 게임 시작 불가");
            _startRequested = false;
        }

    }

    public void SwitchToGameUI()
    {
        Debug.Log("[RoomUIManager] SwitchToGameUI() 호출됨");

        // 메인메뉴 UI 전체 끄기
        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(false);  // ← Canvas 루트 전체 꺼버림

        if (gameHudCanvas) gameHudCanvas.SetActive(true);

        Debug.Log("[RoomUIManager] 모든 UI 정리 완료, 게임 HUD만 활성화됨");
        UIManager.Instance.StartStopwatchLocal();
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

        var local = NetworkClient.connection?.identity?.GetComponent<RoomPlayer>();
        if (local == null) return;

        var players = Object.FindObjectsByType<RoomPlayer>(FindObjectsSortMode.None)
                            .Where(p => p.matchId == local.matchId);

        foreach (var p in players)
        {
            bool isMe = p.isLocalPlayer;
            AddPlayerToList(p.playerName, p.isLeader, isMe);
        }
    }



    public Button[] characterButtons;
    public TextMeshProUGUI[] characterButtonTexts;

    public void UpdateCharacterSelection(int index, string playerName)
    {
        if (index < 0 || index >= characterButtons.Length) return;

        var text = characterButtons[index].GetComponentInChildren<TextMeshProUGUI>();
        text.text = $"{playerName} 선택함";
        characterButtons[index].interactable = false;
    }

    public void SetupCharacterButtons(RoomPlayer localPlayer)
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int idx = i; // for closure
            characterButtons[i].onClick.RemoveAllListeners();
            characterButtons[i].onClick.AddListener(() =>
            {
                localPlayer.CmdSelectCharacter(idx);
            });
        }
    }

    public void UpdateCharacterButtonStates(int[] selectedCharacters, string[] playerNames)
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            var btn = characterButtons[i];
            var txt = characterButtonTexts[i];

            bool isTaken = false;

            for (int j = 0; j < selectedCharacters.Length; j++)
            {
                if (selectedCharacters[j] == i)
                {
                    txt.text = $"{playerNames[j]}\n선택됨";
                    btn.interactable = false;
                    isTaken = true;
                    break;
                }
            }

            if (!isTaken)
            {
                txt.text = "선택 안됨";
                btn.interactable = true;
            }
        }
    }

    public void OnCharacterButtonClicked(int index)
    {
        var local = NetworkClient.connection?.identity?.GetComponent<RoomPlayer>();
        if (local == null) return;

        // 전역 스캔 금지. 그냥 서버에 위임.
        local.CmdSelectCharacter(index);
    }






    public void HideRoomUI()
    {
        roomPanel?.SetActive(false);     // RoomUI: 방 이름/리스트 패널
        lobyPanel.SetActive(false);
        mainMenuPanel?.SetActive(false); // Panel: 방 생성/입장 패널
        background?.SetActive(false);    // Background: 전체 회색 배경
    }
}
