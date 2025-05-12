using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "EnemyAttack", story: "EnemyAttack( [Self] [Target] [IsAttacking] )", category: "Action", id: "330645f32de356aac9de169119a8c1c2")]
public partial class EnemyAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> IsAttacking;

    protected override Status OnUpdate()
    {
        if (Target.Value == null) return Status.Failure;

        EnemyBase enemyBase = Self.Value.GetComponent<EnemyBase>();
        if (enemyBase == null) return Status.Failure;

        // 공격 실행
        enemyBase.UseAllAttack(Target.Value);
        IsAttacking.Value = enemyBase.isAttacking;

        return IsAttacking.Value ? Status.Success : Status.Running;
    }
}
