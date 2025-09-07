using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class MagicBallHitbox : MonoBehaviour
{
    private float damage;
    private GameObject owner;
    private float duration;
    public float speed = 10f;
    private bool initialized = false;
    public bool rotate = false; // 발사체 자체를 회전시킬지 여부
    public float rotateAmount = 45; // 초당 회전 속도

    private Rigidbody rb; // Rigidbody 캐싱
    private GameObject target;
    public GameObject muzzlePrefab; // 발사 시 생성할 총구 이펙트
    public GameObject hitPrefab; // 충돌 시 생성할 이펙트
    public List<GameObject> trails; // 이동 중 따라다니는 궤적 이펙트들

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 발사 시 총구 이펙트 생성
        if (muzzlePrefab != null)
        {
            var muzzleVFX = Instantiate(muzzlePrefab, transform.position, Quaternion.identity);
            muzzleVFX.transform.forward = gameObject.transform.forward;

            // 파티클이 있으면 지속시간 후 삭제
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

    public void Initialize(float damage, float duration, GameObject owner, GameObject target = null)
    {
        this.damage = damage;
        this.owner = owner;
        this.target = target;
        initialized = true;
        Destroy(gameObject, duration); // 자동 삭제
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        // 타겟이 있으면 그 방향을 바라봄
        if (target)
        {
            // 목표 방향 벡터
            Vector3 direction = (target.transform.position - transform.position).normalized;

            // 현재 방향에서 목표 방향으로 점진적 회전
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
        // 앞으로 이동
        if (speed != 0 && rb != null)
            rb.position += (transform.forward) * (speed * Time.deltaTime);

        // 자전 회전
        if (rotate)
            transform.Rotate(0, 0, rotateAmount, Space.Self);
    }

    void OnCollisionEnter(Collision co)
    {
        if (!initialized) return;
        if (co.gameObject == owner) return;
        if (co.gameObject.CompareTag("Enemy")) return;
        if (co.gameObject.CompareTag("Player"))
        {
            co.gameObject.GetComponent<PlayerStats>()?.TakeDamage(damage);
        }

        // 궤적 파티클 분리 후 삭제
        if (trails != null && trails.Count > 0)
        {
            for (int i = 0; i < trails.Count; i++)
            {
                trails[i].transform.parent = null;
                var ps = trails[i].GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                    Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
                }
            }
        }

        // 속도 멈추고 물리 비활성화
        speed = 0;
        rb.isKinematic = true;

        // 충돌 지점에 히트 이펙트 생성
        ContactPoint contact = co.contacts[0];
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, contact.normal);
        Vector3 pos = contact.point;

        if (hitPrefab != null)
        {
            var hitVFX = Instantiate(hitPrefab, pos, rot) as GameObject;

            var ps = hitVFX.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                var psChild = hitVFX.transform.GetChild(0).GetComponent<ParticleSystem>();
                Destroy(hitVFX, psChild.main.duration);
            }
            else
                Destroy(hitVFX, ps.main.duration);
        }

        Destroy(gameObject);
    }
}
