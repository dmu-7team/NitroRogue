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
    [SerializeReference] public BlackboardVariable<float> MoveSpeed = new BlackboardVariable<float>(3.5f);
    [SerializeReference] public BlackboardVariable<float> DistanceThreshold = new BlackboardVariable<float>(0.2f);

    private NavMeshAgent navAgent;
    private Animator anim;
    private Vector3 adjustedTargetPosition;
    private float colliderOffset;

    protected override Status OnStart()
    {
        if (Agent?.Value == null || Target?.Value == null)
        {
            return Status.Failure;
        }

        // 수정된 부분: moveSpeed 값을 BlackboardVariable에 할당
        EnemyBase enemy = Agent.Value.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            MoveSpeed.Value = enemy.MoveSpeed;
        }

        navAgent = Agent.Value.GetComponent<NavMeshAgent>();
        anim = Agent.Value.GetComponent<Animator>();
        colliderOffset = GetColliderOffset();

        adjustedTargetPosition = GetAdjustedTargetPosition();

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.speed = MoveSpeed.Value;
            navAgent.stoppingDistance = DistanceThreshold.Value;
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

        if (distance <= DistanceThreshold.Value)
        {
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

        // fallback: transform 이동
        Vector3 direction = adjustedTargetPosition - Agent.Value.transform.position;
        direction.y = 0f;
        direction.Normalize();

        Agent.Value.transform.position += direction * MoveSpeed.Value * Time.deltaTime;
        Agent.Value.transform.forward = direction;

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }
    }

    private float GetColliderOffset()
    {
        colliderOffset = 0f;
        var col = Agent.Value.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Vector3 extents = col.bounds.extents;
            colliderOffset = Mathf.Max(extents.x, extents.z);
        }
        return colliderOffset;
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
