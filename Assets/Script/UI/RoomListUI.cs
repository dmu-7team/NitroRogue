using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using System;

public class RoomListUI : MonoBehaviour
{
    public static RoomListUI Instance;

    [Header("�� ����Ʈ UI")]
    public GameObject roomUIPrefab;
    public Transform contentParent;

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
                    Debug.LogError("[RoomListUI] ��ȿ���� ���� matchId");
                    return;
                }

                Debug.Log($"[RoomListUI] ���� �õ� matchId: {info.matchId}");
                CustomNetworkManager.matchIdToJoin = info.matchId;
                var customNetworkManager = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
                if (customNetworkManager != null)
                {
                    customNetworkManager.StartClientWithCustomPort();
                    Debug.Log($"Ŭ���̾�Ʈ ���� �õ�, matchId: {CustomNetworkManager.matchIdToJoin}");
                }
                else
                {
                    Debug.LogError("CustomNetworkManager�� ã�� �� �����ϴ�. Inspector���� Ȯ���ϼ���.");
                }
            });
        }
    }
}