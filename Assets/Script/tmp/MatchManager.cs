using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System; // Guid를 사용하기 위해 추가

/// <summary>
/// 서버 전용 매치 관리자 (최종 버전):
/// - 싱글톤 패턴이 제거됨.
/// - 정적 Dictionary(ActiveMatches)를 통해 각 매치의 인스턴스를 관리.
/// - 토큰 없는 BurstSpawner와 함께 작동하도록 수정됨.
/// </summary>
[RequireComponent(typeof(NetworkMatch))]
public class MatchManager : NetworkBehaviour
{
    // [신규] 서버에서 실행 중인 모든 매치를 관리하는 '안내 데스크' 역할의 정적 Dictionary
    public static readonly Dictionary<Guid, MatchManager> ActiveMatches = new Dictionary<Guid, MatchManager>();

    [Header("Configuration")]
    [SerializeField] private GameModeConfigSO gameModeConfig;
    [SerializeField] private int startStageId = 0;
    [SerializeField] private MatchSpaceNavMesh matchNavMesh;
    [SerializeField] private BurstSpawner spawner;

    [Header("Match State")]
    [SyncVar] private int currentStageId;
    [SyncVar] private int elapsedMinutes;
    private float matchStartTime;
    private int lastAppliedMinute = -1;

    // 이 매치의 고유 ID
    private Guid matchId;

    // 이 매치에 속한 플레이어 목록
    private readonly List<Transform> playerTransforms = new List<Transform>();
    private readonly HashSet<NetworkIdentity> aliveEnemies = new();

    [SyncVar] private bool isBossAlive;
    private GameObject currentBoss;
    private int killsThisStage = 0;

    private BurstSpawner.StepParams currentDifficultyStep;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // NetworkMatch 컴포넌트에서 이 매치의 고유 ID를 가져옵니다.
        matchId = GetComponent<NetworkMatch>().matchId;
        // 정적 Dictionary에 자기 자신을 등록합니다.
        ActiveMatches[matchId] = this;

        matchStartTime = Time.time;
        currentStageId = startStageId;
        isBossAlive = false;
        killsThisStage = 0;

        if (!spawner) spawner = GetComponent<BurstSpawner>();

