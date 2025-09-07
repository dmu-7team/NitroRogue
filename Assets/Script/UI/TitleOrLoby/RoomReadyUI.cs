using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class RoomReadyUI : MonoBehaviour
{
    public Button readyButton;

    private RoomPlayer localPlayer;

    void Start()
    {
        readyButton?.onClick.AddListener(OnClickReady);

        // 로컬 플레이어 찾기
        if (NetworkClient.connection != null && NetworkClient.connection.identity != null)
        {
            localPlayer = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
        }

        if (localPlayer == null)
        {
            Debug.LogError("[RoomReadyUI] RoomPlayer를 찾을 수 없습니다.");
            readyButton.interactable = false;
        }
    }

    public void OnClickReady()
    {
        if (localPlayer != null)
        {
            Debug.Log("[RoomReadyUI] Ready 버튼 클릭됨");
            localPlayer.CmdSetReady(true);
            readyButton.interactable = false; // 클릭 후 비활성화
        }
    }
}
