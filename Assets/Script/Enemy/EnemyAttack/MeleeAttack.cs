using System.Collections.Generic;
using UnityEngine;

public class MeleeAttack : AttackBase
{
    AttackObjectMelee attackObjectMelee;
    List<GameObject> attackEntities = new();
    List<UniversalHitbox> hitboxes = new();
    protected GameObject cachedCaster;

    public override bool Execute(GameObject caster, GameObject target)
    {
        if (target == null) return false;
        if (attackEntities.Count == 0) return false;
        Vector3 direction = (target.transform.position - caster.transform.position).normalized;
        caster.transform.forward = direction;
        cachedCaster = caster;
        float casterAtk = caster.GetComponent<EnemyBase>().AttackDamage;
        float finalDamage = casterAtk * attackObj.damageCoefficient;
        foreach (var hitbox in hitboxes)
        {
            hitbox.Initialize(casterAtk * attackObj.damageCoefficient, 0f,caster);
        }

        return true;
    }

    public override void Initialize(GameObject caster)
    {
        attackEntities.Clear();
        hitboxes.Clear();

        var allHitboxes = caster.GetComponentsInChildren<UniversalHitbox>(true);
        foreach (var name in attackObjectMelee.hitboxName)
        {
            foreach (var ub in allHitboxes)
            {
                if (ub.name == name)
                {
                    attackEntities.Add(ub.gameObject);
                    hitboxes.Add(ub);
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
        if (attackEntities.Count == 0) return;
        if (eventName == "EnableAttackEntity")
        {
            foreach (var entity in attackEntities)
            {
                entity.SetActive(true);
            }
        } else if (eventName == "DisableAttackEntity")
        {
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
