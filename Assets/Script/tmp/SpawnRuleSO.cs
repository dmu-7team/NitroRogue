using UnityEngine;

[CreateAssetMenu(menuName = "Game/SpawnRule")]
public class SpawnRuleSO : ScriptableObject
{
    [Header("플레이어 기준 거리 제한(수평 거리)")]
    public float minDist = 15f;   // 너무 가까운 건 금지
    public float maxDist = 45f;   // 너무 먼 건 금지

    [Header("좌표 찾기 기본 옵션")]
    public float sampleMaxDistance = 2f;  // NavMesh로 스냅 허용 거리
    [Range(1, 50)] public int maxPositionAttempts = 12; // 좌표 시도 횟수

    [Header("한 번에 뿌릴 때 살짝 퍼뜨리기")]
    public float intraBurstMaxDelay = 0.3f; // 개체마다 0~이 값 만큼 랜덤 지연

    // -------- 여기부터 콜라이더/레이 없이도 동작하도록 추가된 옵션 --------
    [Header("콜라이더 없이 쓰기")]
    [Tooltip("false면 콜라이더가 없어도 NavMesh만으로 바닥 좌표를 찾습니다.")]
    public bool useGroundRay = false;      // 기본값: 콜라이더 없이 사용

    [Tooltip("NavMesh를 찾을 때 위아래로 얼마나 범위를 볼지(미터).")]
    public float verticalSearch = 10f;     // 플레이어 높이 기준 위/아래 탐색 반경

    [Tooltip("플레이어와 높이 차이가 이 값보다 크면 다른 층으로 보고 버립니다.")]
    public float maxVerticalDelta = 6f;    // 다른 층(위층/지하) 필터

    // (선택) 만약 콜라이더를 쓰고 싶다면, 아래 값들을 사용
    [Header("콜라이더를 쓸 때만 사용(옵션)")]
    [Tooltip("위에서 아래로 쏠 레이 높이(콜라이더가 있을 때만 사용).")]
    public float raycastHeight = 80f;
}
