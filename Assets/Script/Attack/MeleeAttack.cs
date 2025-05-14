using System.Collections.Generic;
using UnityEngine;

public class MeleeAttack : AttackBase
{
    AttackObjectMelee attackObjectMelee;
    List<GameObject> attackEntities = new();
    List<Hitbox> hitboxes = new();
    public override bool Execute(GameObject caster, GameObject target)
    {
        if (target == null) return false;
        if (attackEntities.Count == 0) return false;
        Vector3 direction = (target.transform.position - caster.transform.position).normalized;
        caster.transform.forward = direction;

        foreach (var hitbox in hitboxes)
        {
            hitbox.Initialize(attackObj.damage, caster);
        }

        return true;
    }

    public override void Initialize(GameObject caster)
    {
        attackEntities.Clear();
        hitboxes.Clear();

        var allHitboxes = caster.GetComponentsInChildren<Hitbox>(true);
        foreach (var name in attackObjectMelee.hitboxName)
        {
            foreach (var hb in allHitboxes)
            {
                if (hb.name == name)
                {
                    attackEntities.Add(hb.gameObject);
                    hitboxes.Add(hb);
                    break;
                }
            }
        }

        if (hitboxes.Count == 0)
        {
            Debug.LogWarning("MeleeAttack: 히트박스를 하나도 찾지 못했습니다.");
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "EnableAttackEntity")
        {
            if (attackEntities.Count == 0) return;
            foreach (var entity in attackEntities)
            {
                entity.SetActive(true);
            }
        } else if (eventName == "DisableAttackEntity")
        {
            if (attackEntities.Count == 0) return;
            foreach (var entity in attackEntities)
            {
                entity.SetActive(false);
            }
        }
    }

    public MeleeAttack(AttackObjectBase attackObj)
    {
        this.attackObj = attackObj;
        attackObjectMelee = (AttackObjectMelee)attackObj;
    }
}
