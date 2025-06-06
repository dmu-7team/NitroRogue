using Mirror;
using NetworkMessages;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
    public static string matchIdToJoin;
    private static bool joinSent = false;

    [Header("클라이언트 프리팹 등록")]
    [SerializeField] private GameObject playerPrefab_EF;
    [SerializeField] private GameObject playerPrefab_RBM;
    [SerializeField] private GameObject playerPrefab_RBM2;

    [Header("클라이언트 몬스터 프리팹 등록")]
    [SerializeField] private GameObject[] monsterPrefabs; // 인스펙터에서 할당

    public override void OnStartClient()
    {
        base.OnStartClient();
        joinSent = false;

        // 플레이어 프리팹 등록
        if (playerPrefab_EF != null) NetworkClient.RegisterPrefab(playerPrefab_EF);
        if (playerPrefab_RBM != null) NetworkClient.RegisterPrefab(playerPrefab_RBM);
        if (playerPrefab_RBM2 != null) NetworkClient.RegisterPrefab(playerPrefab_RBM2);

        // 몬스터 프리팹 등록
        foreach (var monster in monsterPrefabs)
        {
            if (monster != null)
            {
                Debug.Log($"[Client] 몬스터 프리팹 등록됨: {monster.name}");
                NetworkClient.RegisterPrefab(monster);
            }
        }

        // 메시지 핸들러
        NetworkClient.RegisterHandler<JoinResultMessage>(OnJoinResultMessageReceived);
        NetworkClient.RegisterHandler<RoomListSyncMessage>(msg => RoomListUI.Instance.OnRoomListSyncMessageReceived(msg));
    }


    public override void OnClientConnect()
    {
        base.OnClientConnect();

        if (!string.IsNullOrEmpty(RoomListUI.matchIdToJoin))
        {
            Debug.Log($"[Client] OnClientConnect → 자동 참가 요청: {RoomListUI.matchIdToJoin}");
            var joinMsg = new JoinMatchMessage
            {
                matchId = RoomListUI.matchIdToJoin,
                roomName = RoomListUI.matchIdToJoin
            };
            NetworkClient.Send(joinMsg);
        }
        else
        {
            Debug.Log("[Client] matchIdToJoin이 비어 있음 → 자동 참가 생략");
        }
    }

    private void OnJoinResultMessageReceived(JoinResultMessage msg)
    {
        if (msg.success)
        {
            Debug.Log($"[Client] 방 참가 성공: {msg.roomName} ({msg.matchId})");
            RoomUIManager.Instance.ShowRoom(msg.roomName);
        }
        else
        {
            Debug.LogWarning("[Client] 방 참가 실패");
        }
    }
}
