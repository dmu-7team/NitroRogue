using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 서버 전용: 스테이지 바뀔 때 이 매치 공간에 NavMesh를 장착/해제.
/// 메인 서버 씬 전체는 건드리지 않음.
/// </summary>
public class MatchSpaceNavMesh : MonoBehaviour
{
    [Header("이 매치 공간의 기준 위치(오프셋용)")]
    public Transform anchor;

    private readonly List<NavMeshDataInstance> instances = new();

    public void ApplyStageNavMesh(StageConfigSO stage)
    {
        Clear();
        if (stage == null || stage.navMeshDatas == null) return;

        Vector3 pos = anchor ? anchor.position : Vector3.zero;
        Quaternion rot = anchor ? anchor.rotation : Quaternion.identity;

        foreach (var data in stage.navMeshDatas)
        {
            if (data == null) continue;
            var inst = NavMesh.AddNavMeshData(data, pos, rot);
            instances.Add(inst);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < instances.Count; i++)
            if (instances[i].valid) instances[i].Remove();
        instances.Clear();
    }
}
