using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단발/지속 피해, 전진/정지/유도, 파괴 여부를 모두 커버하는 범용 히트박스
/// </summary>
public class UniversalHitbox : MonoBehaviour
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

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 생성 시 총구 이펙트 출력
        if (muzzlePrefab != null)
        {
            var muzzleVFX = Instantiate(muzzlePrefab, transform.position, Quaternion.identity);
            muzzleVFX.transform.forward = transform.forward;

            var ps = muzzleVFX.GetComponent<ParticleSystem>();
            if (ps != null)
                Destroy(muzzleVFX, ps.main.duration);
            else
            {
                var psChild = muzzleVFX.transform.GetChild(0).GetComponent<ParticleSystem>();
                Destroy(muzzleVFX, psChild.main.duration);
            }
        }
    }

    /// <summary>
    /// 범용 히트박스 초기화
    /// </summary>
    public void Initialize(float dmg, float dur, GameObject ownerObj, GameObject tgt = null)
    {
        damage = dmg;
        duration = dur;
        owner = ownerObj;
        target = tgt;
        initialized = true;
        
        if (autoDestroy)
            Destroy(gameObject, duration);
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        // 이동 처리
        if (!isStatic && rb != null)
        {
            Vector3 moveDir = isHoming && target != null
                ? (target.transform.position - transform.position).normalized
                : transform.forward;

            rb.MovePosition(rb.position + moveDir * speed * Time.deltaTime);
            transform.forward = moveDir;
        }

        // 자전 효과
        if (rotate)
            transform.Rotate(0, 0, rotateAmount * Time.deltaTime, Space.Self);
    }

    /// <summary>
    /// 지속 피해용 OnTriggerStay 처리
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!initialized || other.gameObject == owner) return;
        if (!other.CompareTag("Player")) return;

        // 틱 간격마다 피해 적용
        if (tickInterval > 0f)
        {
            if (!tickTimes.ContainsKey(other.gameObject))
                tickTimes[other.gameObject] = 0f;

            if (Time.time >= tickTimes[other.gameObject])
            {
                Debug.Log("지속공격");
                other.GetComponent<PlayerStats>()?.TakeDamage(damage);
                tickTimes[other.gameObject] = Time.time + tickInterval;
            }
        }
    }

    /// <summary>
    /// 단발 피해용 OnTriggerEnter 처리
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || other.gameObject == owner) return;
        if (!other.CompareTag("Player")) return;

        // 단발 피해일 경우 즉시 데미지 및 종료 처리
        if (tickInterval <= 0f)
        {
            Debug.Log("단일공격");
            other.GetComponent<PlayerStats>()?.TakeDamage(damage);
            DoHitEffect();
            Cleanup();
        }
    }

    /// <summary>
    /// 충돌 이펙트 및 궤적 정리
    /// </summary>
    private void DoHitEffect()
    {
        // 충돌 이펙트 출력
        if (hitPrefab != null)
        {
            var hitVFX = Instantiate(hitPrefab, transform.position, transform.rotation);
            var ps = hitVFX.GetComponent<ParticleSystem>();
            if (ps != null)
                Destroy(hitVFX, ps.main.duration);
            else
            {
                var psChild = hitVFX.transform.GetChild(0).GetComponent<ParticleSystem>();
                Destroy(hitVFX, psChild.main.duration);
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
                }
            }
        }
    }

    /// <summary>
    /// 히트박스 정리 (삭제 or 비활성화)
    /// </summary>
    private void Cleanup()
    {
        if (autoDestroy) {
            if (rb != null) rb.isKinematic = true;
            speed = 0;
            Destroy(gameObject);
        }
        else
            gameObject.SetActive(false); // 재사용 구조 대응
    }
}
