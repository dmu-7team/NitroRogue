using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using Mirror;
public struct DefeatMessage : NetworkMessage { public string matchId; }
public struct VictoryMessage : NetworkMessage { public string matchId; }
[RequireComponent(typeof(NetworkMatch))]
public class MatchManager : NetworkBehaviour
{
    public static event Action<MatchManager> OnManagerReady;
    public static readonly System.Collections.Generic.Dictionary<Guid, MatchManager> ActiveMatches
        = new System.Collections.Generic.Dictionary<Guid, MatchManager>();
    public static event Action<Guid, bool> OnMatchEnded; // (matchId, isVictory)

    private bool ended; // 중복 종료 방지
    [HideInInspector] public Transform startPoint;

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

    private readonly System.Collections.Generic.List<Transform> playerTransforms = new();
    private readonly System.Collections.Generic.HashSet<NetworkIdentity> aliveEnemies = new();

    [SyncVar] private bool isBossAlive;
    private GameObject currentBoss;
    private int killsThisStage = 0;

    private BurstSpawner.StepParams currentDifficultyStep;
    private GameObject currentServerLogicInstance;
    private GameObject currentClientMapInstance;

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
        if (ActiveMatches.ContainsKey(matchId))
            ActiveMatches.Remove(matchId);
    }

    void Update()
    {
        if (!isServer) return;

        int currentMinutes = Mathf.FloorToInt((Time.time - matchStartTime) / 60f);
        if (currentMinutes != elapsedMinutes) elapsedMinutes = currentMinutes;

        if (elapsedMinutes != lastAppliedMinute)
        {
            UpdateDifficultyScaling();
            spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);
        }

        if (playerTransforms.Count > 0)
            spawner.ServerTick(Time.deltaTime, playerTransforms);
    }

    // === 엔드 매치: 패널 먼저 → 한 프레임 뒤 이벤트만 통지 (정리는 서버 매니저) ===
    [Server]
    public void EndMatch(bool isVictory)
    {
        if (ended) return;
        ended = true;

        var guid = GetComponent<NetworkMatch>().matchId;

        // ★ 모든 해당 플레이어의 connection에 메시지 전송 (오브젝트 파괴와 무관)
        foreach (var ni in NetworkServer.spawned.Values)
        {
            if (!ni) continue;
            var nm = ni.GetComponent<NetworkMatch>();
            if (nm == null || nm.matchId != guid) continue;

            var conn = ni.connectionToClient;
            if (conn == null) continue;

            if (isVictory) conn.Send(new VictoryMessage { matchId = guid.ToString() });
            else conn.Send(new DefeatMessage { matchId = guid.ToString() });
        }

        // 한 프레임 뒤에 서버 정리(네트 객체 파괴)
        StartCoroutine(NotifyEndedNextFrame(guid, isVictory));
    }

    [Server]
    private IEnumerator NotifyEndedNextFrame(Guid guid, bool isVictory)
    {
        yield return null; // 필요 시 WaitForSeconds(0.5f~2f)
        OnMatchEnded?.Invoke(guid, isVictory); // ★ 정리는 CustomNetworkManager_Server가 수행
    }

    // 플레이어 등록/해제
    [Server] public void AddPlayer(Transform t) { if (!playerTransforms.Contains(t)) playerTransforms.Add(t); }
    [Server] public void RemovePlayer(Transform t) { playerTransforms.Remove(t); }

    // 서버 매니저가 호출
    [Server]
    public void StartMatchWithPlayers(System.Collections.Generic.List<Transform> initialPlayers)
    {
        UIManager.Instance?.ResetAllUI();
        UIManager.Instance?.EnterGameplayHUD();

        playerTransforms.Clear();
        playerTransforms.AddRange(initialPlayers);

        matchStartTime = Time.time;
        currentStageId = startStageId;
        isBossAlive = false;
        killsThisStage = 0;

        ApplyStageContent();
        UpdateDifficultyScaling();
        spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);

        TeleportPlayersToSpawnPoints();
    }

    [Server]
    private void TeleportPlayersToSpawnPoints()
    {
        var spawnPoints = GetComponentsInChildren<Transform>()
            .Where(t => t.CompareTag("PlayerSpawnPoint")).ToArray();

        if (spawnPoints.Length == 0)
        {
            Debug.LogError($"[MatchManager] No spawn points for stage {currentStageId}!");
            return;
        }

        for (int i = 0; i < playerTransforms.Count; i++)
        {
            Transform playerTransform = playerTransforms[i];
            Transform sp = spawnPoints[i % spawnPoints.Length];

            var ni = playerTransform.GetComponentInParent<NetworkIdentity>();
            var conn = ni != null ? ni.connectionToClient : null;
            if (conn == null) { Debug.LogWarning("Teleport skipped: no owner conn"); continue; }

            var ps = playerTransform.GetComponent<PlayerStats>();
            if (ps != null) ps.TargetTeleport(conn, sp.position, sp.rotation);
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
            Destroy(currentServerLogicInstance);

        currentServerLogicInstance = Instantiate(stage.serverLogicPrefab, transform.position, transform.rotation, transform);
        RpcLoadClientMap(currentStageId);
    }

    [ClientRpc]
    public void RpcLoadClientMap(int stageId)
    {
        if (gameModeConfig == null)
        {
            Debug.LogError("[Client] FATAL: gameModeConfig is NULL on MatchManager!");
            return;
        }

        var stage = gameModeConfig.FindStage(stageId);
        if (stage == null || stage.clientMapPrefab == null) return;

        if (currentClientMapInstance != null)
            Destroy(currentClientMapInstance);

        currentClientMapInstance = Instantiate(stage.clientMapPrefab, transform.position, transform.rotation, transform);
    }

    [Server]
    public bool TryRegisterEnemy(NetworkIdentity id)
    { if (!id) return false; int cap = currentDifficultyStep.maxAliveHardCap; if (cap > 0 && aliveEnemies.Count >= cap) return false; return aliveEnemies.Add(id); }
    [Server] public void UnregisterEnemy(NetworkIdentity id) { if (id) aliveEnemies.Remove(id); }

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
        isBossAlive = false;
        currentBoss = null;
        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null) return;

        if (stage.nextStageId < 0)
        {
            EndMatch(true);   // 승리
            return;
        }

        currentStageId = stage.nextStageId;
        killsThisStage = 0;

        ApplyStageContent();
        UpdateDifficultyScaling();
        spawner.ApplyStep(currentDifficultyStep, gameModeConfig.FindStage(currentStageId), gameModeConfig.spawnRule);
    }
}
