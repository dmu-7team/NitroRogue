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

    //테스트 용도
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

    //몬스터 공격 범위 표시해주는 테스트 코드 밸런스 잡을때 필요해요
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
        RpcShowDamagePopup((int)amount); //  팝업 호출

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
            Debug.LogWarning("[EnemyBase] DamagePopup 프리팹이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        Debug.Log($"popupSpawnPoint: {popupSpawnPoint}");
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity, popupSpawnPoint);
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
            Debug.Log("[EnemyBase] 박스 드랍 완료");
        }
        else
        {
            Debug.LogWarning("[EnemyBase] 드랍할 박스 프리팹이 없습니다.");
        }
    }
}