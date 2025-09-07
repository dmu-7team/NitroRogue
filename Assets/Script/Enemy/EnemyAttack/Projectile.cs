using Mirror;
using UnityEngine;

/// <summary>
/// 발사체를 특정 이름의 spawnPoint(Transform) 아래에 붙이기 위한 매니저입니다.
/// 서버에서 RpcSetParent()를 호출하면, 클라이언트에서 해당 이름을 가진 자식 Transform을 찾아
/// 해당 발사체를 자식으로 붙입니다.
/// </summary>
public class Projectile : NetworkBehaviour
{
    [SyncVar] public uint casterNetId;
    [SyncVar] public string spawnPointName;
    [SyncVar] public bool followSpawner;   // ← 추가

    public override void OnStartClient()
    {
        base.OnStartClient();

        // followSpawner가 false면 부모 붙이기 로직 자체를 건너뜁니다.
        if (!followSpawner) return;

        if (!NetworkClient.spawned.TryGetValue(casterNetId, out var casterIdentity))
        {
            Debug.LogWarning($"[Projectile] casterNetId {casterNetId} 못 찾음.");
            return;
        }

        // TransformExtensions.FindDeepChild 를 사용해 재귀 탐색
        var parentTransform = casterIdentity.transform.FindDeepChild(spawnPointName);
        if (parentTransform != null)
            transform.SetParent(parentTransform, worldPositionStays: true);
        else
            Debug.LogWarning($"[Projectile] '{spawnPointName}' Transform을 못 찾음.");
    }
}

// 깊이 탐색용 Extension
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}
