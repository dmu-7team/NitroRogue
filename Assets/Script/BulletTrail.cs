using UnityEngine;
using Mirror;

public class BulletTrail : NetworkBehaviour
{
    [ServerCallback]
    private void Start()
    {
        Destroy(gameObject, 5f); // 5초 뒤 자동 제거
    }

    // 필요하면 위치 갱신/이펙트 코드 추가 가능
}
