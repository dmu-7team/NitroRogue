using UnityEngine;
using TMPro;
using Mirror;
using System.Collections;
using UnityEngine.SceneManagement;

public class RoomSceneUI : MonoBehaviour
{
    public TextMeshProUGUI roomNameText; // ← 인스펙터에서 반드시 연결
    public static RoomSceneUI Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        roomNameText.text = "방 이름 불러오는 중...";
        StartCoroutine(WaitAndApplyRoomName());
    }

    private IEnumerator WaitAndApplyRoomName()
    {
        // localPlayer가 생성될 때까지 대기
        yield return new WaitUntil(() => NetworkClient.localPlayer != null);

        RoomPlayer player = NetworkClient.localPlayer.GetComponent<RoomPlayer>();

        if (player != null)
        {
            UpdateRoomName(player.roomName);
            Debug.Log($"[RoomSceneUI] 코루틴으로 방 이름 갱신됨: {player.roomName}");
        }
        else
        {
            Debug.LogWarning("[RoomSceneUI] RoomPlayer 컴포넌트가 존재하지 않음");
        }
    }


    private void TryApplyRoomName()
    {
        if (NetworkClient.localPlayer != null)
        {
            RoomPlayer player = NetworkClient.localPlayer.GetComponent<RoomPlayer>();
            if (player != null)
            {
                UpdateRoomName(player.roomName);
                Debug.Log($"[RoomSceneUI] Start()에서 방 이름 설정됨: {player.roomName}");
            }
            else
            {
                Debug.LogWarning("[RoomSceneUI] RoomPlayer 컴포넌트를 찾을 수 없음");
            }
        }
        else
        {
            Debug.LogWarning("[RoomSceneUI] localPlayer가 아직 생성되지 않음");
        }
    }

    // 방 이름 UI 업데이트
    public void UpdateRoomName(string name)
    {
        if (roomNameText != null)
        {
            roomNameText.text = $"방 이름: {name}";
            Debug.Log($"[RoomSceneUI] 방 이름 UI 갱신: {name}");
        }
        else
        {
            Debug.LogWarning("[RoomSceneUI] roomNameText가 인스펙터에 연결되지 않음");
        }
    }

    // 방 나가기 버튼 이벤트
    public void OnClickLeaveRoom()
    {
        Debug.Log("[클라이언트] 방 나가기 요청됨");
        RoomListUI.matchIdToJoin = "";
        RoomListUI.enableAutoJoin = false;
        StartCoroutine(DisconnectAndReturnToMenu());
   
    }

    // 클라이언트 종료 후 메인 메뉴로
    private IEnumerator DisconnectAndReturnToMenu()
    {
        NetworkManager.singleton.StopClient();
        NetworkClient.Shutdown();

        yield return new WaitForSeconds(0.3f);

        RoomListUI.matchIdToJoin = ""; // 여기가 핵심!

        SceneManager.LoadScene("MainMenu");
    }

}
