using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.AI;

public class MonsterSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private Transform[] spawnPoints;

    public override void OnStartServer()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnEnemy();
        }
    }

    [Server]
    private void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[MonsterSpawner] enemyPrefab 또는 spawnPoints가 설정되지 않음");
            return;
        }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // NavMesh 위인지 확인
        if (NavMesh.SamplePosition(point.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);
            NetworkServer.Spawn(enemy);
            Debug.Log($"[MonsterSpawner] 몬스터 스폰 완료: {enemy.name}");
        }
        else
        {
            Debug.LogWarning("[MonsterSpawner] NavMesh 위에서 스폰 실패");
        }
    }
}
