using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// 서버 전용 매치 관리자
/// </summary>
[RequireComponent(typeof(NetworkMatch))]
public class MatchManager : NetworkBehaviour
{
    public static event Action<MatchManager> OnManagerReady;
    public static readonly Dictionary<Guid, MatchManager> ActiveMatches = new Dictionary<Guid, MatchManager>();
    public static event Action<Guid, bool> OnMatchEnded; // (matchId, isVictory)
    private bool ended; // 중복 종료 방지

    [HideInInspector]
    public Transform startPoint;

    [Header("Configuration")]
    [SerializeField] private GameModeConfigSO gameModeConfig;
    [SerializeField] private int startStageId = 0;
    private BurstSpawner spawner;

    [Header("Match State")]
    [SyncVar] private int currentStageId;
    [SyncVar] private int elapsedMinutes;
    private float matchStartTime;
    private int lastAppliedMinute = -1;

    private Guid matchId;

    private readonly List<Transform> playerTransforms = new List<Transform>();
    private readonly HashSet<NetworkIdentity> aliveEnemies = new();

    [SyncVar] private bool isBossAlive;
    private GameObject currentBoss;
    private int killsThisStage = 0;

    private BurstSpawner.StepParams currentDifficultyStep;

    private GameObject currentServerLogicInstance;
    private GameObject currentClientMapInstance;

    [Server]
    public void EndMatch(bool isVictory)
    {
        if (ended) return;
        ended = true;

        var guid = GetComponent<NetworkMatch>().matchId;

        // 1) 결과창 브로드캐스트 (승리/패배 모두 여기서 처리 가능)
        foreach (var ni in NetworkServer.spawned.Values)
        {
            if (!ni) continue;
            var nm = ni.GetComponent<NetworkMatch>();
            if (nm == null || nm.matchId != guid) continue;

            var ps = ni.GetComponent<PlayerStats>();
            var conn = ps ? ps.connectionToClient : null;
            if (ps != null && conn != null)
            {
                if (isVictory) ps.TargetShowVictory(conn);
                else ps.TargetShowDefeat(conn);
            }
        }

        // 2) NetworkManager에게 “이 매치 끝남” 알림 (오브젝트 정리/시작 지점 반환)
        OnMatchEnded?.Invoke(guid, isVictory);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        matchId = GetComponent<NetworkMatch>().matchId;
        ActiveMatches[matchId] = this;

        if (!spawner) spawner = GetComponent<BurstSpawner>();

        OnManagerReady?.Invoke(this);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        if (NetworkManager.singleton is CustomNetworkManager_Server customManager)
        {
            if (startPoint != null) customManager.FreeUpMatchPoint(startPoint);
        }
        if (ActiveMatches.ContainsKey(matchId))
        {
            ActiveMatches.Remove(matchId);
        }
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

    // NetworkManager로부터 플레이어 목록을 받아 게임을 시작하는 메서드
    [Server]
    public void StartMatchWithPlayers(List<Transform> initialPlayers)
    {
        playerTransforms.Clear();
        playerTransforms.AddRange(initialPlayers);
        Debug.Log($"Match [{matchId}] starting with {initialPlayers.Count} players.");

        matchStartTime = Time.time;
        currentStageId = startStageId;
        isBossAlive = false;
        killsThisStage = 0;

        // 초기
        ApplyStageContent();
        UpdateDifficultyScaling();
        spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);

        TeleportPlayersToSpawnPoints();
    }

    // 플레이어들을 현재 스테이지의 스폰 포인트로 텔레포트시키는 메서드
    [Server]
    private void TeleportPlayersToSpawnPoints()
    {
        var spawnPoints = GetComponentsInChildren<Transform>()
                                .Where(t => t.CompareTag("PlayerSpawnPoint"))
                                .ToArray();

        if (spawnPoints.Length == 0)
        {
            Debug.LogError($"[MatchManager] No spawn points found in match [{matchId}] for stage {currentStageId}!");
            return;
        }

        for (int i = 0; i < playerTransforms.Count; i++)
        {
            Transform playerTransform = playerTransforms[i];
            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];

            // 오너 커넥션 얻기
            var ni = playerTransform.GetComponentInParent<NetworkIdentity>();
            var conn = ni != null ? ni.connectionToClient : null;
            if (conn == null)
            {
                Debug.LogWarning("Teleport skipped: owner connection not found.");
                continue;
            }

            // 플레이어 오브젝트에서 TargetRpc 호출
            var tele = playerTransform.GetComponent<PlayerStats>();
            if (tele != null)
            {
                tele.TargetTeleport(conn, spawnPoint.position, spawnPoint.rotation);
            }
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
        if (stage == null || stage.serverLogicPrefab == null) return;

        if (currentServerLogicInstance != null)
        {
            Destroy(currentServerLogicInstance);
        }

        currentServerLogicInstance = Instantiate(stage.serverLogicPrefab, transform.position, transform.rotation, transform);

        RpcLoadClientMap(currentStageId);
    }

    [ClientRpc]
    public void RpcLoadClientMap(int stageId)
    {
        if (gameModeConfig == null)
        {
            Debug.LogError("[Client] FATAL: gameModeConfig field is NULL on the MatchManager! Check the prefab inspector.");
            return;
        }

        var stage = gameModeConfig.FindStage(stageId);
        if (stage == null || stage.clientMapPrefab == null) return;

        if (currentClientMapInstance != null)
        {
            Destroy(currentClientMapInstance);
        }

        currentClientMapInstance = Instantiate(stage.clientMapPrefab, transform.position, transform.rotation, transform);
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
        currentBoss.GetComponent<NetworkMatch>().matchId = matchId;
        NetworkServer.Spawn(currentBoss);
        isBossAlive = true;

        var h = currentBoss.GetComponent<EnemyBase>();
        if (h) h.OnDied += OnBossDied;
    }

    [Server]
    private void OnBossDied()
    {
        Debug.Log("보스 사망 받음");
        isBossAlive = false;
        currentBoss = null;
        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null) return;

        if (stage.nextStageId < 0)
        {
            // 기존: RpcGameOver();
            EndMatch(true);        // ★ 승리 처리로 교체
            return;
        }

        currentStageId = stage.nextStageId;
        killsThisStage = 0;

        ApplyStageContent();
        UpdateDifficultyScaling();
        spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);
    }


    [ClientRpc] void RpcGameOver()
    {
        /* 게임 종료 연출 */
        Debug.Log("게임 종료 클라 받음");
    }
}