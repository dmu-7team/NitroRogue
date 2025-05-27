using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class RoomSceneUI : MonoBehaviour
{
    public void OnClickLeaveRoom()
    {
        if (NetworkClient.isConnected)
        {
            Debug.Log("[클라이언트] 방 나가기 요청");

            NetworkManager.singleton.StopClient(); // 네트워크 종료
            SceneManager.LoadScene("MainMenuScene"); // 메인메뉴로 복귀
        }
    }
}
