using UnityEngine;
using UnityEngine.AI;
using Mirror;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 서버 전용 스포너 (토큰 시스템 없는 리듬 기반 최종 버전):
/// - 설정된 Cooldown 시간이 되면, 설정된 Burst Size 만큼의 몬스터를 즉시 스폰.
/// - 토큰/Spm 개념이 없어 매우 직관적임.
/// </summary>
public class BurstSpawner : NetworkBehaviour
{
    // GameModeConfigSO에 정의된 구조체와 동일해야 합니다.
    [System.Serializable]
    public struct StepParams
    {
        public int burstMin, burstMax, burstMode;
        public float cdMin, cdMax;
        public int maxAliveHardCap;
        public int elitePercent;
        public float hpMul, dmgMul, moveMul;
    }

    private StepParams step;
    private StageConfigSO stage;
    private SpawnRuleSO rule;

    private float nextBurstTime = 0f;
    private NetworkMatch networkMatch;

    // 자신의 MatchManager를 저장할 변수
    private MatchManager matchManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        networkMatch = GetComponent<NetworkMatch>();

        // 자신의 게임 오브젝트에 붙어있는 MatchManager를 찾아 저장합니다.
        matchManager = GetComponent<MatchManager>();
        if (matchManager == null)
        {
            Debug.LogError("BurstSpawner could not find MatchManager on the same GameObject!");
        }
    }

    [Server]
    public void ApplyStep(StepParams newStep, StageConfigSO newStage, SpawnRuleSO newRule)
    {
        step = newStep;
        stage = newStage;
        rule = newRule;
        ScheduleNextBurst(); // 새 스텝 적용 시 다음 버스트 예약
    }

    [Server]
    public void ServerTick(float dt, IReadOnlyList<Transform> players)
    {
        if (players == null || players.Count == 0) return;

        if (Time.time >= nextBurstTime)
            TryDoBurst(players);
    }

    [Server]
    private void ScheduleNextBurst()
    {
        // step이 아직 설정되지 않았을 수 있으므로 안전장치 추가
        if (step.cdMax <= 0) return;
        float cd = Random.Range(step.cdMin, step.cdMax);
        nextBurstTime = Time.time + cd;
    }

    [Server]
    private void TryDoBurst(IReadOnlyList<Transform> players)
    {
        if (players.Count == 0 || stage == null || rule == null) { ScheduleNextBurst(); return; }

        // 토큰 계산 없이, 바로 Burst Min/Max에 따라 스폰 수 결정
        int totalSpawnCount = GetBiasedRandomValue(step.burstMin, step.burstMode, step.burstMax);
        if (totalSpawnCount <= 0)
        {
            ScheduleNextBurst();
            return;
        }
        float jitterMax = rule.intraBurstMaxDelay;

        for (int i = 0; i < totalSpawnCount; i++)
        {
            Transform pivotPlayer = players[i % players.Count];

            if (TryPickSpawnPoint(players, pivotPlayer, out Vector3 spawnPos))
            {
                float delay = Random.Range(0f, jitterMax);
                StartCoroutine(SpawnOneAfterDelay(spawnPos, delay));
            }
        }

        ScheduleNextBurst();
    }

    private IEnumerator SpawnOneAfterDelay(Vector3 pos, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (matchManager == null || stage == null || stage.mapSpawnSet == null) yield break;

        bool elite = (Random.Range(0, 100) < step.elitePercent);
        GameObject prefab = elite
            ? stage.mapSpawnSet.PickByChance(stage.mapSpawnSet.eliteList)
            : stage.mapSpawnSet.PickByChance(stage.mapSpawnSet.normalList);

        if (prefab == null)
        {
            // [로그 추가] 스폰할 몬스터를 못 고르면 이 에러가 뜰 것입니다.
            Debug.LogError($"[BurstSpawner] Could not pick a monster prefab to spawn. Check MapSpawnSetSO '{stage.mapSpawnSet.name}' to see if monster lists are empty.");
            yield break;
        }

        var go = Instantiate(prefab, pos, Quaternion.identity);
        var netIdComponent = go.GetComponent<NetworkIdentity>();
        var networkMatchComponent = go.GetComponent<NetworkMatch>();

        if (networkMatchComponent != null && networkMatch != null)
        {
            networkMatchComponent.matchId = networkMatch.matchId;
        }

        NetworkServer.Spawn(go);

        if (!matchManager.TryRegisterEnemy(netIdComponent))
        {
            NetworkServer.Destroy(go);
            yield break;
        }

        // EnemyBase 컴포넌트가 있는지 확인하고 이벤트 연결
        var h = go.GetComponent<EnemyBase>();
        h.ApplyMultipliers(step.hpMul, step.dmgMul, step.moveMul);
        if (h)
        {
            h.OnDied += () =>
            {
                if (matchManager == null) return;
                matchManager.UnregisterEnemy(netIdComponent);
                matchManager.OnEnemyKilled();
            };
        }
    }

    private bool TryPickSpawnPoint(IReadOnlyList<Transform> allPlayers, Transform pivot, out Vector3 result)
    {
        result = default;
        if (rule == null) return false;

        for (int attempt = 0; attempt < rule.maxPositionAttempts; attempt++)
        {
            Vector3 ringPoint = RandomPointOnRing(pivot.position, rule.minDist, rule.maxDist);
            Vector3 finalPos;
            bool found = rule.useGroundRay
                ? FindSpawnPointWithRaycast(ringPoint, out finalPos)
                : FindSpawnPointOnNavMesh(ringPoint, pivot.position.y, out finalPos);

            if (found && IsValidSpawnPoint(finalPos, pivot, allPlayers))
            {
                result = finalPos;
                return true;
            }
        }
        return false;
    }

    private bool FindSpawnPointWithRaycast(Vector3 originXZ, out Vector3 result)
    {
        result = default;
        Vector3 top = originXZ + Vector3.up * rule.raycastHeight;
        if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, rule.raycastHeight * 2f)) return false;
        if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, rule.sampleMaxDistance, NavMesh.AllAreas)) return false;
        result = navHit.position;
        return true;
    }

    private bool FindSpawnPointOnNavMesh(Vector3 originXZ, float pivotY, out Vector3 result)
    {
        result = default;
        Vector3 probe = new Vector3(originXZ.x, pivotY, originXZ.z);
        float searchRadius = Mathf.Max(rule.sampleMaxDistance, rule.verticalSearch);
        if (!NavMesh.SamplePosition(probe, out NavMeshHit navHit, searchRadius, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(navHit.position.y - pivotY) > rule.maxVerticalDelta) return false;
        result = navHit.position;
        return true;
    }

    private bool IsValidSpawnPoint(Vector3 point, Transform pivot, IReadOnlyList<Transform> allPlayers)
    {
        Vector2 nXZ = new Vector2(point.x, point.z);
        Vector2 pivotXZ = new Vector2(pivot.position.x, pivot.position.z);
        float distToPivot = Vector2.Distance(pivotXZ, nXZ);
        if (distToPivot < rule.minDist || distToPivot > rule.maxDist) return false;
        foreach (var otherPlayer in allPlayers)
        {
            if (otherPlayer == pivot) continue;
            Vector2 otherXZ = new Vector2(otherPlayer.position.x, otherPlayer.position.z);
            if (Vector2.Distance(otherXZ, nXZ) < rule.minDist) return false;
        }
        return true;
    }

    private static Vector3 RandomPointOnRing(Vector3 center, float minR, float maxR)
    {
        float r = Random.Range(minR, maxR);
        float a = Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
    }

    private static int GetBiasedRandomValue(int min, int mode, int max)
    {
        if (min >= max) return min;
        int totalW = 0;
        for (int k = min; k <= max; k++)
        {
            int w = (k <= mode) ? (k - min + 1) : (max - k + 1);
            totalW += Mathf.Max(1, w);
        }
        if (totalW <= 0) return mode;
        int r = Random.Range(0, totalW);
        for (int k = min; k <= max; k++)
        {
            int w = (k <= mode) ? (k - min + 1) : (max - k + 1);
            w = Mathf.Max(1, w);
            if (r < w) return k;
            r -= w;
        }
        return mode;
    }

    [Server]
    public void OverrideNextBurst(float time)
    {
        nextBurstTime = time;
    }
}