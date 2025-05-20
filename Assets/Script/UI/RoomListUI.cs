using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mirror;

public class RoomListUI : MonoBehaviour
{
    [Header("Room UI")]
    public Transform roomListParent;         // ScrollView > Viewport > Content
    public GameObject roomEntryPrefab;       // Project â�� �ִ� Room ������

    private List<GameObject> spawnedRooms = new();

    void Start()
    {
        if (roomListParent == null || roomEntryPrefab == null)
        {
            Debug.LogError("[RoomListUI] �ν����Ϳ��� RoomListParent �Ǵ� RoomEntryPrefab�� ������� �ʾҽ��ϴ�.");
            return;
        }

        RefreshRoomList();
    }

    public void RefreshRoomList()
    {
        // ���� �� ��� ����
        foreach (var obj in spawnedRooms)
        {
            Destroy(obj);
        }
        spawnedRooms.Clear();

        // ���÷� 4�� ���� �� ����
        for (int i = 0; i < 4; i++)
        {
            GameObject roomObj = Instantiate(roomEntryPrefab); // parent ���� ����
            roomObj.transform.SetParent(roomListParent, false); // ���⼭ parent ����

            // �� �̸� ����
            TMP_Text nameText = roomObj.transform.Find("Room Name Text")?.GetComponent<TMP_Text>();
            if (nameText != null)
                nameText.text = $"Room {i + 1}";
            else
                Debug.LogWarning("[RoomListUI] Room Name Text�� ã�� �� ����");

            // ���� �ؽ�Ʈ�� ���ϸ� ���⿡ �߰� ����

            // Join ��ư ����
            Button joinButton = roomObj.transform.Find("Room Join Button")?.GetComponent<Button>();
            if (joinButton != null)
            {
                int roomIndex = i;
                joinButton.onClick.AddListener(() =>
                {
                    Debug.Log($"[JOIN] Room {roomIndex} Ŭ����");
                    NetworkManager.singleton.networkAddress = "localhost"; // ���߿� ���� �ּҷ� ��ü
                    NetworkManager.singleton.StartClient();
                });
            }
            else
            {
                Debug.LogError("[RoomListUI] Room Join Button�� ã�� �� �����ϴ�!");
            }

            spawnedRooms.Add(roomObj);
        }
    }
}
