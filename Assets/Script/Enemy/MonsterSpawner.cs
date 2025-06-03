using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.AI;
using System;

public class MonsterSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private Transform[] spawnPoints;
    [HideInInspector] public string matchId;
    public override void OnStartServer()
    {
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
        if (enemyPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[MonsterSpawner] enemyPrefab 또는 spawnPoints가 설정되지 않음");
            return;
        }

        Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        if (NavMesh.SamplePosition(point.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

            // matchId 설정
            if (enemy.TryGetComponent(out NetworkMatch match))
            {
                match.matchId = Guid.Parse(matchId); // string → Guid
            }
            else
            {
                Debug.LogWarning("[MonsterSpawner] NetworkMatch가 enemy에 없음");
            }

            NetworkServer.Spawn(enemy);
            Debug.Log($"[MonsterSpawner] 몬스터 스폰 완료: {enemy.name}");
        }
        else
        {
            Debug.LogWarning("[MonsterSpawner] NavMesh 위에서 스폰 실패");
        }
    }
}
