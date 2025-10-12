using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class ClusterBomb : NetworkBehaviour
{
    [Header("어미 폭탄 설정")]
    [SerializeField] private float fuseTime = 2.0f;           // 발사 후 폭발까지 걸리는 시간
    [SerializeField] private GameObject explosionEffectPrefab; // 폭발 시 생성될 이펙트 프리팹

    // 이 값들은 SkillController가 SkillConfig로부터 읽어서 채워줄 예정입니다.
    [Header("자탄 정보 (외부에서 주입)")]
    public GameObject childPrefab; // 생성할 자탄 프리팹
    public int childCount;         // 생성할 자탄 개수
    public float childForce;       // 자탄이 흩어지는 힘

    [SyncVar]
    public GameObject owner; // 스킬을 시전한 플레이어

    // 서버에서만 실행됩니다.
    public override void OnStartServer()
    {
        // fuseTime 이후에 ExplodeAndSpawnChildren 함수를 서버에서 실행하도록 예약합니다.
        Invoke(nameof(ExplodeAndSpawnChildren), fuseTime);
    }

    [Server] // 이 메서드는 서버 권한으로만 실행되어야 합니다.
    private void ExplodeAndSpawnChildren()
    {
        // 1. 어미 폭탄의 폭발 이펙트를 생성하고 모든 클라이언트에 동기화합니다.
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(effect);
        }

        // 2. 자탄 프리팹이 제대로 연결되었는지 확인 후, 자탄을 생성합니다.
        if (childPrefab != null && childCount > 0)
        {
            Debug.Log($"[서버] {childCount}개의 자탄을 생성합니다.");
            for (int i = 0; i < childCount; i++)
            {
                // 자탄을 어미 폭탄의 위치에 생성합니다.
                GameObject child = Instantiate(childPrefab, transform.position, Quaternion.identity);

                // 자탄이 사방으로 퍼져나가도록 랜덤한 방향으로 힘을 가합니다.
                Vector3 randomDirection = Random.onUnitSphere; // 3D 공간의 랜덤한 방향 벡터를 가져옵니다.
                randomDirection.y = Mathf.Abs(randomDirection.y); // 땅속으로 파고들지 않게 위쪽으로 솟구치도록 y값을 양수로 만듭니다.

                child.GetComponent<Rigidbody>().AddForce(randomDirection * childForce, ForceMode.Impulse);

                // 자탄의 주인(owner) 정보를 설정해줍니다. (누가 쏜 스킬인지 기록)
                var submunition = child.GetComponent<Submunition>();
                if (submunition != null)
                {
                    submunition.owner = this.owner;
                }

                // 생성된 자탄을 모든 클라이언트에게 동기화시킵니다.
                NetworkServer.Spawn(child);
            }
        }

        // 3. 모든 역할을 마친 어미 폭탄 자신을 서버에서 파괴합니다.
        NetworkServer.Destroy(gameObject);
    }
}