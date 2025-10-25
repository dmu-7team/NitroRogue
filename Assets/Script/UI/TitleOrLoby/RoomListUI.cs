using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using NetworkMessages;

public class RoomListUI : MonoBehaviour
{
    public static RoomListUI Instance;
    public static string matchIdToJoin;
    public static bool enableAutoJoin = false;

    [Header("방 리스트 UI")]
    public GameObject roomUIPrefab;
    public Transform contentParent;
    public Button refreshButton;

    [Header("방 만들기 팝업 UI")]
    public GameObject createRoomPopup;
    public TMP_InputField roomNameInputField;
    public Button createButton;
    public Button cancelButton;

    [Header("룸 씬 내 방 이름 UI")]
    public TextMeshProUGUI roomNameText;

    private static bool handlerRegistered = false;
    private static bool listenersRegistered = false;
    private float refreshInterval = 3f;
    public static bool triedAutoConnect = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        createRoomPopup?.SetActive(false);
        Instance = this;
        Debug.Log($"[RoomListUI] 현재 인스턴스 ID: {GetInstanceID()}");
        if (!handlerRegistered)
        {
            NetworkClient.RegisterHandler<RoomListSyncMessage>(OnRoomListSyncMessageReceived);
            handlerRegistered = true;
            Debug.Log("[RoomListUI] RoomListSyncMessage 핸들러 등록 완료");
        }
    }

    private void Start()
    {
        Debug.Log("[RoomListUI] Start 호출됨");

        if (!handlerRegistered)
        {
            NetworkClient.RegisterHandler<RoomListSyncMessage>(OnRoomListSyncMessageReceived);
            handlerRegistered = true;
        }

        if (!listenersRegistered)
        {
            Debug.Log($"[RoomListUI] 리스너 등록 시도, 인스턴스 ID: {GetInstanceID()}");

            createButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            refreshButton.onClick.RemoveAllListeners();

            createButton.onClick.AddListener(OnCreateRoomConfirm);
            cancelButton.onClick.AddListener(HideCreateRoomPopup);
            refreshButton.onClick.AddListener(RequestRoomListRefresh);

            listenersRegistered = true;
        }

        createRoomPopup?.SetActive(false);

        // 방 이름 UI 갱신
        TryUpdateRoomNameUI();
    }

    private void OnEnable()
    {
        if (!NetworkClient.isConnected && !NetworkClient.active && !NetworkServer.active && !triedAutoConnect)
        {
            triedAutoConnect = true;
            Debug.Log("[RoomListUI] 자동 서버 연결 시도 (localhost:7777)");

            var nm = NetworkManager.singleton;
            nm.networkAddress = "4.217.235.248";         // 내부망이면 192.168.x.x로
            var tp = nm.GetComponent<TelepathyTransport>();
            if (tp != null) tp.port = 7777;

            nm.StartClient();
        }

        InvokeRepeating(nameof(RequestRoomListRefresh), 1f, refreshInterval);
    }



    private void OnDisable()
    {
        CancelInvoke(nameof(RequestRoomListRefresh));
    }

    public void ShowCreateRoomPopup()
    {
        createRoomPopup?.SetActive(true);
        if (roomNameInputField != null)
            roomNameInputField.text = "";
    }

    public void HideCreateRoomPopup()
    {
        createRoomPopup?.SetActive(false);
    }

    public void OnCreateRoomConfirm()
    {
        if (!NetworkClient.isConnected)
        {
            Debug.LogWarning("[RoomListUI] 아직 서버에 연결되지 않음. 연결 후 시도하세요.");
            return;
        }

        string roomName = roomNameInputField.text;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("[RoomListUI] 방 이름이 비어 있습니다.");
            return;
        }

        string newMatchId = Guid.NewGuid().ToString();

        matchIdToJoin = newMatchId;
        enableAutoJoin = true;

        string nickname = PlayerPrefs.GetString("nickname", "");
        if (string.IsNullOrEmpty(nickname))
        {
            nickname = $"게스트{UnityEngine.Random.Range(1000, 9999)}";
            PlayerPrefs.SetString("nickname", nickname);
        }
        JoinMatchMessage msg = new JoinMatchMessage
        {
            matchId = newMatchId,
            roomName = roomName,
            nickname = nickname,
        };

        NetworkClient.Send(msg);
        createRoomPopup.SetActive(false);

        RoomUIManager.Instance.ShowRoom(roomName);
        Debug.Log($"[RoomListUI] 방 생성 요청 전송: {roomName} ({newMatchId})");
    }

    public void RequestRoomListRefresh()
    {
        if (NetworkClient.isConnected)
        {
            Debug.Log("[RoomListUI] 서버에 방 리스트 요청 전송 (자동)");
            NetworkClient.Send(new RoomListRequestMessage());
            return;
        }

        if (!triedAutoConnect)
        {
            triedAutoConnect = true;
            Debug.Log("[RoomListUI] 서버에 자동 연결 시도 중... (localhost:7777)");

            // 주소/포트 로컬로 고정
            var nm = NetworkManager.singleton;
            nm.networkAddress = "4.217.235.248"; // 또는 서버 PC의 내부 IP (예: 192.168.x.x)

            // Telepathy 포트 맞추기
            var tp = nm.GetComponent<TelepathyTransport>();
            if (tp != null) tp.port = 7777;

            if (!NetworkClient.active && !NetworkServer.active)
                nm.StartClient();
        }
    }


    public void OnRoomListSyncMessageReceived(RoomListSyncMessage msg)
    {
        if (contentParent == null)
        {
            Debug.LogWarning("[RoomListUI] contentParent 없음 -> 방 리스트 갱신 무시");
            return;
        }

        ClearRoomList();

   
        foreach (RoomInfo info in msg.roomList)
        {
            if (info.currentPlayers <= 0)
                continue; // 방에 플레이어가 없으면 표시하지 않음

            GameObject roomItem = Instantiate(roomUIPrefab, contentParent);
            RoomInfoUI ui = roomItem.GetComponent<RoomInfoUI>();
            if (ui != null)
                ui.SetInfo(info);
        }

    }

    private void ClearRoomList()
    {
        foreach (Transform child in contentParent)
        {
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    public void SetRoomInfo(string name, string matchId, int current, int max)
    {
        roomNameText.text = $"{name} ({current}/{max})";
    }

    public void RenderRoomList(List<RoomInfo> list)
    {
        if (contentParent == null || !contentParent.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[RoomListUI] contentParent 비활성화됨");
            return;
        }

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        if (list == null || list.Count == 0)
        {
            Debug.Log("[RoomListUI] 표시할 방 없음");
            return;
        }

        foreach (var info in list)
        {
            GameObject obj = Instantiate(roomUIPrefab, contentParent);
            var infoUI = obj.GetComponent<RoomInfoUI>();

            if (infoUI != null)
                infoUI.SetInfo(info);  // 여기도 수정됨
        }
    }

    private void TryUpdateRoomNameUI()
    {
        if (roomNameText != null && NetworkClient.connection != null && NetworkClient.connection.identity != null)
        {
            var player = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
            if (player != null)
                roomNameText.text = $"방 이름: {player.roomName}";
        }
    }

}
