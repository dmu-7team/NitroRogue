using UnityEngine;

public class MagicBallAttack : AttackBase
{
    AttackObjectMagicBall MagicBallObj;
    public override bool Execute(GameObject caster, GameObject target)
    {
        if (target == null) return false;
        if (attackEntity == null) return false;

        Vector3 direction = (target.transform.position - caster.transform.position).normalized;
        caster.transform.forward = direction;

        //Debug.Log($"[Attack] {caster.name}이 {target.name}에게 {attackObj.damage} 데미지");

        GameObject magicBall = GameObject.Instantiate(attackEntity,
        caster.transform.position + Vector3.up * 1.2f + direction * 1f, // 머리 높이에서 살짝 앞에 생성
        Quaternion.LookRotation(direction));

        MagicBallHitbox hitbox = magicBall.GetComponent<MagicBallHitbox>();
        hitbox.Initialize(attackObj.damage, caster, MagicBallObj.duration);

        return true;
    }

    public override void Initialize(GameObject caster)
    {
        attackEntity = MagicBallObj.attackEntity;
    }

    public MagicBallAttack(AttackObjectBase attackObj)
    {
        this.attackObj = attackObj;
        this.MagicBallObj = (AttackObjectMagicBall)attackObj;
    }
}
