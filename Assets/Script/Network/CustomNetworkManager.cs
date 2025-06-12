using Mirror;
using NetworkMessages;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
    public static string matchIdToJoin;
    private static bool joinSent = false;

    [Header("클라이언트 프리팹 등록")]
    [SerializeField] private GameObject playerPrefab_AR;
    [SerializeField] private GameObject playerPrefab_DMR;
    [SerializeField] private GameObject playerPrefab_SG;
    [SerializeField] private GameObject playerPrefab_SMG;


    [Header("클라이언트 몬스터 프리팹 등록")]
    [SerializeField] private GameObject[] monsterPrefabs; // 인스펙터에서 할당
    [SerializeField] private GameObject treasureChestPrefab;
    public override void OnStartClient()
    {
        base.OnStartClient();
        joinSent = false;

        // 플레이어 프리팹 등록 (신규 직업)
        if (playerPrefab_AR != null) NetworkClient.RegisterPrefab(playerPrefab_AR);
        if (playerPrefab_DMR != null) NetworkClient.RegisterPrefab(playerPrefab_DMR);
        if (playerPrefab_SG != null) NetworkClient.RegisterPrefab(playerPrefab_SG);
        if (playerPrefab_SMG != null) NetworkClient.RegisterPrefab(playerPrefab_SMG);
        if (treasureChestPrefab != null)NetworkClient.RegisterPrefab(treasureChestPrefab);
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

    public override void Start()
    {
        base.Start();

        // 테스트용: 서버 IP 하드코딩해서 자동 접속
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            NetworkManager.singleton.networkAddress = "121.157.99.154"; // ← 너 공인 IP
            NetworkManager.singleton.StartClient();

            Debug.Log($"[AutoConnect] 서버 자동 접속 시도 중 → {NetworkManager.singleton.networkAddress}");
        }
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
