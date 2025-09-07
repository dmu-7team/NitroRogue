using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NavigateToTargetAction", story: "[Self] navigates to [Target]", category: "Action", id: "c05ec5746e70d3a631ce9ca53a83d52a")]
public partial class NavigateToTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    private float MoveSpeed = 3.5f;
    private float DistanceThreshold = 0.2f;

    private NavMeshAgent navAgent;
    private Animator anim;
    private Vector3 adjustedTargetPosition;

    protected override Status OnStart()
    {
        if (Agent?.Value == null || Target?.Value == null)
            return Status.Failure;

        // MoveSpeed: EnemyBase에서 가져옴
        EnemyBase enemy = Agent.Value.GetComponent<EnemyBase>();
        if (enemy != null)
            MoveSpeed = enemy.MoveSpeed;

        navAgent = Agent.Value.GetComponent<NavMeshAgent>();
        anim = Agent.Value.GetComponent<Animator>();

        // ✅ 에이전트와 타겟 콜라이더 크기 합산해서 거리 설정
        DistanceThreshold = GetCombinedColliderOffset();

        adjustedTargetPosition = GetAdjustedTargetPosition();

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.speed = MoveSpeed;
            navAgent.stoppingDistance = DistanceThreshold;
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.SetDestination(adjustedTargetPosition);
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Agent?.Value == null || Target?.Value == null)
        {
            Debug.LogWarning("[NavigateToTarget] Agent or Target is null.");
            return Status.Failure;
        }

        float distance = GetDistanceXZ();

        if (distance <= DistanceThreshold)
        {
            anim?.SetFloat("speed", 0f);
            return Status.Success;
        }

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            adjustedTargetPosition = GetAdjustedTargetPosition();
            navAgent.SetDestination(adjustedTargetPosition);

            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                anim?.SetFloat("speed", 0f);
                return Status.Success;
            }

            anim?.SetFloat("speed", 1f);
            return Status.Running;
        }

        // fallback: 직접 Transform 이동
        Vector3 direction = adjustedTargetPosition - Agent.Value.transform.position;
        direction.y = 0f;
        direction.Normalize();

        Agent.Value.transform.position += direction * MoveSpeed * Time.deltaTime;
        Agent.Value.transform.forward = direction;

        anim?.SetFloat("speed", 1f);
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }

        anim?.SetFloat("speed", 0f);
    }

    // ✅ 에이전트 + 타겟 콜라이더 크기 합산 거리 계산
    private float GetCombinedColliderOffset()
    {
        float self = GetColliderRadius(Agent.Value);
        float target = GetColliderRadius(Target.Value);
        return self + target;
    }

    private float GetColliderRadius(GameObject obj)
    {
        var col = obj?.GetComponentInChildren<Collider>();
        if (col == null) return 0.5f;

        Vector3 extents = col.bounds.extents;
        return Mathf.Max(extents.x, extents.z);
    }

    private Vector3 GetAdjustedTargetPosition()
    {
        var targetCol = Target.Value.GetComponentInChildren<Collider>();
        if (targetCol != null)
        {
            return targetCol.ClosestPoint(Agent.Value.transform.position);
        }
        return Target.Value.transform.position;
    }

    private float GetDistanceXZ()
    {
        Vector3 a = Agent.Value.transform.position;
        Vector3 b = adjustedTargetPosition;
        a.y = b.y;
        return Vector3.Distance(a, b);
    }
}
