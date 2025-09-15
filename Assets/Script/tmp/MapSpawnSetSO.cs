using UnityEngine;

[CreateAssetMenu(menuName = "Game/MapSpawnSet")]
public class MapSpawnSetSO : ScriptableObject
{
    [System.Serializable]
    public class ChanceEntry
    {
        public GameObject prefab;
        [Range(0, 100)] public int chance = 1; // 확률(상대값, 합이 100일 필요 없음)
    }

    [Header("일반 몬스터(확률로 고름)")]
    public ChanceEntry[] normalList;

    [Header("강한 몬스터(확률로 고름, 선택)")]
    public ChanceEntry[] eliteList;

    [Header("보스 정보")]
    public GameObject bossPrefab;
    public string bossSpawnPointTag = "BossSpawn";

    private static int Sum(ChanceEntry[] arr)
    {
        int s = 0; if (arr == null) return 0;
        foreach (var e in arr) if (e != null) s += Mathf.Max(0, e.chance);
        return s;
    }

    // 확률(상대값)로 하나 뽑기
    public GameObject PickByChance(ChanceEntry[] arr)
    {
        if (arr == null || arr.Length == 0) return null;
        int total = Sum(arr);
        if (total <= 0) return null;

        int r = Random.Range(0, total); // [0, total)
        foreach (var e in arr)
        {
            if (e == null || e.prefab == null) continue;
            int w = Mathf.Max(0, e.chance);
            if (r < w) return e.prefab;
            r -= w;
        }
        // 안전장치
        for (int i = arr.Length - 1; i >= 0; --i)
            if (arr[i]?.prefab) return arr[i].prefab;
        return null;
    }
}
