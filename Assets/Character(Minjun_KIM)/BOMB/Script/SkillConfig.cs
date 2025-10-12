using UnityEngine;

// 이 enum은 SkillConfig 파일 상단이나 별도의 파일에 있어도 좋습니다.
public enum SkillType
{
    Projectile, // 투사체 (폭탄 등)
    Buff,       // 자신이나 아군에게 거는 버프
    Teleport    // 순간이동
}

// [CreateAssetMenu(...)] 이 부분이 Unity 에디터에서 파일을 만들 수 있게 해주는 핵심입니다.
[CreateAssetMenu(menuName = "RPG/Skill Config")]
public class SkillConfig : ScriptableObject
{
    [Header("스킬 기본 정보")]
    public SkillType type;      // 이 스킬의 종류
    public int skillId;         // 스킬 고유 ID
    public string skillName;    // 스킬 이름
    public Sprite skillIcon;    // UI에 표시될 아이콘
    [TextArea] public string description; // 스킬 설명
    public float cooldown = 10f; // 스킬 쿨타임 (초)

    [Header("투사체 스킬 정보")]
    public GameObject projectilePrefab;     // 발사할 프리팹 (어미 폭탄)
    public float launchForce = 15f;         // 발사 힘

    [Header("클러스터 스킬 정보")]
    public GameObject childProjectilePrefab; // 생성할 자탄 프리팹
    public int childProjectileCount = 4;     // 생성할 자탄 개수
    public float childLaunchForce = 10f;     // 자탄이 흩어지는 힘
}