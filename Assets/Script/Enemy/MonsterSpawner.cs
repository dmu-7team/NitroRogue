using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.AI;
using System;

public class MonsterSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnInterval = 10f;

    [SerializeField] private MonsterSpawnPointData spawnData;
    private Vector3[] spawnPositions;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (spawnData == null || spawnData.points == null || spawnData.points.Length == 0)
        {
            Debug.LogWarning("[MonsterSpawner] SpawnPointData가 설정되지 않았거나 비어있습니다.");
            return;
        }
        // SO에서 가져온 좌표 배열 복사
        spawnPositions = spawnData.points;

        // 스폰 루프 시작
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    [Server]
    private void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnPositions == null || spawnPositions.Length == 0)
        {
            Debug.LogWarning("[MonsterSpawner] enemyPrefab 또는 spawnPositions가 설정되지 않음");
            return;
        }

        Vector3 rawPos = spawnPositions[UnityEngine.Random.Range(0, spawnPositions.Length)];

        // NavMesh 위로 보정
        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

            var enemyMatch = enemy.GetComponent<NetworkMatch>();
            var spawnerMatch = GetComponent<NetworkMatch>();
            enemyMatch.matchId = spawnerMatch.matchId;

            NetworkServer.Spawn(enemy);
            Debug.Log($"[MonsterSpawner] 몬스터 스폰: {enemy.name} at {hit.position}");
        }
        else
        {
            Debug.LogWarning("[MonsterSpawner] NavMesh 위에서 스폰 실패");
        }
    }
}
