using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "UpdateTarget", story: "[Self] Find [Target]", category: "Action", id: "7f10690fa91a27a0961ad4cdeca2e5db")]
public partial class UpdateTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    protected override Status OnStart()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        float minDistance = Mathf.Infinity;
        GameObject minDistancePlayer = null;
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(Self.Value.transform.position, player.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                minDistancePlayer = player;
            }
        }
        if (minDistancePlayer == null) return Status.Failure;
        Target.Value = minDistancePlayer;
        return Status.Success;
    }
}

