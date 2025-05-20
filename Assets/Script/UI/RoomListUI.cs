using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mirror;

public class RoomListUI : MonoBehaviour
{
    [Header("Room UI")]
    public Transform roomListParent;         // ScrollView > Viewport > Content
    public GameObject roomEntryPrefab;       // Project 창에 있는 Room 프리팹

    private List<GameObject> spawnedRooms = new();

    void Start()
    {
        if (roomListParent == null || roomEntryPrefab == null)
        {
            Debug.LogError("[RoomListUI] 인스펙터에서 RoomListParent 또는 RoomEntryPrefab이 연결되지 않았습니다.");
            return;
        }

        RefreshRoomList();
    }

    public void RefreshRoomList()
    {
        // 기존 방 목록 삭제
        foreach (var obj in spawnedRooms)
        {
            Destroy(obj);
        }
        spawnedRooms.Clear();

        // 예시로 4개 더미 방 생성
        for (int i = 0; i < 4; i++)
        {
            GameObject roomObj = Instantiate(roomEntryPrefab); // parent 없이 생성
            roomObj.transform.SetParent(roomListParent, false); // 여기서 parent 지정

            // 방 이름 설정
            TMP_Text nameText = roomObj.transform.Find("Room Name Text")?.GetComponent<TMP_Text>();
            if (nameText != null)
                nameText.text = $"Room {i + 1}";
            else
                Debug.LogWarning("[RoomListUI] Room Name Text를 찾을 수 없음");

            // 상태 텍스트도 원하면 여기에 추가 가능

            // Join 버튼 연결
            Button joinButton = roomObj.transform.Find("Room Join Button")?.GetComponent<Button>();
            if (joinButton != null)
            {
                int roomIndex = i;
                joinButton.onClick.AddListener(() =>
                {
                    Debug.Log($"[JOIN] Room {roomIndex} 클릭됨");
                    NetworkManager.singleton.networkAddress = "localhost"; // 나중에 실제 주소로 대체
                    NetworkManager.singleton.StartClient();
                });
            }
            else
            {
                Debug.LogError("[RoomListUI] Room Join Button을 찾을 수 없습니다!");
            }

            spawnedRooms.Add(roomObj);
        }
    }
}
