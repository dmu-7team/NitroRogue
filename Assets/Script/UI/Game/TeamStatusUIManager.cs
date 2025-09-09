using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class TeamStatusUIManager : MonoBehaviour
{
    [SerializeField] RectTransform listRoot;
    [SerializeField] PlayerStatusRow rowPrefab;

    private readonly Dictionary<uint, PlayerStatusRow> rows = new(); // netId → Row

    void OnEnable()
    {
        PlayerStats.Spawned += OnSpawned;
        PlayerStats.Despawned += OnDespawned;

        // 레이트조인/씬 리로드: 이미 떠있는 것들 한 번 싹 반영
        foreach (var kv in NetworkClient.spawned)
            if (kv.Value && kv.Value.TryGetComponent(out PlayerStats p))
                TryAdd(p);
    }

    void OnDisable()
    {
        PlayerStats.Spawned -= OnSpawned;
        PlayerStats.Despawned -= OnDespawned;
    }

    void OnSpawned(PlayerStats p) => TryAdd(p);

    void OnDespawned(PlayerStats p)
    {
        if (rows.TryGetValue(p.netId, out var row))
        {
            Destroy(row.gameObject);
            rows.Remove(p.netId);
        }
    }

    void TryAdd(PlayerStats p)
    {
        if (rows.ContainsKey(p.netId)) return;

        var row = Instantiate(rowPrefab, listRoot);
        row.Bind(p);                   // 인스턴스 이벤트(Name/Hp 등) 구독
        rows[p.netId] = row;
    }
}
