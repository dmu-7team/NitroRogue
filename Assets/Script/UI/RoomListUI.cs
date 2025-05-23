using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System;
using System.Collections.Generic;

// EmptyMessage는 Mirror에서 기본 제공하므로 따로 정의하지 않음

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

        if (refreshButton != null)
            refreshButton.onClick.AddListener(RequestRoomListRefresh);

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



    public void RequestRoomListRefresh()
    {
        if (NetworkClient.active)
        {
            Debug.Log("[RoomListUI] 방 리스트 새로고침 요청");
            NetworkClient.Send(new EmptyMessage());
        }
        else
        {
            Debug.LogWarning("[RoomListUI] 네트워크 연결 안 됨 (새로고침 무시)");
        }
    }
}
