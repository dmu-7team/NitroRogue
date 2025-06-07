using UnityEngine;

[CreateAssetMenu(menuName = "Data/MonsterSpawnPointData")]
public class MonsterSpawnPointData : ScriptableObject
{
    public Vector3[] points; // 인스펙터에서 스폰 위치를 미리 세팅
}
