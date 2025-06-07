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
    [SerializeField] private GameObject bodyRoot;

    [SerializeField] private float _attackDamage = 10f;
    [SerializeField] private float _moveSpeed = 3f;

    public override float AttackDamage => _attackDamage;
    public override float MoveSpeed => _moveSpeed;

    [Header("애니메이션/AI")]
    private Animator animator;
    private BehaviorGraphAgent behavior;
    private NavMeshAgent navMeshAgent;
    public bool isAttacking = false;

    [Header("체력바 UI")]
    public Image healthBarImage;

    [Header("드랍 박스")]
    public GameObject boxPrefab;

    [Header("데미지 팝업")]
    public GameObject damagePopupPrefab;
    public Transform popupSpawnPoint;
    public Camera worldCamera;

    private bool isDead = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    [ServerCallback]
    private void Start()
    {
        // 서버에서 필요한 초기화는 OnStartServer에서 처리
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (worldCamera == null) worldCamera = Camera.main;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        behavior = GetComponent<BehaviorGraphAgent>();
        navMeshAgent = GetComponent<NavMeshAgent>();

        if (navMeshAgent != null)
            navMeshAgent.speed = MoveSpeed;

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
        if (bodyRoot == null || isAttacking) return;

        foreach (var attack in attacks)
        {
            if (attack.IsReadyToExecute(bodyRoot, target))
            {
                currentAttack = attack;
                attack.Execute(gameObject, target);
                isAttacking = true;
                PlayAttackAnimation(attacks.IndexOf(attack));
                RpcPlayAttackAnimation(attacks.IndexOf(attack));

                StartCoroutine(ResetAttackStateAfterDelay(attack.attackObj.cooldown + 0.5f));
                return;
            }
        }
    }

    [Server]
    private IEnumerator ResetAttackStateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isAttacking = false;
        behavior?.SetVariableValue("IsAttacking", false);
    }

    void PlayAttackAnimation(int index)
    {
        animator.SetInteger("attackIndex", index);
        animator.SetTrigger("doAttack");
    }

    [ClientRpc]
    void RpcPlayAttackAnimation(int index)
    {
        if (animator == null) return;
        animator.SetInteger("attackIndex", index);
        animator.SetTrigger("doAttack");
    }

    public void OnAttackAnimationEnd()
    {
        if (!isServer) return;

        isAttacking = false;
        behavior?.SetVariableValue("IsAttacking", false);
        currentAttack?.ForceCooldownStart();
    }

    public void OnAnimationEvent(string eventName)
    {
        if (!isServer || currentAttack == null) return;
        currentAttack.OnAnimationEvent(eventName);
    }

    [Server]
    public override void TakeDamage(float amount, GameObject attacker)
    {
        currentHealth -= amount;
        RpcUpdateHealthBar(CurrentHealth, MaxHealth);
        RpcShowDamagePopup((int)amount);

        if (attacker != null && attacker.TryGetComponent<PlayerStats>(out var player))
        {
            player.TotalDamage += amount;
        }

        if (CurrentHealth <= 0)
        {
            Die(attacker);
        }
    }

    [ClientRpc]
    private void RpcUpdateHealthBar(float current, float max)
    {
        if (healthBarImage != null)
            healthBarImage.fillAmount = current / max;
    }

    [ClientRpc]
    private void RpcShowDamagePopup(int amount)
    {
        if (damagePopupPrefab == null) return;

        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
        TMP_Text text = popup.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = amount.ToString();

        if (worldCamera != null)
        {
            popup.transform.LookAt(worldCamera.transform);
            popup.transform.Rotate(0, 180f, 0);
        }

        popup.transform.localScale = Vector3.one * 0.01f;
        Destroy(popup, 1.2f);
    }

    [Server]
    protected override void Die()
    {
        Die(null);
    }

    [Server]
    public void Die(GameObject killer)
    {
        if (isDead) return;
        isDead = true;

        if (killer != null && killer.TryGetComponent<PlayerStats>(out var killerStats))
        {
            killerStats.KillCount++;
        }

        behavior?.SetVariableValue("IsDead", true);
        RpcPlayDeathAnimation();
        DropBox();
        StartCoroutine(DelayedDestroy());
        NetworkMissionManager.Instance?.CheckMissionProgress();
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
        if (boxPrefab == null)
        {
            Debug.LogWarning("[EnemyBase] 박스 프리팹이 없습니다.");
            return;
        }

        GameObject box = Instantiate(boxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        NetworkServer.Spawn(box);
        Debug.Log("[EnemyBase] 박스 드랍 완료");
    }

    void OnDrawGizmosSelected()
    {
        if (bodyRoot == null || attackObjs == null) return;

        Color[] debugColors = { Color.red, Color.green, Color.blue, Color.yellow };
        for (int i = 0; i < attackObjs.Length; i++)
        {
            Gizmos.color = debugColors[i % debugColors.Length];
            Gizmos.DrawWireSphere(bodyRoot.transform.position, attackObjs[i].range);
        }
    }
}
