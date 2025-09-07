using UnityEngine;

[CreateAssetMenu(menuName = "Attack/GroundSmash")]
public class AttackObjectGroundSmash : AttackObjectMelee
{
    [Header("이펙트")]
    [Tooltip("이 공격에서 사용할 히트박스의 이름들 (애니메이션 이벤트에서 사용)")]
    public GameObject groundEffect;
    public string spawnPoint;

    public override AttackBase CreateAttackInstance()
    {
        return new GroundSmashAttack(this);
    }
}