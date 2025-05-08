using UnityEngine;

public class MeleeAttack : AttackBase
{
    AttackObjectMelee attackObjectMelee;
    Hitbox hitbox;
    public override bool Execute(GameObject caster, GameObject target)
    {
        if (target == null) return false;
        if (attackEntity == null) return false;

        Vector3 direction = (target.transform.position - caster.transform.position).normalized;
        caster.transform.forward = direction;

        //Debug.Log($"[Attack] {caster.name}�� {target.name}���� {attackObj.damage} ������");
        hitbox.Initialize(attackObj.damage, caster);

        return true;
    }

    public override void Initialize(GameObject caster)
    {
        var hitboxes = caster.GetComponentsInChildren<Hitbox>(true);
        foreach (var hb in hitboxes)
        {
            if (hb.name == attackObjectMelee.hitboxName)
            {
                attackEntity = hb.gameObject;
                hitbox = attackEntity.GetComponent<Hitbox>();
                break;
            }
        }
        if (hitbox == null) {
            Debug.Log("melee ��Ʈ�ڽ� �������ּ���");
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "EnableAttackEntity")
        {
            if (attackEntity == null) return;
            attackEntity.SetActive(true);
        } else if (eventName == "DisableAttackEntity")
        {
            if (attackEntity == null) return;
            attackEntity.SetActive(false);
        }
    }

    public MeleeAttack(AttackObjectBase attackObj)
    {
        this.attackObj = attackObj;
        attackObjectMelee = (AttackObjectMelee)attackObj;
    }
}
