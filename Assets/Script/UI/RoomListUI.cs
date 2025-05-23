using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System;
using System.Collections.Generic;

// EmptyMessage�� Mirror���� �⺻ �����ϹǷ� ���� �������� ����

public class RoomListUI : MonoBehaviour
{
    public static RoomListUI Instance;

    [Header("�� ����Ʈ UI")]
    public GameObject roomUIPrefab;
    public Transform contentParent;
    public Button refreshButton;

    [Header("�� ����� �˾� UI")]
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
        Debug.Log($"[RoomListUI] ���� matchId: {newMatchId}");

        CustomNetworkManager.matchIdToJoin = newMatchId;

        var customNetworkManager = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
        if (customNetworkManager != null)
        {
            customNetworkManager.StartHost();
            Debug.Log($"ȣ��Ʈ ���� ����, matchId: {CustomNetworkManager.matchIdToJoin}");
        }
        else
        {
            Debug.LogError("CustomNetworkManager�� ã�� �� �����ϴ�. Inspector���� Ȯ���ϼ���.");
        }

        createRoomPopup.SetActive(false);
    }



    public void RequestRoomListRefresh()
    {
        if (NetworkClient.active)
        {
            Debug.Log("[RoomListUI] �� ����Ʈ ���ΰ�ħ ��û");
            NetworkClient.Send(new EmptyMessage());
        }
        else
        {
            Debug.LogWarning("[RoomListUI] ��Ʈ��ũ ���� �� �� (���ΰ�ħ ����)");
        }
    }
}
