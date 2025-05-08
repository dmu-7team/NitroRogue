using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.UI;

public class EnemyBase : CharacterStats
{
    [Header("공격 관련")]
    public AttackObjectBase[] attackObjs;
    private List<AttackBase> attacks = new List<AttackBase>();
    AttackBase currentAttack;

    [Header("애니메이션/AI")]
    Animator animator;
    BehaviorGraphAgent behavior;
    public bool isAttacking = false;

    [Header("체력바 UI")]
    public Image healthBarImage;

    [Header("드랍 박스")]
    public GameObject boxPrefab;

    private bool isDead = false; // 추가: 중복 죽음 방지

    Color[] colors = new Color[8]
    {
    Color.red,       // 빨강
    Color.yellow,    // 노랑 (주황 대체 없음)
    Color.green,     // 초록
    Color.cyan,      // 청록
    Color.blue,      // 파랑
    Color.magenta,   // 보라
    Color.gray,      // 회색 (중간톤 보조)
    Color.white      // 흰색 (밝기 강조 보조)
    };
    
    void Awake()
    {
        behavior = GetComponent<BehaviorGraphAgent>();
        animator = GetComponent<Animator>();

        foreach (var attackObj in attackObjs)
        {
            var attackInstance = attackObj.CreateAttackInstance();
            attackInstance.Initialize(gameObject);
            attackInstance.lastUsedTime = Time.time;
            attacks.Add(attackInstance);
        }
    }

    protected override void Start()
    {
        base.Start();
        UpdateHealthBar(); // 시작할 때 체력바 세팅
    }

    void OnDrawGizmosSelected()
    {
        for (int i=0; i<attackObjs.Length; i++)
        {
            Gizmos.color = colors[i];
            Gizmos.DrawWireSphere(transform.position, attackObjs[i].range);
        }
    }

    /// <summary>
    /// 쿨타임과 사정거리를 충족할 경우 타겟에게 공격을 시도함
    /// </summary>
    /// <param name="target"></param>
    public virtual void UseAllAttack(GameObject target)
    {
        for (int i = 0; i < attacks.Count; i++)
        {
            if (attacks[i].IsReadyToExecute(gameObject, target))
            {
                currentAttack = attacks[i];
                attacks[i].Execute(gameObject, target);

                isAttacking = true;

                animator.SetInteger("attackIndex", i);
                animator.SetTrigger("doAttack");
                return;
            }
        }
        isAttacking = false;
    }

    /// <summary>
    /// 모든 공격 쿨타임 초기화
    /// </summary>
    public void ForceCooldownStart()
    {
        foreach (var attack in attacks)
        {
            attack.lastUsedTime = Time.time;
        }
    }

    /// <summary>
    /// 공격 애니메이션 끝 이벤트
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        behavior?.SetVariableValue("IsAttacking", isAttacking);
        currentAttack?.ForceCooldownStart(); // ← 여기서 쿨타임 시작
    }

    /// <summary>
    /// 공격 애니메이션 이벤트
    /// </summary>
    public void OnAnimationEvent(string eventName)
    {
        if (currentAttack == null) return;
        currentAttack.OnAnimationEvent(eventName);
    }
    public override void TakeDamage(float amount, GameObject attacker)
    {
        base.TakeDamage(amount, attacker);
        UpdateHealthBar();

        if (CurrentHealth <= 0)
        {
            Die(); // 체력 0 이하일 때 사망 처리
        }
    }
    private void UpdateHealthBar()
    {
        if (healthBarImage != null)
        {
            healthBarImage.fillAmount = CurrentHealth / MaxHealth;
        }
    }

    protected override void Die()
    {
        if (isDead) return; // 이미 죽었으면 무시
        isDead = true;

        base.Die();

        behavior?.SetVariableValue("IsDead", true);
        behavior?.SetVariableValue("IsAttacking", true);

        Debug.Log(gameObject.name);
        DropBox();
        Destroy(gameObject);
    }

    private void DropBox()
    {
        if (boxPrefab != null)
        {
            Instantiate(boxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            Debug.Log("[EnemyBase] 박스 드랍 완료");
        }
        else
        {
            Debug.LogWarning("[EnemyBase] 드랍할 박스 프리팹이 없습니다.");
        }
    }
}
