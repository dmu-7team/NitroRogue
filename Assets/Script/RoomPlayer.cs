using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomPlayer : NetworkBehaviour
{
    [SyncVar] public string matchId;
    [SyncVar(hook = nameof(OnRoomNameChanged))] public string roomName;
    [SyncVar] public int currentPlayers;
    [SyncVar] public int maxPlayers;

    [SyncVar(hook = nameof(OnReadyChanged))] public bool isReady = false;
    [SyncVar] public string playerName = "플레이어";
    [SyncVar(hook = nameof(OnLeaderChanged))] public bool isLeader = false;
    [SyncVar(hook = nameof(OnCharacterSelected))] public int selectedCharacter = -1;

    [System.Serializable]
    public class PlayerInfo
    {
        public string name;
        public bool isLeader;
        public bool isMe;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isLocalPlayer)
        {
            Invoke(nameof(UpdateRoomUI), 0.3f);
            RoomUIManager.Instance?.ShowStartButton(isLeader);
            CmdNotifyUpdateList();
        }

        gameObject.hideFlags = HideFlags.HideInHierarchy;
        gameObject.name = $"[RoomPlayer:{roomName}]";
    }

    private void UpdateRoomUI()
    {
        if (RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.ShowRoom(roomName);
        }
    }

    private void OnRoomNameChanged(string oldName, string newName)
    {
        if (isLocalPlayer && RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.UpdateRoomName(newName);
        }
    }

    private void OnLeaderChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer)
        {
            RoomUIManager.Instance?.ShowStartButton(newVal);
        }
    }

    private void OnReadyChanged(bool oldReady, bool newReady)
    {
        RoomUIManager.Instance?.UpdatePlayerReadyStatus(this, newReady);
    }

    private void OnCharacterSelected(int oldVal, int newVal)
    {
        if (isLocalPlayer)
        {
            RoomUIManager.Instance?.UpdateCharacterButtonStates();
        }
    }

    [Command]
    public void CmdSetReady(bool isReady)
    {
        this.isReady = isReady;
    }

    [Command]
    public void CmdNotifyUpdateList()
    {
        if (NetworkManager.singleton is CustomNetworkManager_Server manager)
        {
            manager.BroadcastPlayerList(matchId);
        }
    }

    [TargetRpc]
    public void TargetRebuildPlayerList(List<PlayerInfo> players)
    {
        RoomUIManager.Instance.ClearPlayerList();
        foreach (var info in players)
        {
            RoomUIManager.Instance.AddPlayerToList(info.name, info.isLeader, info.isMe);
        }
    }

    [ClientRpc]
    public void RpcUpdatePlayerList()
    {
        RoomUIManager.Instance?.RebuildPlayerList();
    }

    [Command]
    public void CmdSelectCharacter(int index)
    {
        var players = FindObjectsByType<RoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != this && p.selectedCharacter == index)
                return; // 중복 선택 불가
        }

        selectedCharacter = index;
    }

    public void OnStartGameButtonClicked()
    {
        var players = FindObjectsByType<RoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.selectedCharacter == -1)
            {
                Debug.Log("모든 플레이어가 캐릭터를 선택해야 합니다.");
                return;
            }
        }

        players.First(p => p.isLeader).CmdStartGame();
    }

    [Command]
    public void CmdStartGame()
    {
        if (!isLeader) return;

        var players = FindObjectsByType<RoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.selectedCharacter < 0) return;
        }

        var netManager = NetworkManager.singleton as CustomNetworkManager_Server;
        if (netManager != null && netManager.matchRooms.ContainsKey(matchId))
        {
            netManager.StartGame(matchId);
        }
    }

    [TargetRpc]
    public void TargetStartGame(NetworkConnection target, int characterIndex, string matchId)
    {
        Debug.Log($"[클라이언트] TargetStartGame 호출됨 - 캐릭터: {characterIndex}, 매치ID: {matchId}");

        // 메인메뉴-룸 UI 비활성화 및 인게임 UI 전환
        RoomUIManager.Instance?.SwitchToGameUI();

        // 스폰 처리만 진행
        SpawnLocalPlayerCharacter(characterIndex);
    }

    private void SpawnLocalPlayerCharacter(int characterIndex)
    {
        // 프리팹 이름 결정
        string prefabName = characterIndex switch
        {
            0 => "Player_ver_EF",
            1 => "Player_ver_RBM",
            2 => "Player_ver_RBM2",
            _ => null
        };

        if (prefabName == null)
        {
            Debug.LogError($"[클라이언트] 잘못된 캐릭터 인덱스: {characterIndex}");
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(prefabName);
        if (prefab == null)
        {
            Debug.LogError($"[클라이언트] Resources에서 프리팹 {prefabName} 로드 실패");
            return;
        }

        // 스폰 위치
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint");
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[클라이언트] PlayerSpawnPoint 태그 오브젝트 없음");
            return;
        }

        // 이름 기준 정렬 후 인덱스로 선택
        var sortedPoints = spawnPoints.OrderBy(go => go.name).ToArray();
        Vector3 spawnPos = sortedPoints[Mathf.Clamp(characterIndex, 0, sortedPoints.Length - 1)].transform.position;

        // 인스턴스 생성 (주의: 이건 클라이언트에서만 사용. 서버와 별개)
        GameObject character = Instantiate(prefab, spawnPos, Quaternion.identity);
        Debug.Log($"[클라이언트] 캐릭터 '{prefabName}' 인스턴스 생성 완료 at {spawnPos}");

        // 카메라나 UI 연결은 여기서 추가로 해도 됨
    }



    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (scene.name != "Game") return;

        RoomPlayer localPlayer = NetworkClient.connection.identity?.GetComponent<RoomPlayer>();
        if (localPlayer == null) return;

        int characterIndex = localPlayer.selectedCharacter;
        string prefabName = characterIndex switch
        {
            0 => "Player_ver_EF",
            1 => "Player_ver_RBM",
            2 => "Player_ver_RBM2",
            _ => null
        };

        GameObject prefab = Resources.Load<GameObject>(prefabName);
        if (prefab == null) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint");
        Vector3 spawnPos = Vector3.zero;

        if (spawnPoints.Length > 0)
        {
            var sorted = spawnPoints.OrderBy(sp => sp.name).ToArray();
            int index = Array.IndexOf(sorted, sorted.FirstOrDefault(sp => sp.name.Contains(localPlayer.playerName)));
            spawnPos = sorted[Mathf.Clamp(index, 0, sorted.Length - 1)].transform.position;
        }

        GameObject character = Instantiate(prefab, spawnPos, Quaternion.identity);
        Debug.Log($"[클라이언트] 캐릭터 {prefabName} 생성 완료");
    }

    public void SetMatchInfo(string id, string name)
    {
        matchId = id;
        roomName = name;
    }

    public GameObject GetPrefabForCharacter(int index)
    {
        string prefabName = index switch
        {
            0 => "Player_ver_EF",
            1 => "Player_ver_RBM",
            2 => "Player_ver_RBM2",
            _ => null
        };

        var prefab = NetworkManager.singleton.spawnPrefabs
            .FirstOrDefault(go => go.name == prefabName);

        return prefab;
    }

    [TargetRpc]
    public void TargetUpdateCharacterButtons()
    {
        RoomUIManager.Instance?.UpdateCharacterButtonStates();
    }
}
