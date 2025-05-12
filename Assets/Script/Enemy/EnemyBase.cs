using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using TMPro;
using Mirror;
using Unity.Behavior;

[RequireComponent(typeof(NetworkIdentity))]
public class EnemyBase : CharacterStats
{
    [Header("���� ����")]
    public AttackObjectBase[] attackObjs;

    [Header("�ִϸ��̼�/AI")]
    private Animator animator;
    private NetworkAnimator networkAnimator;
    private BehaviorGraphAgent behaviorAgent;
    public bool isAttacking = false;
    private NavMeshAgent agent;
    [Header("ü�¹� UI")]
    public Image healthBarImage;

    [Header("��� �ڽ�")]
    public GameObject boxPrefab;

    [Header("������ �˾�")]
    public GameObject damagePopupPrefab;
    public Transform popupSpawnPoint;

    [Header("AI Ÿ�� ����")]
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
                Debug.Log($"[EnemyBase] Target ���� �Ϸ�: {nearestPlayer.name}");
            }
            else
            {
                Debug.LogWarning("[EnemyBase] ��ó�� �÷��̾ ã�� ���߽��ϴ�.");
            }
        }
        else
        {
            if (!NetworkServer.active)
                Debug.LogWarning("[EnemyBase] ������ �ƴ�. Target ���� ����.");
            if (targetVar == null)
                Debug.LogWarning("[EnemyBase] targetVar�� ������� �ʾҽ��ϴ�.");
        }

        // ������: Animator �Ķ���� Ȯ��
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            Debug.Log($"[Animator] �Ķ���� �̸�: {param.name}, Ÿ��: {param.type}");
        }
        agent = GetComponent<NavMeshAgent>();
        if (!isServer)
        {
            agent.enabled = false; // Ŭ���̾�Ʈ������ NavMeshAgent ��
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

        Debug.Log($"[EnemyBase] �Ÿ�: {dist}, OnNavMesh={agent.isOnNavMesh}, isStopped={agent.isStopped}");

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

        // Animator ���°� Move�� ���� ���� ������ �ɾ�� �ϹǷ�,
        // �ϴ� Move ���·� ���� (SetFloat("speed", 1) �Ǵ� Trigger Ȱ��)
        animator.Play("Move");

        // ���� �ε��� ���� (1~8)
        int attackIndex = Random.Range(1, 9);
        animator.SetInteger("attackInd", attackIndex);
        animator.SetBool("doAttack", true);

        Debug.Log($"[EnemyBase] ���� ���� �� attackInd: {attackIndex}");
    }


    public void OnAttackAnimationEnd()
    {
        Debug.Log("[EnemyBase] ���� �ִϸ��̼� ���� �� ���� �ʱ�ȭ");
        isAttacking = false;
        animator.SetBool("doAttack", false);
    }


    public void OnAnimationEvent()
    {
        Debug.Log("[EnemyBase] OnAnimationEvent �����");

        if (currentTarget == null)
        {
            Debug.LogWarning("[EnemyBase] currentTarget�� null��");
            return;
        }

        var stats = currentTarget.GetComponent<CharacterStats>();
        if (stats == null)
        {
            Debug.LogWarning("[EnemyBase] currentTarget�� CharacterStats ����");
            return;
        }

        stats.TakeDamage(attackObjs[0].damage, gameObject);
        Debug.Log($"�� {currentTarget.name}���� {attackObjs[0].damage} ������!");
    }


    private void OnDestroy()
    {
        // ���� �ʿ� �� ó��
    }
}

// Animator�� �ش� �Ķ���Ͱ� �����ϴ��� �����ϰ� Ȯ���ϴ� Ȯ�� �޼���
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

