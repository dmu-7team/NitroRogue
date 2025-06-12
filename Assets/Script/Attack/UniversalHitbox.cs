using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 단발/지속 피해, 전진/정지/유도, 파괴 여부를 모두 커버하는 범용 히트박스
/// </summary>
public class UniversalHitbox : NetworkBehaviour
{
    // 데미지, 지속 시간, 틱 간격
    private float damage;
    private float duration;
    [SerializeField] private float tickInterval;

    // 이동 관련
    [SerializeField] private float speed;
    [SerializeField] private bool isHoming;      // 타겟 추적 여부
    [SerializeField] private bool isStatic;      // 이동 안하는 히트박스 여부

    // 히트박스 상태 관리
    [SerializeField] private bool autoDestroy = true;   // duration 후 자동 파괴 여부
    private bool initialized = false;  // 초기화 완료 여부

    // 참조
    private GameObject owner;          // 공격자
    private GameObject target;         // 추적 대상

    // 연출용 요소들
    [SerializeField] private GameObject muzzlePrefab;    // 생성 시 출력 이펙트
    [SerializeField] private GameObject hitPrefab;       // 충돌 시 이펙트
    [SerializeField] private List<GameObject> trails;    // 궤적 파티클
    private Rigidbody rb;

    // 지속 피해용: 타겟별 마지막 피해 시간 기록
    private Dictionary<GameObject, float> tickTimes = new();

    // 자전 여부
    [SerializeField] private bool rotate = false;
    [SerializeField] private float rotateAmount = 45f;

    private NetworkMatch ownerMatch;

    public override void OnStartServer()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogError($"UniversalHitbox(OnStartServer): Rigidbody가 없습니다! ({gameObject.name})");
        // Collider는 반드시 isTrigger = true로 설정되어 있어야 함
    }
    public override void OnStartClient()
    {
        if (muzzlePrefab == null) return;

        var muzzleVFX = Instantiate(muzzlePrefab, transform.position, transform.rotation);
        muzzleVFX.transform.forward = transform.forward;

        var ps = muzzleVFX.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            Destroy(muzzleVFX, ps.main.duration);
        }
        else
        {
            Debug.LogWarning("UniversalHitbox: muzzlePrefab에 ParticleSystem 컴포넌트가 없습니다.");
            Destroy(muzzleVFX, 2f);
        }
    }


    /// <summary>
    /// 범용 히트박스 초기화
    /// </summary>
    [Server]
    public void Initialize(float dmg, float dur, GameObject ownerObj, GameObject tgt = null)
    {
        if (IsInvoking(nameof(Cleanup)))
            CancelInvoke(nameof(Cleanup));
        damage = dmg;
        duration = dur;
        owner = ownerObj;
        target = tgt;
        initialized = true;
        ownerMatch = ownerObj.GetComponent<NetworkMatch>();
        tickTimes.Clear();

        if (autoDestroy)
            Invoke(nameof(Cleanup), duration);
    }

    private void FixedUpdate()
    {
        if (!initialized || isStatic) return;
        if (rb == null) return;

        // 이동 처리
        Vector3 moveDir = isHoming && target != null
            ? (target.transform.position - transform.position).normalized
            : transform.forward;

        rb.MovePosition(rb.position + moveDir * speed * Time.deltaTime);
        transform.forward = moveDir;

        // 자전 효과
        if (rotate)
            transform.Rotate(0, 0, rotateAmount * Time.deltaTime, Space.Self);
    }
    private bool ShouldIgnoreCollision(Collider other)
    {
        if (!isServer || !initialized) return true;
        if (other.gameObject == owner) return true;
        if (!other.CompareTag("Player")) return true;

        var otherMatch = other.GetComponent<NetworkMatch>();
        if (ownerMatch == null || otherMatch == null) return true;
        if (ownerMatch.matchId != otherMatch.matchId) return true;
        return false;
    }

    /// <summary>
    /// 지속 피해용 OnTriggerStay 처리
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (ShouldIgnoreCollision(other)) return;

        // 틱 간격마다 피해 적용
        if (tickInterval > 0f)
        {
            if (!tickTimes.ContainsKey(other.gameObject))
                tickTimes[other.gameObject] = 0f;

            if (Time.time >= tickTimes[other.gameObject])
            {
                var playerStats = other.GetComponent<PlayerStats>();
                if (playerStats != null)
                    playerStats.TakeDamage(damage);
                else
                    Debug.LogWarning($"UniversalHitbox: {other.gameObject.name}에 PlayerStats 컴포넌트가 없습니다.");

                tickTimes[other.gameObject] = Time.time + tickInterval;
            }
        }
    }

    /// <summary>
    /// 지속 피해 대상 범위 이탈 시 tickTimes에서 제거
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!isServer) return;
        if (tickTimes.ContainsKey(other.gameObject))
            tickTimes.Remove(other.gameObject);
    }

    /// <summary>
    /// 단발 피해용 OnTriggerEnter 처리
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (ShouldIgnoreCollision(other)) return;

        // 단발 피해일 경우 즉시 데미지 및 종료 처리
        if (tickInterval <= 0f)
        {
            var playerStats = other.GetComponent<PlayerStats>();
            if (playerStats != null)
                playerStats.TakeDamage(damage);
            else
                Debug.LogWarning($"UniversalHitbox: {other.gameObject.name}에 PlayerStats 컴포넌트가 없습니다.");

            // 충돌 이펙트 RPC 호출 (서버→클라이언트)
            RpcPlayHitEffect();
            Cleanup();
        }
    }

    /// <summary>
    /// 충돌 이펙트 및 궤적 정리
    /// </summary>
    [ClientRpc]
    private void RpcPlayHitEffect()
    {
        // 충돌 이펙트 출력
        if (hitPrefab != null)
        {
            var hitVFX = Instantiate(hitPrefab, transform.position, transform.rotation);
            var ps = hitVFX.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
                Destroy(hitVFX, ps.main.duration);
            else
            {
                Destroy(hitVFX, 1f);
            }
        }

        // 궤적 이펙트 정리
        if (trails != null && trails.Count > 0)
        {
            foreach (var trail in trails)
            {
                if (trail == null) continue;
                trail.transform.parent = null;
                var ps = trail.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                    Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
                } else
                {
                    Destroy(trail, 1f);
                }
            }
        }
    }

    /// <summary>
    /// 히트박스 정리 (삭제 or 비활성화) - 서버 전용
    /// </summary>
    [Server]
    private void Cleanup()
    {
        if (autoDestroy)
        {
            if (rb != null) rb.isKinematic = true;
            speed = 0f;
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            initialized = false;
            if (IsInvoking(nameof(Cleanup)))
                CancelInvoke(nameof(Cleanup));

            speed = 0f;
            if (rb != null) rb.isKinematic = true;
            tickTimes.Clear();

            gameObject.SetActive(false);
            RpcDeactivate();
        }
    }

    [ClientRpc]
    private void RpcDeactivate()
    {
        if (autoDestroy) return;
        gameObject.SetActive(false);
    }
}
