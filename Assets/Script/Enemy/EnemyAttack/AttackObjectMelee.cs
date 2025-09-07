using UnityEngine;

[CreateAssetMenu(menuName = "Attack/Melee")]
public class AttackObjectMelee : AttackObjectBase
{
    [Header("히트박스 이름들")]
    [Tooltip("이 공격에서 사용할 히트박스의 이름들 (애니메이션 이벤트에서 사용)")]
    public string[] hitboxName;
    public override AttackBase CreateAttackInstance()
    {
        return new MeleeAttack(this);
    }
}
