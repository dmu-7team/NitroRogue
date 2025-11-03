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

    [SyncVar(hook = nameof(OnPlayerNameChanged))]                 // ★ hook 추가
    public string playerName;

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
        RoomUIManager.Instance?.ShowRoom(roomName);
    }

    private void OnRoomNameChanged(string oldName, string newName)
    {
        if (isLocalPlayer)
            RoomUIManager.Instance?.UpdateRoomName(newName);
    }

    private void OnLeaderChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer)
            RoomUIManager.Instance?.ShowStartButton(newVal);
    }

    private void OnReadyChanged(bool oldReady, bool newReady)
    {
        RoomUIManager.Instance?.UpdatePlayerReadyStatus(this, newReady);
    }

    private void OnPlayerNameChanged(string oldV, string newV)     // ★ UI 리빌드 트리거
    {
        RoomUIManager.Instance?.RebuildPlayerList();
    }

    [Command]
    public void CmdSetReady(bool isReady) => this.isReady = isReady;

    [Command]
    public void CmdNotifyUpdateList()
    {
        if (NetworkManager.singleton is CustomNetworkManager_Server manager)
            manager.BroadcastPlayerList(matchId);
    }

    [TargetRpc]
    public void TargetRebuildPlayerList(List<PlayerInfo> players)
    {
        RoomUIManager.Instance.ClearPlayerList();
        foreach (var info in players)
            RoomUIManager.Instance.AddPlayerToList(info.name, info.isLeader, info.isMe);
    }

    [ClientRpc]
    public void RpcUpdatePlayerList() => RoomUIManager.Instance?.RebuildPlayerList();

    [Command]
    public void CmdSelectCharacter(int index)
    {
        var server = NetworkManager.singleton as CustomNetworkManager_Server;
        if (server == null) return;
        if (!server.matchRooms.TryGetValue(matchId, out var roomPlayers) || roomPlayers == null) return;

        foreach (var p in roomPlayers)
            if (p && p != this && p.selectedCharacter == index) return;

        selectedCharacter = index;
        server.BroadcastSelections(matchId);
    }

    public void OnStartGameButtonClicked()
    {
        var server = NetworkManager.singleton as CustomNetworkManager_Server;
        if (server == null) return;
        if (!server.matchRooms.TryGetValue(matchId, out var roomPlayers) || roomPlayers == null) return;

        foreach (var p in roomPlayers)
        {
            if (p.selectedCharacter == -1)
            {
                Debug.Log("모든 플레이어가 캐릭터를 선택해야 합니다.");
                return;
            }
        }

        roomPlayers.First(p => p.isLeader).CmdStartGame();
    }

    [Command]
    public void CmdStartGame()
    {
        Debug.Log($"[서버] CmdStartGame 호출됨 by {playerName} (리더: {isLeader})");
        if (!isLeader) { Debug.LogWarning("[서버] 리더가 아니라 실행 중단"); return; }

        var server = NetworkManager.singleton as CustomNetworkManager_Server;
        if (server == null) return;
        if (!server.matchRooms.TryGetValue(matchId, out var roomPlayers) || roomPlayers == null) return;

        foreach (var p in roomPlayers)
        {
            Debug.Log($"[서버] {p.playerName} 선택 캐릭터 인덱스: {p.selectedCharacter}");
            if (p.selectedCharacter < 0)
            {
                Debug.LogWarning($"[서버] {p.playerName} 캐릭터 미선택 → 게임 시작 중단");
                return;
            }
        }

        if (server.matchRooms.ContainsKey(matchId))
        {
            Debug.Log($"[서버] StartGame() 호출 with matchId: {matchId}");
            server.StartGame(matchId);
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
        typeof(RoomUIManager).GetField("_startRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(RoomUIManager.Instance, false);
    }

    // (로컬 리소스 스폰/씬 전환 관련 메서드는 기존 유지 — 네트워크 권위 스폰과 혼용 시 주의)
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
    public void TargetUpdateCharacterButtons(NetworkConnection conn, string matchId, int[] selected, string[] names)
    {
        if (this.matchId != matchId) return;
        RoomUIManager.Instance?.UpdateCharacterButtonStates(selected, names);
    }
}
