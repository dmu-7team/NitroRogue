using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Unity.Behavior;
using TMPro;
using System.Collections.Generic;
using Mirror;
using System.Collections;

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

    //�׽�Ʈ �뵵
    Color[] colors = new Color[8]
    {
        Color.red,
        Color.yellow,
        Color.green,
        Color.cyan,
        Color.blue,
        Color.magenta,
        Color.gray,
        Color.white
    };

    //���� ���� ���� ǥ�����ִ� �׽�Ʈ �ڵ� �뷱�� ������ �ʿ��ؿ�
    void OnDrawGizmosSelected()
    {
        for (int i = 0; i < attackObjs.Length; i++)
        {
            Gizmos.color = colors[i];
            Gizmos.DrawWireSphere(transform.position, attackObjs[i].range);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

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

        RpcUpdateHealthBar(CurrentHealth, MaxHealth);
    }

    [Server]
    public virtual void UseAllAttack(GameObject target)
    {
        for (int i = 0; i < attacks.Count; i++)
        {
            if (attacks[i].IsReadyToExecute(gameObject, target))
            {
                currentAttack = attacks[i];
                attacks[i].Execute(gameObject, target);

                isAttacking = true;
                RpcPlayAttackAnimation(i);
                return;
            }
        }
        isAttacking = false;
    }

    [ClientRpc]
    void RpcPlayAttackAnimation(int index)
    {
        currentAttack = attacks[index];
        animator.SetInteger("attackIndex", index);
        animator.SetTrigger("doAttack");
    }

    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        behavior?.SetVariableValue("IsAttacking", isAttacking);

        currentAttack?.ForceCooldownStart();
    }

    public void OnAnimationEvent(string eventName)
    {
        if (currentAttack == null) return;
        currentAttack.OnAnimationEvent(eventName);
    }

    [Server]
    public override void TakeDamage(float amount, GameObject attacker)
    {
        currentHealth -= amount;
        RpcUpdateHealthBar(CurrentHealth, MaxHealth);
        RpcShowDamagePopup((int)amount); //  �˾� ȣ��

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    [ClientRpc]
    private void RpcUpdateHealthBar(float current, float max)
    {
        if (healthBarImage != null)
        {
            healthBarImage.fillAmount = current / max;
        }
    }

    [ClientRpc]
    private void RpcShowDamagePopup(int amount)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[EnemyBase] DamagePopup �������� ������� �ʾҽ��ϴ�.");
            return;
        }

        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        Debug.Log($"popupSpawnPoint: {popupSpawnPoint}");
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity, popupSpawnPoint);
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

    [Server]
    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        behavior?.SetVariableValue("IsDead", true);
        
        RpcPlayDeathAnimation();
        DropBox();
        StartCoroutine(DelayedDestroy());
        NetworkMissionManager.Instance.CheckMissionProgress();

    }

    [ClientRpc]
    void RpcPlayDeathAnimation()
    {
        animator.SetTrigger("doDie");
    }

    [Server]
    private IEnumerator DelayedDestroy()
    {
        yield return new WaitForSeconds(3f);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    private void DropBox()
    {
        if (boxPrefab != null)
        {
            GameObject box = Instantiate(boxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            NetworkServer.Spawn(box);
            Debug.Log("[EnemyBase] �ڽ� ��� �Ϸ�");
        }
        else
        {
            Debug.LogWarning("[EnemyBase] ����� �ڽ� �������� �����ϴ�.");
        }
    }
}