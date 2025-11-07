using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using Mirror;
using System.Collections.Generic;
//using UnityEditor;
using UnityEngine.Tilemaps;
// struct DefeatMessage : NetworkMessage { public string matchId; }
//public struct VictoryMessage : NetworkMessage { public string matchId; }
[RequireComponent(typeof(NetworkMatch))]
public class MatchManager : NetworkBehaviour
{
    public static event Action<MatchManager> OnManagerReady;
    public static readonly Dictionary<Guid, MatchManager> ActiveMatches = new Dictionary<Guid, MatchManager>();
    public static event Action<List<PlayerMatchRecord>, bool> OnMatchSummaryReady;
    public static event Action<Guid, bool> OnMatchEnded;
    public static event Action<int, float> OnTopRankUpdated;

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

    private readonly List<Transform> playerTransforms = new();
    private readonly HashSet<NetworkIdentity> aliveEnemies = new();

    [SyncVar] private bool isBossAlive;
    private GameObject currentBoss;
    private int killsThisStage = 0;

    private BurstSpawner.StepParams currentDifficultyStep;
    private GameObject currentServerLogicInstance;
    private GameObject currentClientMapInstance;

    private readonly HashSet<int> loadedClients = new();

    private List<PlayerMatchRecord> collectedRecords;

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
    // 1) 파일 상단의 아래 두 struct 완전히 삭제하세요.
    // public struct DefeatMessage : NetworkMessage { public string matchId; }
    // public struct VictoryMessage : NetworkMessage { public string matchId; }

    [Server]
    public void ServerNotifyPlayerDead(PlayerStats dead)
    {
        if (ended) return;

        // matchId를 기반으로 현재 매치 내 PlayerStats 전부 조회
        var allPlayers = NetworkServer.spawned.Values
            .Select(id => id.GetComponent<PlayerStats>())
            .Where(p => p != null && p.matchIdStr == dead.matchIdStr)
            .ToList();

        int aliveCount = allPlayers.Count(p => p.isAlive);
        Debug.Log($"[MatchManager] 플레이어 사망 감지: {dead.Nickname}, 남은 생존자: {aliveCount}");

        if (aliveCount <= 0)
        {
            Debug.Log("[MatchManager] 전원 사망 → 패배 처리");
            EndMatch(false);
        }
    }

    [Server]
    public void EndMatch(bool isVictory)
    {
        if (ended) return;
        ended = true;

        var guid = GetComponent<NetworkMatch>().matchId;
        float matchDuration = Time.time - matchStartTime;

        collectedRecords = new List<PlayerMatchRecord>();

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn?.identity == null) continue;
            var nm = conn.identity.GetComponent<NetworkMatch>();
            if (nm == null || nm.matchId != guid) continue;

            var stats = conn.identity.GetComponent<PlayerStats>();
            if (stats == null) continue;

