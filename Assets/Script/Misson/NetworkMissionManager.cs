using UnityEngine;
using Mirror;
using System;

public class NetworkMissionManager : NetworkBehaviour
{
    public static NetworkMissionManager Instance { get; private set; }

    // 팀 누적 처치 수(>=1 이면 완료)
    [SyncVar(hook = nameof(OnTeamKillChanged))]
    private int teamKill;

    public static event Action<int> OnTeamKillUpdated; // 클라 UI 리스너

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    [Server]
    public void AddTeamKill(int amount = 1)
    {
        if (amount > 0) teamKill += amount;
    }

    // (선택) bool로 쓰고 싶으면: [SyncVar(hook=nameof(OnCompleted))] bool missionCompleted;
    // [Server] public void MarkCompleted(){ missionCompleted = true; }

    void OnTeamKillChanged(int oldVal, int newVal)
    {
        if (isClient) OnTeamKillUpdated?.Invoke(newVal);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        OnTeamKillUpdated?.Invoke(teamKill); // 늦게 들어온 클라 초기 UI 세팅
    }
    public static NetworkMissionManager GetOrFind()
    {
        if (Instance != null) return Instance;
        return FindFirstObjectByType<NetworkMissionManager>();
    }
    // 매치 분리를 할 거면 아래처럼 NetworkMatch를 붙이고 matchId를 세팅해줘야 함.
    // public NetworkMatch networkMatch; // 컴포넌트로 추가해 두고 서버에서 matchId 주입
}
