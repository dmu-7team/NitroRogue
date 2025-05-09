using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Unity.Behavior;
using TMPro;
using System.Collections.Generic;

public class EnemyBase : CharacterStats
{
    [Header("���� ����")]
    public AttackObjectBase[] attackObjs;
    private List<AttackBase> attacks = new List<AttackBase>();
    private AttackBase currentAttack;

    [Header("�ִϸ��̼�/AI")]
    private Animator animator;
    private BehaviorGraphAgent behavior;
    public bool isAttacking = false;

    [Header("ü�¹� UI")]
    public Image healthBarImage;

    [Header("��� �ڽ�")]
    public GameObject boxPrefab;

    [Header("������ �˾�")]
    public GameObject damagePopupPrefab;
    public Transform popupSpawnPoint;
    public Camera worldCamera; //  ���� �����ϴ� ī�޶�


    private bool isDead = false;

    protected override void Start()
    {
        base.Start();

        behavior = GetComponent<BehaviorGraphAgent>();
        animator = GetComponent<Animator>();

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        foreach (var attackObj in attackObjs)
        {
            var attackInstance = attackObj.CreateAttackInstance();
            attackInstance.Initialize(gameObject);
            attackInstance.lastUsedTime = Time.time;
            attacks.Add(attackInstance);
        }

        UpdateHealthBar();
    }

    public virtual void UseAllAttack(GameObject target)
    {
        for (int i = 0; i < attacks.Count; i++)
        {
            if (attacks[i].IsCooldownReady() && attacks[i].IsInRange(gameObject, target))
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

    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        if (behavior != null)
            behavior.SetVariableValue("IsAttacking", isAttacking);

        currentAttack?.ForceCooldownStart();
    }

    public void OnAnimationEvent(string eventName)
    {
        if (currentAttack == null) return;
        currentAttack.OnAnimationEvent(eventName);
    }

    public override void TakeDamage(float amount, GameObject attacker)
    {
        base.TakeDamage(amount, attacker);
        UpdateHealthBar();
        ShowDamagePopup((int)amount); //  �˾� ȣ��

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarImage != null)
        {
            healthBarImage.fillAmount = CurrentHealth / MaxHealth;
        }
    }

    private void ShowDamagePopup(int amount)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[EnemyBase] DamagePopup �������� ������� �ʾҽ��ϴ�.");
            return;
        }

        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
        Debug.Log($"[DamagePopup] Showing damage: {amount}");

        // �ؽ�Ʈ ����
        TMP_Text text = popup.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = amount.ToString();
        }

        // ī�޶� �������� ���̰�
        if (worldCamera != null)
        {
            popup.transform.LookAt(worldCamera.transform);
            popup.transform.Rotate(0, 180f, 0);
        }
        else
        {
            Debug.LogWarning("[DamagePopup] worldCamera�� ����ֽ��ϴ�.");
        }

        // ������ �� ����
        popup.transform.localScale = Vector3.one * 0.01f;
        Destroy(popup, 1.0f);

    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        base.Die();

        if (behavior != null)
            behavior.SetVariableValue("IsDead", true);

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