            collectedRecords.Add(stats.GetRecord(matchDuration));
        }

        StartCoroutine(CoEndFlow(guid, isVictory, matchDuration));
    }


    [Server]
    private IEnumerator CoEndFlow(Guid matchGuid, bool isVictory, float matchDuration)
    {
        RpcShowMatchSummary(collectedRecords, isVictory);

        var firebase = FirebaseManagerServer.Instance;

        if (firebase != null && collectedRecords.Count > 0)
        {
            yield return firebase.CoUploadAndGetRankings(
                matchGuid.ToString(),
                collectedRecords,
                isVictory,
                matchDuration,
                this
            );
        }

        // 4) 모든 전송 끝난 뒤에만 종료 트리거
        FinalizeEnd(matchGuid.ToString(), isVictory);
    }

    [Server]
    public void ShowSummaryOnly(bool isVictory)
    {
        RpcShowMatchSummary(collectedRecords, isVictory);
    }

    [Server]
    public void FinalizeEnd(string matchId, bool isVictory)
    {
        Guid guid = Guid.Parse(matchId);
        OnMatchEnded?.Invoke(guid, isVictory);
    }

    [Server]
    public void OnRankingResult(string userId, int rank, float percentile)
    {
        if (TryGetPlayerConn(userId, out var conn))
        {
            TargetShowRanking(conn, rank, percentile);
        }
        else
        {
            Debug.LogWarning($"[MatchManager] OnRankingResult: userId={userId} conn 미발견");
        }
    }

    [Server]
    private bool TryGetPlayerConn(string userId, out NetworkConnectionToClient conn)
    {
        conn = null;

        foreach (var c in NetworkServer.connections.Values)
        {
            if (c?.identity == null) continue;
            var stats = c.identity.GetComponent<PlayerStats>();
            if (stats == null) continue;

            // UserId 매칭 기준은 프로젝트 규칙에 맞게 조정
            if (!string.IsNullOrEmpty(stats.UserId) && stats.UserId == userId)
            {
                conn = c;
                return true;
            }
        }
        return false;
    }

    [TargetRpc]
    private void TargetShowRanking(NetworkConnectionToClient connt, int rank, float percent)
    {
        // >>> 추가: 신기록자만 Top Score Panel 켜기
        var ui = UIManager.Instance;
        if (ui != null)
        {
            ui.ShowTopScoreboard();   // Result Canvas / Top Score Panel 켜짐
                                      // 필요하면 Rank/Percent 텍스트 세팅 함수도 여기서 호출
                                      // ui.SetRankText(rank > 0 ? $"랭킹 {rank}위" : "랭킹권 밖");
                                      // ui.SetTopPercentText(rank > 0 ? $"상위 {percent:0.0}%" : "-");
        }

        // (선택) 기존 이벤트 유지
        OnTopRankUpdated?.Invoke(rank, percent);
    }


    [ClientRpc]
    void RpcShowMatchSummary(List<PlayerMatchRecord> records, bool isVictory)
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        ui.ShowScoreboard();

        // --- 내 userId 가져오기 ---
        string myId = "";
        if (NetworkClient.localPlayer)
            myId = NetworkClient.localPlayer.GetComponent<PlayerStats>()?.UserId ?? "";
        if (string.IsNullOrEmpty(myId))
            myId = FirebaseManagerClient.Instance?.GetUserId() ?? "";

        // --- 내 레코드 찾기 (struct라 null 금지 → 인덱스 사용) ---
        int idx = -1;
        if (!string.IsNullOrEmpty(myId))
            idx = records.FindIndex(r => r.userId == myId);

        if (idx < 0 && NetworkClient.localPlayer)  // fallback: 닉네임으로
        {
            var myNick = NetworkClient.localPlayer.GetComponent<PlayerStats>()?.Nickname;
            if (!string.IsNullOrEmpty(myNick))
                idx = records.FindIndex(r => r.nickname == myNick);
        }

        // idx >= 0 이면 내 레코드가 존재
        if (idx >= 0)
        {
            var my = records[idx];
            // TODO: 당신 프로젝트 함수명에 맞게 채우기
            // ui.FillTeamInfo(records);
            // ui.FillMyInfo(my, isVictory);
        }
        else
        {
            // 못 찾은 경우에도 팀 정보는 채우고, 내 정보는 빈값/미표시 처리
            // ui.FillTeamInfo(records);
            // ui.FillMyInfoDefault(isVictory);
        }

        // (선택) 기존 이벤트 유지
        OnMatchSummaryReady?.Invoke(records, isVictory);
    }




    // 플레이어 등록/해제
    [Server] public void AddPlayer(Transform t) { if (!playerTransforms.Contains(t)) playerTransforms.Add(t); }
    [Server] public void RemovePlayer(Transform t) { playerTransforms.Remove(t); }

    [Server]
    public void StartMatchWithPlayers(List<Transform> initialPlayers)
    {
        ended = false;
        var guid = GetComponent<NetworkMatch>().matchId;
        TargetResetUIForAll(guid);

        // 타이머/단계/보스/카운터 등 전부 초기화
        playerTransforms.Clear();
        playerTransforms.AddRange(initialPlayers);
        matchStartTime = Time.time;
        elapsedMinutes = 0;
        lastAppliedMinute = -1;
        currentStageId = startStageId;
        isBossAlive = false;
        currentBoss = null;
        killsThisStage = 0;
        aliveEnemies.Clear();
        loadedClients.Clear();

        ApplyStageContent();
        UpdateDifficultyScaling();

        var stage = gameModeConfig.FindStage(currentStageId);
        spawner.ApplyStep(currentDifficultyStep, stage, gameModeConfig.spawnRule);

        if (stage != null)
            spawner.OverrideNextBurst(Time.time + stage.initialSpawnDelay);
    }

    [Server]
    void TargetResetUIForAll(Guid guid)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn?.identity == null) continue;
            var nm = conn.identity.GetComponent<NetworkMatch>();
            if (nm != null && nm.matchId == guid)
                TargetResetUI(conn);
        }
    }

    [TargetRpc]
    void TargetResetUI(NetworkConnectionToClient conn)
    {
        Debug.Log("[UI] TargetResetUI 수신");
        UIManager.Instance?.ResetAllUI();
        UIManager.Instance?.EnterGameplayHUD();
    }


    [TargetRpc]
    void TargetResetResultUI(NetworkConnectionToClient conn)
    {
        UIManager.Instance?.ResetAllUI();         // 패널/모달/바인딩 모두 해제
        UIManager.Instance?.EnterGameplayHUD();   // 게임 HUD 켜기
    }

    // 모든 해당 매치 참가자에게 초기화 쏘기
    [Server]
    private void ResetResultUIForAll(Guid guid)
    {
        foreach (var ni in NetworkServer.spawned.Values)
        {
            if (!ni) continue;
            var nm = ni.GetComponent<NetworkMatch>();
            if (nm == null || nm.matchId != guid) continue;

            var conn = ni.connectionToClient;
            if (conn != null) TargetResetResultUI(conn);
        }
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

            var tele = playerTransform.GetComponent<PlayerStats>();
            if (tele != null)
            {
                tele.TargetTeleport(conn, sp.position, sp.rotation);
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

        // 로컬플레이어에게서만 서버에 알림 보내기
        if (NetworkClient.localPlayer != null)
        {
            NetworkClient.localPlayer.GetComponent<PlayerStats>()?.CmdNotifyMapLoaded();
        }
    }

    [Server]
    public void OnClientMapLoaded(NetworkConnectionToClient conn)
    {
        loadedClients.Add(conn.connectionId);
        Debug.Log($"클라 {conn.connectionId} 맵 로드 완료 ({loadedClients.Count}/{playerTransforms.Count})");

        if (loadedClients.Count >= playerTransforms.Count)
        {
            TeleportPlayersToSpawnPoints();
            loadedClients.Clear();
        }
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

        // ★ 기존 보스 이벤트 해제 안전장치
        if (currentBoss != null)
        {
            var old = currentBoss.GetComponent<EnemyBase>();
            if (old) old.OnDied -= OnBossDied;
            currentBoss = null;
        }

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

        if (currentBoss != null)
        {
            var h = currentBoss.GetComponent<EnemyBase>();
            if (h) h.OnDied -= OnBossDied;
            currentBoss = null;
        }
        ClearAllEnemies();

        var stage = gameModeConfig.FindStage(currentStageId);
        if (stage == null) return;

        if (stage.nextStageId < 0)
        {
            EndMatch(true);
            return;
        }

        currentStageId = stage.nextStageId;
        killsThisStage = 0;

        ApplyStageContent();
        UpdateDifficultyScaling();

        var nextStage = gameModeConfig.FindStage(currentStageId);
        spawner.ApplyStep(currentDifficultyStep, nextStage, gameModeConfig.spawnRule);

        if (nextStage != null)
            spawner.OverrideNextBurst(Time.time + nextStage.initialSpawnDelay);
    }

    [Server]
    private void ClearAllEnemies()
    {
        foreach (var enemy in aliveEnemies.ToList())
        {
            if (enemy != null && enemy.gameObject != null)
            {
                NetworkServer.Destroy(enemy.gameObject);
            }
        }
        aliveEnemies.Clear();
    }
}
