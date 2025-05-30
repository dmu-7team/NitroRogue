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
    private float refreshInterval = 3f;
    private bool triedAutoConnect = false;

    private void Awake()
    {
        Debug.Log("[RoomListUI] Awake 실행");

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RoomListUI] 중복 인스턴스 발견 -> 파괴됨");
            Destroy(gameObject);
            return;
        }

        Debug.Log("[RoomListUI] Singleton 인스턴스 등록 완료");
        Instance = this;
    }


    private static bool listenersRegistered = false; // 리스너 중복 방지

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
            createButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
            refreshButton?.onClick.RemoveAllListeners();

            createButton?.onClick.AddListener(OnCreateRoomConfirm);
            cancelButton?.onClick.AddListener(HideCreateRoomPopup);
            refreshButton?.onClick.AddListener(RequestRoomListRefresh);

            listenersRegistered = true;
            Debug.Log("[RoomListUI] 버튼 리스너 최초 등록");
        }

        createRoomPopup?.SetActive(false);

        if (roomNameText != null && NetworkClient.connection != null && NetworkClient.connection.identity != null)
        {
            var player = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
            if (player != null)
                roomNameText.text = player.roomName;
        }
    }


    private void OnEnable()
    {
        if (!NetworkServer.active && !NetworkClient.active)
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

        // 서버에 방 생성 요청 전송
        JoinMatchMessage msg = new JoinMatchMessage
        {
            matchId = newMatchId,
            roomName = roomName
        };

        NetworkClient.Send(msg);
        createRoomPopup.SetActive(false);

        Debug.Log($"[RoomListUI] 방 생성 요청 전송: {roomName} ({newMatchId})");
        Debug.LogError("[RoomListUI]  OnCreateRoomConfirm() 호출됨", this);
        Debug.Log(Environment.StackTrace); // 호출 경로 로그 찍기
    }


    public void RequestRoomListRefresh()
    {
        if (NetworkClient.isConnected)
        {
            Debug.Log("[RoomListUI] 서버에 방 리스트 요청 전송 (자동)");
            NetworkClient.Send(new RoomListRequestMessage());
        }
        else
        {
            if (!triedAutoConnect)
            {
                triedAutoConnect = true;
                Debug.Log("[RoomListUI] 서버에 자동 연결 시도 중...");

                // 1. 먼저 연결 시도
                NetworkManager.singleton.networkAddress = "127.0.0.1";
                NetworkManager.singleton.StartClient();

                // 2. 연결 완료 후 요청 보내도록 따로 처리해야 함 (예: OnClientConnect에서)
            }
        }
    }


    // 메시지 수신 후 리스트 처리
    // 메시지 수신 후 리스트 처리
    public void OnRoomListSyncMessageReceived(RoomListSyncMessage msg)
    {
        ClearRoomList();

        foreach (RoomInfo info in msg.roomList)
        {
            GameObject roomItem = Instantiate(roomUIPrefab, contentParent);
            RoomInfoUI ui = roomItem.GetComponent<RoomInfoUI>();
            if (ui != null)
                ui.SetInfo(info);
        }
    }

    //  기존 리스트 제거
    private void ClearRoomList()
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
    }

    // 예전 함수 유지
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
                infoUI.SetRoomInfo(info.roomName, info.matchId, info.currentPlayers, info.maxPlayers);
        }
    }
    
    public TextMeshProUGUI playerCountText;
    public Button joinButton;

    
    private string roomName;

    

   
}
