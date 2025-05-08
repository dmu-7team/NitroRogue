using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.UI;

public class EnemyBase : CharacterStats
{
    [Header("���� ����")]
    public AttackObjectBase[] attackObjs;
    private List<AttackBase> attacks = new List<AttackBase>();
    AttackBase currentAttack;

    [Header("�ִϸ��̼�/AI")]
    Animator animator;
    BehaviorGraphAgent behavior;
    public bool isAttacking = false;

    [Header("ü�¹� UI")]
    public Image healthBarImage;

    [Header("��� �ڽ�")]
    public GameObject boxPrefab;

    private bool isDead = false; // �߰�: �ߺ� ���� ����

    Color[] colors = new Color[8]
    {
    Color.red,       // ����
    Color.yellow,    // ��� (��Ȳ ��ü ����)
    Color.green,     // �ʷ�
    Color.cyan,      // û��
    Color.blue,      // �Ķ�
    Color.magenta,   // ����
    Color.gray,      // ȸ�� (�߰��� ����)
    Color.white      // ��� (��� ���� ����)
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
        UpdateHealthBar(); // ������ �� ü�¹� ����
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
    /// ��Ÿ�Ӱ� �����Ÿ��� ������ ��� Ÿ�ٿ��� ������ �õ���
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
    /// ��� ���� ��Ÿ�� �ʱ�ȭ
    /// </summary>
    public void ForceCooldownStart()
    {
        foreach (var attack in attacks)
        {
            attack.lastUsedTime = Time.time;
        }
    }

    /// <summary>
    /// ���� �ִϸ��̼� �� �̺�Ʈ
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        behavior?.SetVariableValue("IsAttacking", isAttacking);
        currentAttack?.ForceCooldownStart(); // �� ���⼭ ��Ÿ�� ����
    }

    /// <summary>
    /// ���� �ִϸ��̼� �̺�Ʈ
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
            Die(); // ü�� 0 ������ �� ��� ó��
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
        if (isDead) return; // �̹� �׾����� ����
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
            Debug.Log("[EnemyBase] �ڽ� ��� �Ϸ�");
        }
        else
        {
            Debug.LogWarning("[EnemyBase] ����� �ڽ� �������� �����ϴ�.");
        }
    }
}
