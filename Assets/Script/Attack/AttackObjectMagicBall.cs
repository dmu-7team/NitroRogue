using UnityEngine;

[CreateAssetMenu(menuName = "Attack/MagicBall")]
public class AttackObjectMagicBall : AttackObjectBase
{
    public float duration;
    public GameObject attackEntity;
    public override AttackBase CreateAttackInstance()
    {
        return new MagicBallAttack(this);
    }
}
