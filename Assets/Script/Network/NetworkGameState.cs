using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class NetworkGameState : NetworkBehaviour
{
    public static NetworkGameState Instance;

    private readonly HashSet<uint> alive = new();
    void Start() { if (!isServer) { enabled = false; return; } }
    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // === 서버 전용 등록/해제 ===
    [Server]
    public void Register(PlayerStats ps)
    {
        if (ps) alive.Add(ps.netId);
    }

    [Server]
    public void Unregister(PlayerStats ps)
    {
        if (ps) alive.Remove(ps.netId);
        if (alive.Count == 0) RpcShowDefeat();
    }

    // === 미션 성공 시 호출 ===
    [Server]
    public void OnMissionSuccess()
    {
        RpcShowVictory();
    }

    // === 클라: 결과 패널 ===
    [ClientRpc]
    void RpcShowVictory()
    {
        UIManager.Instance?.ShowVictoryPanel();
    }

    [ClientRpc]
    void RpcShowDefeat()
    {
        UIManager.Instance?.ShowDefeatPanel();
    }
}
