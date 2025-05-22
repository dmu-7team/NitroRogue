using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using System;

public class RoomListUI : MonoBehaviour
{
    public static RoomListUI Instance;

    [Header("방 리스트 UI")]
    public GameObject roomUIPrefab;
    public Transform contentParent;

    [Header("방 만들기 팝업 UI")]
    public GameObject createRoomPopup;
    public TMP_InputField roomNameInputField;
    public Button createButton;
    public Button cancelButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (createButton != null)
            createButton.onClick.AddListener(OnCreateRoomConfirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(() => createRoomPopup.SetActive(false));

        createRoomPopup?.SetActive(false);
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
        Debug.Log("createRoomPopup: OK");

        string newMatchId = Guid.NewGuid().ToString();
        Debug.Log($"[RoomListUI] 생성 matchId: {newMatchId}");

        CustomNetworkManager.matchIdToJoin = newMatchId;

        var customNetworkManager = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
        if (customNetworkManager != null)
        {
            customNetworkManager.StartHost();
            Debug.Log($"호스트 시작 성공, matchId: {CustomNetworkManager.matchIdToJoin}");
        }
        else
        {
            Debug.LogError("CustomNetworkManager를 찾을 수 없습니다. Inspector에서 확인하세요.");
        }

        createRoomPopup.SetActive(false);
    }

    public void RenderRoomList(List<RoomInfo> list)
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        foreach (RoomInfo info in list)
        {
            GameObject obj = Instantiate(roomUIPrefab, contentParent);
            var texts = obj.GetComponentsInChildren<TextMeshProUGUI>();

            foreach (var t in texts)
            {
                if (t.name.Contains("Name"))
                    t.text = $"Room {info.matchId.Substring(0, 6)}";
                if (t.name.Contains("State"))
                    t.text = $"{info.currentPlayers}/{info.maxPlayers}";
            }

            var joinBtn = obj.GetComponentInChildren<Button>();
            joinBtn.onClick.AddListener(() =>
            {
                if (!Guid.TryParse(info.matchId, out _))
                {
                    Debug.LogError("[RoomListUI] 유효하지 않은 matchId");
                    return;
                }

                Debug.Log($"[RoomListUI] 조인 시도 matchId: {info.matchId}");
                CustomNetworkManager.matchIdToJoin = info.matchId;
                var customNetworkManager = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
                if (customNetworkManager != null)
                {
                    customNetworkManager.StartClientWithCustomPort();
                    Debug.Log($"클라이언트 조인 시도, matchId: {CustomNetworkManager.matchIdToJoin}");
                }
                else
                {
                    Debug.LogError("CustomNetworkManager를 찾을 수 없습니다. Inspector에서 확인하세요.");
                }
            });
        }
    }
}