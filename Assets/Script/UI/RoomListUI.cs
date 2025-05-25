// 통합 RoomListUI.cs (자동 진입 방지: 메인메뉴 씬에서만 StartHost)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RoomListUI : MonoBehaviour
{
    public static RoomListUI Instance;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;

#if UNITY_EDITOR
        if (currentScene == "MainMenuScene" && !NetworkServer.active && !NetworkClient.active)
        {
            Debug.Log("[RoomListUI] 메인 메뉴에서만 StartHost() 실행됨");
            NetworkManager.singleton.GetComponent<CustomNetworkManager>().StartHost();
        }
#else
        string[] args = Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg == "-host" && currentScene == "MainMenuScene")
            {
                if (!NetworkServer.active && !NetworkClient.active)
                {
                    Debug.Log("[RoomListUI] -host 인자 감지, StartHost() 실행");
                    NetworkManager.singleton.GetComponent<CustomNetworkManager>().StartHost();
                }
            }
        }
#endif

        if (!handlerRegistered)
        {
            NetworkClient.RegisterHandler<RoomListSyncMessage>(OnRoomListSyncMessage);
            handlerRegistered = true;
        }

        createButton?.onClick.AddListener(OnCreateRoomConfirm);
        cancelButton?.onClick.AddListener(HideCreateRoomPopup);
        refreshButton?.onClick.AddListener(RequestRoomListRefresh);

        createRoomPopup?.SetActive(false);

        if (roomNameText != null && NetworkClient.connection != null && NetworkClient.connection.identity != null)
        {
            var player = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
            if (player != null)
            {
                roomNameText.text = player.roomName;
            }
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
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[RoomListUI] 이미 서버나 클라이언트가 실행 중이므로 StartHost 생략됨");
            return;
        }

        string newMatchId = Guid.NewGuid().ToString();
        Debug.Log($"[RoomListUI] 방 생성 요청 matchId: {newMatchId}");

        CustomNetworkManager.matchIdToJoin = newMatchId;

        var manager = NetworkManager.singleton;
        if (manager == null)
        {
            Debug.LogError("[RoomListUI] NetworkManager.singleton이 null입니다.");
            return;
        }

        var customManager = manager.GetComponent<CustomNetworkManager>();
        if (customManager == null)
        {
            Debug.LogError("[RoomListUI] CustomNetworkManager를 찾을 수 없습니다.");
            return;
        }

        customManager.StartHost();
        Debug.Log("호스트 시작됨");

        createRoomPopup.SetActive(false);
    }

    private bool triedAutoConnect = false;

    public void RequestRoomListRefresh()
    {
        Debug.Log($"[RoomListUI] 연결 상태: ClientActive={NetworkClient.active}, Connected={NetworkClient.isConnected}");

        if (NetworkClient.isConnected)
        {
            NetworkClient.Send(new EmptyMessage());
            Debug.Log("[RoomListUI] 서버에 방 리스트 요청 전송");
        }
        else
        {
            Debug.LogWarning("[RoomListUI] 클라이언트 연결되지 않음");

            if (!triedAutoConnect)
            {
                triedAutoConnect = true;
                Debug.Log("[RoomListUI] 자동 연결 시도 중...");

                var net = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
#if UNITY_EDITOR
                net.StartClientManual("127.0.0.1", 7777);
#else
            net.StartClientWithCustomPort();
#endif
            }
            else
            {
                Debug.LogWarning("[RoomListUI] 이전에 자동 연결 시도했으므로 재시도하지 않음");
            }
        }
    }



    private void OnRoomListSyncMessage(RoomListSyncMessage msg)
    {
        Debug.Log($"[RoomListUI] 서버로부터 방 리스트 수신: {msg.roomList.Count}개");
        RenderRoomList(msg.roomList);
    }

    public void RenderRoomList(List<RoomInfo> list)
    {
        if (contentParent == null || !contentParent.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[RoomListUI] contentParent가 파괴되었거나 비활성입니다.");
            return;
        }

        if (list == null || list.Count == 0)
        {
            Debug.Log("[RoomListUI] 표시할 방 없음");
            return;
        }

        var children = new List<GameObject>();
        for (int i = 0; i < contentParent.childCount; i++)
        {
            var child = contentParent.GetChild(i);
            if (child != null)
                children.Add(child.gameObject);
        }

        foreach (var obj in children)
        {
            if (obj != null)
                Destroy(obj);
        }

        foreach (var info in list)
        {
            GameObject obj = Instantiate(roomUIPrefab, contentParent);
            var texts = obj.GetComponentsInChildren<TextMeshProUGUI>();

            foreach (var t in texts)
            {
                if (t == null) continue;
                if (t.name.Contains("Name")) t.text = info.roomName;
                else if (t.name.Contains("State")) t.text = $"{info.currentPlayers}/{info.maxPlayers}";
            }

            var joinBtn = obj.GetComponentInChildren<Button>();
            if (joinBtn != null)
            {
                joinBtn.interactable = true;
                joinBtn.onClick.RemoveAllListeners();
                joinBtn.onClick.AddListener(() =>
                {
                    Debug.Log($"[RoomListUI] 조인 시도: {info.matchId}");
                    CustomNetworkManager.matchIdToJoin = info.matchId;

                    var net = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
#if UNITY_EDITOR
                    if (NetworkClient.active || NetworkServer.active)
                    {
                        Debug.LogWarning("[RoomListUI] 이미 연결된 상태이므로 StartClient 생략");
                    }
                    else
                    {
                        net.StartClientManual("127.0.0.1", 7777);
                    }
#else
                    if (NetworkClient.active || NetworkServer.active)
                    {
                        Debug.LogWarning("[RoomListUI] 이미 연결된 상태이므로 StartClient 생략");
                    }
                    else
                    {
                        net.StartClientWithCustomPort();
                    }
#endif
                });
            }
        }
    }
}
