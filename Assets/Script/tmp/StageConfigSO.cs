using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Game/StageConfig")]
public class StageConfigSO : ScriptableObject
{
    public int stageId = 0;
    public string displayName = "Stage 01";

    [Header("맵에 맞는 몬스터 풀")]
    public MapSpawnSetSO mapSpawnSet;

    [Header("보스/다음 스테이지")]
    public int nextStageId = -1;  // -1이면 마지막(보스 처치 시 게임 종료)
    public int killsToBoss = 0;   // 0이면 처치 수로 자동 소환 안 함(제단만 사용)

    [Header("서버용 NavMesh 데이터(사전 베이크)")]
    public NavMeshData[] navMeshDatas; // 서버가 Add/Remove로 장착
}
