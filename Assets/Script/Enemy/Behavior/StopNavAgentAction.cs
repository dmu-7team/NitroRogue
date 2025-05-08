using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "StopNavAgentAction", story: "[Self] stop", category: "Action", id: "258ddd75aac8358ea6f5dc7ee159e5f4")]
public partial class StopNavAgentAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    protected override Status OnStart()
    {
        var nav = Self?.Value?.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.isOnNavMesh)
        {
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }
        return Status.Success;
    }
}

