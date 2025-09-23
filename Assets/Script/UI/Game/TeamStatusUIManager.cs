using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class TeamStatusUIManager : MonoBehaviour
{
    [SerializeField] RectTransform listRoot;
    [SerializeField] PlayerStatusRow rowPrefab;

    private readonly Dictionary<uint, PlayerStatusRow> rows = new(); // netId → Row

    // ★ 외부에서 비울 수 있게 싱글톤(선택)
    public static TeamStatusUIManager Instance { get; private set; }

    void Awake() { Instance = this; }

    void OnEnable()
    {
        PlayerStats.Spawned += OnSpawned;
        PlayerStats.Despawned += OnDespawned;

        // 레이트조인/씬 리로드: 이미 떠있는 것들 싹 반영
        foreach (var kv in NetworkClient.spawned)
            if (kv.Value && kv.Value.TryGetComponent(out PlayerStats p))
                TryAdd(p);
    }

    void OnDisable()
    {
        PlayerStats.Spawned -= OnSpawned;
        PlayerStats.Despawned -= OnDespawned;

        // ★ 씬 전환/매치 종료 시 혹시 살아있어도 모두 날림
        ClearAll();
    }

    void OnSpawned(PlayerStats p) => TryAdd(p);

    void OnDespawned(PlayerStats p)
    {
        if (p == null) return;
        if (rows.TryGetValue(p.netId, out var row))
        {
            if (row) Destroy(row.gameObject);
            rows.Remove(p.netId);
        }
    }

    void TryAdd(PlayerStats p)
    {
        if (p == null) return;
        if (rows.ContainsKey(p.netId)) return;

        var row = Instantiate(rowPrefab, listRoot);
        row.Bind(p);                   // 인스턴스 이벤트(Name/Hp 등) 구독
        rows[p.netId] = row;
    }

    // ★★★ 핵심: 전판 잔재 싹 제거
    public void ClearAll()
    {
        foreach (var kv in rows)
            if (kv.Value) Destroy(kv.Value.gameObject);
        rows.Clear();
    }
}
