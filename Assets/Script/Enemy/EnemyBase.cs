using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Unity.Behavior;
using TMPro;
using System.Collections.Generic;

public class EnemyBase : CharacterStats
{
    [Header("공격 관련")]
    public AttackObjectBase[] attackObjs;
    private List<AttackBase> attacks = new List<AttackBase>();
    private AttackBase currentAttack;

    [Header("애니메이션/AI")]
    private Animator animator;
    private BehaviorGraphAgent behavior;
    public bool isAttacking = false;

    [Header("체력바 UI")]
    public Image healthBarImage;

    [Header("드랍 박스")]
    public GameObject boxPrefab;

    [Header("데미지 팝업")]
    public GameObject damagePopupPrefab;
    public Transform popupSpawnPoint;
    public Camera worldCamera; //  직접 연결하는 카메라


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
        ShowDamagePopup((int)amount); //  팝업 호출

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
            Debug.LogWarning("[EnemyBase] DamagePopup 프리팹이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
        Debug.Log($"[DamagePopup] Showing damage: {amount}");

        // 텍스트 설정
        TMP_Text text = popup.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = amount.ToString();
        }

        // 카메라 방향으로 보이게
        if (worldCamera != null)
        {
            popup.transform.LookAt(worldCamera.transform);
            popup.transform.Rotate(0, 180f, 0);
        }
        else
        {
            Debug.LogWarning("[DamagePopup] worldCamera가 비어있습니다.");
        }

        // 스케일 및 제거
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
            Debug.Log("[EnemyBase] 박스 드랍 완료");
        }
        else
        {
            Debug.LogWarning("[EnemyBase] 드랍할 박스 프리팹이 없습니다.");
        }
    }
}
