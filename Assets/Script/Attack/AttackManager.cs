using Mirror;
using UnityEngine;

/// <summary>
/// 발사체를 특정 이름의 spawnPoint(Transform) 아래에 붙이기 위한 매니저입니다.
/// 서버에서 RpcSetParent()를 호출하면, 클라이언트에서 해당 이름을 가진 자식 Transform을 찾아
/// 해당 발사체를 자식으로 붙입니다.
/// </summary>
public class AttackManager : NetworkBehaviour
{
    /// <summary>
    /// 서버 → 클라이언트 호출.
    /// spawnPointName에 해당하는 자식 Transform을 찾아,
    /// childNetId 오브젝트를 해당 Transform 아래로 붙입니다.
    /// </summary>
    /// <param name="childNetId">발사체의 NetworkIdentity.netId</param>
    /// <param name="spawnPointName">캐스터의 자식 중 부모가 될 Transform 이름</param>
    [ClientRpc]
    public void RpcSetParent(uint childNetId, string spawnPointName)
    {
        if (!NetworkClient.spawned.TryGetValue(childNetId, out NetworkIdentity childId))
        {
            Debug.LogWarning($"[AttackManager] netId {childNetId}에 해당하는 오브젝트를 찾을 수 없습니다.");
            return;
        }

        // 이 AttackManager가 붙은 오브젝트(보통 캐스터)의 하위에서 spawnPointName을 검색
        Transform[] children = transform.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t.name == spawnPointName)
            {
                childId.transform.SetParent(t, true);
                return;
            }
        }
    }
}