        // 초기 설정 (플레이어는 아직 없으므로 목록은 비어있음)
        UpdateDifficultyScaling();
        ApplyStageContent();
        spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);
    }

    public override void OnStopServer()
    {
        // 서버가 멈추거나 매치가 종료될 때 Dictionary에서 자신을 제거합니다.
        if (ActiveMatches.ContainsKey(matchId))
        {
            ActiveMatches.Remove(matchId);
        }
        base.OnStopServer();
    }

    void Update()
    {
        if (!isServer) return;

        int currentMinutes = Mathf.FloorToInt((Time.time - matchStartTime) / 60f);
        if (currentMinutes != elapsedMinutes) elapsedMinutes = currentMinutes;

        // 분이 바뀔 때마다 난이도와 스포너 설정을 갱신합니다.
        if (elapsedMinutes != lastAppliedMinute)
        {
            UpdateDifficultyScaling();
            spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);
        }

        // 플레이어가 한 명이라도 있을 때만 스포너를 작동시킵니다.
        if (playerTransforms.Count > 0)
        {
            spawner.ServerTick(Time.deltaTime, playerTransforms);
        }
    }

    // 플레이어가 자신을 등록/해제할 수 있는 공개 메서드
    [Server]
    public void AddPlayer(Transform playerTransform)
    {
        if (!playerTransforms.Contains(playerTransform))
        {
            playerTransforms.Add(playerTransform);
            Debug.Log($"Player added to match {matchId}. Total players: {playerTransforms.Count}");
        }
    }

    [Server]
    public void RemovePlayer(Transform playerTransform)
    {
        if (playerTransforms.Remove(playerTransform))
        {
            Debug.Log($"Player removed from match {matchId}. Total players: {playerTransforms.Count}");
        }
    }

    [Server]
    private void UpdateDifficultyScaling()
    {
        lastAppliedMinute = elapsedMinutes;
        if (!gameModeConfig.TryGetStep(elapsedMinutes, out GameModeConfigSO.MinuteStep s)) return;

        int minutesSince = Mathf.Max(0, elapsedMinutes - s.thresholdMinute);

        int eliteMax = (s.elitePercentMax > 0) ? s.elitePercentMax : 100;
        int elite = s.elitePercent + s.elitePercentPerMin * minutesSince;
        elite = Mathf.Clamp(elite, 0, eliteMax);

        float hp = (s.hpMul > 0 ? s.hpMul : 1f) + s.hpMulPerMin * minutesSince;
        float dmg = (s.dmgMul > 0 ? s.dmgMul : 1f) + s.dmgMulPerMin * minutesSince;
        float move = (s.moveSpeedMul > 0 ? s.moveSpeedMul : 1f) + s.moveSpeedMulPerMin * minutesSince;

        if (s.hpMulMax > 0) hp = Mathf.Min(hp, s.hpMulMax);
        if (s.dmgMulMax > 0) dmg = Mathf.Min(dmg, s.dmgMulMax);
        if (s.moveSpeedMulMax > 0) move = Mathf.Min(move, s.moveSpeedMulMax);

        hp = Mathf.Max(0.1f, hp);
        dmg = Mathf.Max(0.1f, dmg);
        move = Mathf.Max(0.1f, move);

        // BurstSpawner의 StepParams 구조체에 맞게 spm 관련 할당을 제거
        currentDifficultyStep = new BurstSpawner.StepParams
        {
            burstMin = Mathf.Max(1, s.burstMin),
            burstMax = Mathf.Max(s.burstMin, s.burstMax),
            burstMode = Mathf.Clamp(s.burstMode, s.burstMin, s.burstMax),
            cdMin = Mathf.Max(0.1f, s.burstCooldownMin),
            cdMax = Mathf.Max(s.burstCooldownMin, s.burstCooldownMax),
            maxAliveHardCap = Mathf.Max(0, s.maxAliveHardCap),
            elitePercent = elite,
            hpMul = hp,
            dmgMul = dmg,
            moveMul = move
        };
    }

    [Server]
    private void ApplyStageContent()
    {
        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null) return;

        if (matchNavMesh) matchNavMesh.ApplyStageNavMesh(stage);
        RpcStageChanged(currentStageId);
    }

    [Server]
    public bool TryRegisterEnemy(NetworkIdentity id)
    {
        if (!id) return false;
        int cap = currentDifficultyStep.maxAliveHardCap;
        if (cap > 0 && aliveEnemies.Count >= cap) return false;
        return aliveEnemies.Add(id);
    }

    [Server]
    public void UnregisterEnemy(NetworkIdentity id)
    {
        if (id) aliveEnemies.Remove(id);
    }

    [Server]
    public void OnEnemyKilled()
    {
        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null) return;

        if (!isBossAlive && stage.killsToBoss > 0)
        {
            killsThisStage++;
            if (killsThisStage >= stage.killsToBoss)
            {
                killsThisStage = 0;
                RequestBossSpawn();
            }
        }
    }

    [Server]
    public void RequestBossSpawn()
    {
        if (isBossAlive) return;
        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null || stage.mapSpawnSet == null || stage.mapSpawnSet.bossPrefab == null) return;

        GameObject point = GameObject.FindWithTag(stage.mapSpawnSet.bossSpawnPointTag);
        Vector3 pos = point ? point.transform.position : Vector3.zero;
        Quaternion rot = point ? point.transform.rotation : Quaternion.identity;

        currentBoss = Instantiate(stage.mapSpawnSet.bossPrefab, pos, rot);
        NetworkServer.Spawn(currentBoss);
        isBossAlive = true;

        var h = currentBoss.GetComponent<EnemyBase>();
        if (h) h.OnDied += OnBossDied;
    }

    [Server]
    private void OnBossDied()
    {
        isBossAlive = false;
        currentBoss = null;
        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null) return;

        if (stage.nextStageId < 0)
        {
            RpcGameOver();
            return;
        }

        currentStageId = stage.nextStageId;
        killsThisStage = 0;

        ApplyStageContent();
        UpdateDifficultyScaling();
        spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);
    }

    [ClientRpc] void RpcStageChanged(int id) { /* 클라이언트 UI/맵 로딩 등 처리 */ }
    [ClientRpc] void RpcGameOver() { /* 게임 종료 연출 */ }
}