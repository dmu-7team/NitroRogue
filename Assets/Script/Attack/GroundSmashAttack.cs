using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

public class GroundSmashAttack : MeleeAttack
{
    public AttackObjectGroundSmash attackObjectGroundSmash;
    public Transform spawnPoint;

    public GroundSmashAttack(AttackObjectBase attackObj) : base(attackObj)
    {
        attackObjectGroundSmash = (AttackObjectGroundSmash)attackObj;
    }
    public override void Initialize(GameObject caster)
    {
        base.Initialize(caster);

        Transform[] children = caster.transform.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t.name == attackObjectGroundSmash.spawnPoint)
            {
                spawnPoint = t;
                return;
            }
        }
    }
    public override void OnAnimationEvent(string eventName)
    {
        base.OnAnimationEvent(eventName);
        if (eventName == "OnGroundEffect")
        {
            SpawnGroundEffect();
        }
    }


    public void SpawnGroundEffect()
    {
        if (attackObjectGroundSmash.groundEffect == null) return;
        if (spawnPoint == null) return;

        GameObject effect = GameObject.Instantiate(attackObjectGroundSmash.groundEffect, spawnPoint.position, Quaternion.identity);
        NetworkServer.Spawn(effect);
        GameObject.Destroy(effect, 2f);
    }
}
