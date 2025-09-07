using Mirror;
using UnityEngine.AI;
using UnityEngine;

public class MonsterSpawner : NetworkBehaviour
{
    [Header("일반 몬스터")]
    [SerializeField] private GameObject[] enemyPrefabs;
    private int currentEnemyIndex = 0;
    private GameObject currentEnemy;

    [Header("스폰 위치 데이터")]
    [SerializeField] private MonsterSpawnPointData spawnData;
    private Vector3[] spawnPositions;

    [Header("스페셜 몬스터")]
    [SerializeField] private int killThreshold = 20;
    [SerializeField] private GameObject specialEnemyPrefab;
    private int killCount = 0;
    private bool specialSpawned = false;

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (spawnData == null || spawnData.points == null || spawnData.points.Length == 0)
        {
            Debug.LogWarning("[MonsterSpawner] SpawnPointData가 비어 있음");
            return;
        }

        spawnPositions = spawnData.points;
        SpawnNextEnemy();  // 첫 마리만 스폰
    }

    [Server]
    private void SpawnNextEnemy()
    {
        if (currentEnemy != null)
        {
            Debug.Log("[MonsterSpawner] 현재 몬스터가 아직 죽지 않음");
            return;
        }

        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        GameObject prefabToSpawn = enemyPrefabs[currentEnemyIndex];
        currentEnemyIndex = (currentEnemyIndex + 1) % enemyPrefabs.Length;

        Vector3 rawPos = spawnPositions[Random.Range(0, spawnPositions.Length)];

        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            currentEnemy = Instantiate(prefabToSpawn, hit.position, Quaternion.identity);

            var match = GetComponent<NetworkMatch>();
            var enemyMatch = currentEnemy.GetComponent<NetworkMatch>();
            if (enemyMatch != null && match != null)
                enemyMatch.matchId = match.matchId;

            if (currentEnemy.TryGetComponent(out EnemyBase enemyBase))
            {
                enemyBase.spawner = this;
            }

            NetworkServer.Spawn(currentEnemy);
            Debug.Log($"[MonsterSpawner] 몬스터 스폰: {currentEnemy.name}");
        }
    }

    [Server]
    public void OnMonsterKilled()
    {
        killCount++;
        Debug.Log($"[MonsterSpawner] 몬스터 처치 {killCount}");

        currentEnemy = null;  // 현재 몬스터 참조 제거

        if (!specialSpawned && killCount >= killThreshold)
        {
            specialSpawned = true;
            SpawnSpecialEnemy();
        }
        else
        {
            SpawnNextEnemy();  // 다음 몬스터 소환
        }
    }

    [Server]
    private void SpawnSpecialEnemy()
    {
        if (specialEnemyPrefab == null || spawnPositions.Length == 0) return;

        Vector3 rawPos = spawnPositions[Random.Range(0, spawnPositions.Length)];

        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            GameObject specialEnemy = Instantiate(specialEnemyPrefab, hit.position, Quaternion.identity);

            var match = GetComponent<NetworkMatch>();
            var enemyMatch = specialEnemy.GetComponent<NetworkMatch>();
            if (enemyMatch != null && match != null)
                enemyMatch.matchId = match.matchId;

            NetworkServer.Spawn(specialEnemy);
            Debug.Log("[MonsterSpawner] 특수 몬스터 소환 완료!");
        }
    }
}
