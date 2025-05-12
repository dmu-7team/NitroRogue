using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using TMPro;
using Mirror;
using Unity.Behavior;

[RequireComponent(typeof(NetworkIdentity))]
public class EnemyBase : CharacterStats
{
    [Header("공격 관련")]
    public AttackObjectBase[] attackObjs;

    [Header("애니메이션/AI")]
    private Animator animator;
    private NetworkAnimator networkAnimator;
    private BehaviorGraphAgent behaviorAgent;
    public bool isAttacking = false;
    private NavMeshAgent agent;
    [Header("체력바 UI")]
    public Image healthBarImage;

    [Header("드랍 박스")]
    public GameObject boxPrefab;

    [Header("데미지 팝업")]
    public GameObject damagePopupPrefab;
    public Transform popupSpawnPoint;

    [Header("AI 타겟 변수")]
    [SerializeField] private BlackboardVariable<GameObject> targetVar;

    private GameObject currentTarget;

    public override void Start()
    {
        base.Start();

        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        behaviorAgent = GetComponent<BehaviorGraphAgent>();

        if (NetworkServer.active && behaviorAgent != null && targetVar != null)
        {
            var players = GameObject.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            GameObject nearestPlayer = null;
            float minDist = float.MaxValue;

            foreach (var p in players)
            {
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPlayer = p.gameObject;
                }
            }

            if (nearestPlayer != null)
            {
                targetVar.Value = nearestPlayer;
                Debug.Log($"[EnemyBase] Target 설정 완료: {nearestPlayer.name}");
            }
            else
            {
                Debug.LogWarning("[EnemyBase] 근처에 플레이어를 찾지 못했습니다.");
            }
        }
        else
        {
            if (!NetworkServer.active)
                Debug.LogWarning("[EnemyBase] 서버가 아님. Target 설정 생략.");
            if (targetVar == null)
                Debug.LogWarning("[EnemyBase] targetVar가 연결되지 않았습니다.");
        }

        // 디버깅용: Animator 파라미터 확인
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            Debug.Log($"[Animator] 파라미터 이름: {param.name}, 타입: {param.type}");
        }
        agent = GetComponent<NavMeshAgent>();
        if (!isServer)
        {
            agent.enabled = false; // 클라이언트에서는 NavMeshAgent 끔
        }

    }
    void Update()
    {
        if (!isServer) return;

        if (currentTarget == null)
        {
            Debug.DrawRay(transform.position, Vector3.up * 2, Color.gray);
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        Debug.DrawLine(transform.position, currentTarget.transform.position, Color.red);

        Debug.Log($"[EnemyBase] 거리: {dist}, OnNavMesh={agent.isOnNavMesh}, isStopped={agent.isStopped}");

        if (!isAttacking && dist > 3f)
        {
            agent.isStopped = false;
            agent.SetDestination(currentTarget.transform.position);
            animator.SetFloat("speed", 1);
        }
        else
        {
            agent.isStopped = true;
            animator.SetFloat("speed", 0);
        }
    }

    public override void TakeDamage(float damage, GameObject attacker = null)
    {
        base.TakeDamage(damage, attacker);
        ShowDamagePopup(damage);
    }

    private void ShowDamagePopup(float damage)
    {
        if (damagePopupPrefab != null && popupSpawnPoint != null)
        {
            GameObject popup = Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity);
            var text = popup.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = damage.ToString("F0");
            }
        }
    }

    protected override void Die()
    {
        base.Die();
        DropItem();

        if (NetworkServer.active)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject, 1f);
        }
    }

    private void DropItem()
    {
        if (boxPrefab != null)
        {
            Instantiate(boxPrefab, transform.position, Quaternion.identity);
        }
    }

    public void UseAllAttack(GameObject target)
    {
        if (!NetworkServer.active) return;

        currentTarget = target;
        isAttacking = true;

        // Animator 상태가 Move일 때만 공격 조건을 걸어야 하므로,
        // 일단 Move 상태로 유도 (SetFloat("speed", 1) 또는 Trigger 활용)
        animator.Play("Move");

        // 공격 인덱스 지정 (1~8)
        int attackIndex = Random.Range(1, 9);
        animator.SetInteger("attackInd", attackIndex);
        animator.SetBool("doAttack", true);

        Debug.Log($"[EnemyBase] 공격 설정 → attackInd: {attackIndex}");
    }


    public void OnAttackAnimationEnd()
    {
        Debug.Log("[EnemyBase] 공격 애니메이션 종료 → 상태 초기화");
        isAttacking = false;
        animator.SetBool("doAttack", false);
    }


    public void OnAnimationEvent()
    {
        Debug.Log("[EnemyBase] OnAnimationEvent 실행됨");

        if (currentTarget == null)
        {
            Debug.LogWarning("[EnemyBase] currentTarget이 null임");
            return;
        }

        var stats = currentTarget.GetComponent<CharacterStats>();
        if (stats == null)
        {
            Debug.LogWarning("[EnemyBase] currentTarget에 CharacterStats 없음");
            return;
        }

        stats.TakeDamage(attackObjs[0].damage, gameObject);
        Debug.Log($"→ {currentTarget.name}에게 {attackObjs[0].damage} 데미지!");
    }


    private void OnDestroy()
    {
        // 정리 필요 시 처리
    }
}

// Animator에 해당 파라미터가 존재하는지 안전하게 확인하는 확장 메서드
public static class AnimatorExtensions
{
    public static bool HasParameter(this Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
}

