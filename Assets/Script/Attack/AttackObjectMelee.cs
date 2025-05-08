using UnityEngine;

[CreateAssetMenu(menuName = "Attack/Melee")]
public class AttackObjectMelee : AttackObjectBase
{
    public string hitboxName;
    public override AttackBase CreateAttackInstance()
    {
        return new MeleeAttack(this);
    }
}
