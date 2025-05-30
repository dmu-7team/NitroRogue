using UnityEngine;

[CreateAssetMenu(menuName = "Attack/MagicBall")]
public class AttackObjectMagicBall : AttackObjectBase
{
    public float duration;
    public GameObject attackEntity;
    public string spawnPoint;
    public float tickInterval;
    public bool followSpawner;

    public override AttackBase CreateAttackInstance()
    {
        return new MagicBallAttack(this);
    }
}
