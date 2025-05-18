using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    // 게임 시작 버튼에 연결
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
}