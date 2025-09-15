using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/GameModeConfig")]
public class GameModeConfigSO : ScriptableObject
{
    [System.Serializable]
    public struct MinuteStep
    {
        [Tooltip("이 분 이상부터 적용(가장 큰 값이 우선)")]
        public int thresholdMinute;

        // [수정됨] Spm 관련 변수 모두 삭제

        [Header("버스트 크기 범위/선호값(정수)")]
        public int burstMin;
        public int burstMax;
        public int burstMode;

        [Header("버스트 간 간격(초) 범위")]
        public float burstCooldownMin;
        public float burstCooldownMax;

        [Header("동시 수 하드 퓨즈(0=무제한, 아주 크게 두길 권장)")]
        public int maxAliveHardCap;

        [Header("강한 몬스터 확률(%)")]
        [Range(0, 100)] public int elitePercent;
        public int elitePercentPerMin;
        [Range(0, 100)] public int elitePercentMax;

        [Header("스탯 배수(기본 + 분당 증가 + 상한)")]
        public float hpMul; public float hpMulPerMin; public float hpMulMax;
        public float dmgMul; public float dmgMulPerMin; public float dmgMulMax;
        public float moveSpeedMul; public float moveSpeedMulPerMin; public float moveSpeedMulMax;
    }

    [Header("규칙/스테이지 목록")]
    public SpawnRuleSO spawnRule;
    public List<StageConfigSO> stages = new List<StageConfigSO>();

    [Header("분 단위 단계 표(몇 개만 찍어도 됨)")]
    public List<MinuteStep> minuteSteps = new List<MinuteStep>();

    public StageConfigSO FindStage(int stageId)
        => stages.Find(s => s != null && s.stageId == stageId);

    public bool TryGetStep(int elapsedMinute, out MinuteStep step)
    {
        step = default;
        bool found = false;
        int bestThreshold = -1;
        foreach (var t in minuteSteps)
        {
            if (elapsedMinute >= t.thresholdMinute && t.thresholdMinute >= bestThreshold)
            {
                bestThreshold = t.thresholdMinute;
                step = t;
                found = true;
            }
        }
        return found;
    }
}