using UnityEngine;
using TMPro;
using Mirror;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject roomListUIPrefab;

    private void Start()
    {
        if (RoomListUI.Instance == null)
        {
            Instantiate(roomListUIPrefab);
            Debug.Log("[MainMenuUI] RoomListUI 프리팹 Instantiate됨");
        }
    }
    public void StartGame()
    {
        // 게임 시작 시 로비 선택 시험을 위해 다음 시센으로 이동
        SceneManager.LoadScene("MainMenu");
    }

    // 환경설정 버튼에 연결
    public void OpenSettings()
    {
        Debug.Log("환경설정 열기");
        // 환경설정 UI 표시를 다루게 해보세요
    }

    // 내 정보 버튼에 연결
    public void OpenMyInfo()
    {
        Debug.Log("내 정보 표시");
        // 내 정보 표시를 다루게 해보세요
    }

    public void GoToTitleScene()
    {
        SceneManager.LoadScene("Title"); // 씬 이름 정확히 입력
    }

    public void Test()
    {
        SceneManager.LoadScene("WoojinScene"); // 씬 이름 정확히 입력
    }
    public TMP_InputField inputFieldMatchId;
    public void OnClickJoin()
    {
        if (NetworkClient.active || NetworkServer.active)
        {
            Debug.LogWarning("[MainMenuUI] 이미 네트워크 동작 중, Join 생략");
            return;
        }

        string matchId = inputFieldMatchId.text;
        if (string.IsNullOrWhiteSpace(matchId))
        {
            Debug.LogWarning("[MainMenuUI] 입력된 matchId 없음");
            return;
        }

        RoomListUI.matchIdToJoin = matchId;

        NetworkManager.singleton.networkAddress = "4.217.235.248";
        NetworkManager.singleton.StartClient();

        Debug.Log($"[MainMenuUI] 서버 연결 및 Join 시도: {matchId}");
    }

    public void OnClickCreateRoom()
    {
        RoomListUI.Instance?.ShowCreateRoomPopup();
    }
}
