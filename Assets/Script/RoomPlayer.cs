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
    [SyncVar] public string playerName;
    [SyncVar(hook = nameof(OnLeaderChanged))] public bool isLeader = false;
    [SyncVar] public int selectedCharacter = -1;

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

        // 선택 상태 수집
        int[] selectedCharacters = new int[players.Length];
        string[] playerNames = new string[players.Length];

        for (int i = 0; i < players.Length; i++)
        {
            selectedCharacters[i] = players[i].selectedCharacter;
            playerNames[i] = players[i].playerName;
        }

        // 모든 클라이언트에게 버튼 상태 갱신 요청
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null)
            {
                var p = conn.identity.GetComponent<RoomPlayer>();
                p?.TargetUpdateCharacterButtons(conn, selectedCharacters, playerNames); //  이렇게 수정
            }
        }
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
        Debug.Log($"[서버] CmdStartGame 호출됨 by {playerName} (리더: {isLeader})");

        if (!isLeader)
        {
            Debug.LogWarning("[서버] 리더가 아니라 실행 중단");
            return;
        }

        var players = FindObjectsByType<RoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            Debug.Log($"[서버] {p.playerName} 선택 캐릭터 인덱스: {p.selectedCharacter}");

            if (p.selectedCharacter < 0)
            {
                Debug.LogWarning($"[서버] {p.playerName} 캐릭터 미선택 → 게임 시작 중단");
                return;
            }
        }

        var netManager = NetworkManager.singleton as CustomNetworkManager_Server;
        if (netManager != null && netManager.matchRooms.ContainsKey(matchId))
        {
            Debug.Log($"[서버] StartGame() 호출 with matchId: {matchId}");
            netManager.StartGame(matchId);
        }
        else
        {
            Debug.LogError("[서버] StartGame 실패: matchId 불일치");
        }
    }

    [TargetRpc]
    public void TargetStartGame(NetworkConnection target, int characterIndex, string matchId)
    {
        Debug.Log($"[클라이언트] TargetStartGame: idx={characterIndex}, match={matchId}");
        RoomUIManager.Instance?.HideRoomUI();
        // 혹시 남아있을 수 있으니:
        typeof(RoomUIManager).GetField("_startRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(RoomUIManager.Instance, false);
    }


    private void SpawnLocalPlayerCharacter(int characterIndex)
    {
        string prefabName = characterIndex switch
        {
            0 => "Player_ver_AR",
            1 => "Player_ver_DMR",
            2 => "Player_ver_SG",
            3 => "Player_ver_SMG",
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

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint");
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[클라이언트] PlayerSpawnPoint 태그 오브젝트 없음");
            return;
        }

        var sortedPoints = spawnPoints.OrderBy(go => go.name).ToArray();
        Vector3 spawnPos = sortedPoints[Mathf.Clamp(characterIndex, 0, sortedPoints.Length - 1)].transform.position;

        GameObject character = Instantiate(prefab, spawnPos, Quaternion.identity);
        Debug.Log($"[클라이언트] 캐릭터 '{prefabName}' 인스턴스 생성 완료 at {spawnPos}");
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
            0 => "Player_ver_AR",
            1 => "Player_ver_DMR",
            2 => "Player_ver_SG",
            3 => "Player_ver_SMG",
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
            0 => "Player_ver_AR",
            1 => "Player_ver_DMR",
            2 => "Player_ver_SG",
            3 => "Player_ver_SMG",
            _ => null
        };


        var prefab = NetworkManager.singleton.spawnPrefabs
            .FirstOrDefault(go => go.name == prefabName);

        return prefab;
    }

    [TargetRpc]
    public void TargetUpdateCharacterButtons(NetworkConnection target, int[] selectedCharacters, string[] playerNames)
    {
        RoomUIManager.Instance?.UpdateCharacterButtonStates(selectedCharacters, playerNames);
    }

}
