using UnityEngine;
using UnityEngine.UIElements;
using static Unity.VisualScripting.Metadata;

public class MagicBallAttack : AttackBase
{
    AttackObjectMagicBall MagicBallObj;
    Transform spawnPoint;
    GameObject cachedCaster;
    Vector3 cachedDirection;
    public override bool Execute(GameObject caster, GameObject target)
    {
        if (target == null) return false;
        if (attackEntity == null) return false;

        Vector3 direction = (target.transform.position - caster.transform.position).normalized;

        caster.transform.forward = direction;
        cachedDirection = direction;

        return true;
    }

    public override void Initialize(GameObject caster)
    {
        cachedCaster = caster;
        attackEntity = MagicBallObj.attackEntity;

        if (MagicBallObj.spawnPoint == "")
        {
            spawnPoint = caster.transform;
            return;
        }

        Transform[] children = caster.transform.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t.name == MagicBallObj.spawnPoint)
            {
                spawnPoint = t;
                return;
            }
        }
    }

    public override void OnAnimationEvent(string eventName)
    {
        if (eventName == "Fire")
        {
            FireProjectile();
        }
    }

    public MagicBallAttack(AttackObjectBase attackObj)
    {
        this.attackObj = attackObj;
        this.MagicBallObj = (AttackObjectMagicBall)attackObj;
    }

    public void FireProjectile()
    {
        if (cachedCaster == null || attackEntity == null) return;

        Vector3 spawnPos = (spawnPoint != null) ? spawnPoint.position :
            cachedCaster.transform.position + Vector3.up * 1.2f + cachedDirection * 1f;

        Quaternion spawnRot = Quaternion.LookRotation(cachedDirection);

        GameObject obj = GameObject.Instantiate(attackEntity, spawnPos, spawnRot);
        Debug.DrawRay(obj.transform.position, obj.transform.forward * 3f, Color.red, 2f);

        if (MagicBallObj.followSpawner && spawnPoint != null)
            obj.transform.SetParent(spawnPoint, true);

        if (obj.TryGetComponent(out UniversalHitbox ub))
            ub.Initialize(attackObj.damage, MagicBallObj.duration, cachedCaster);
        else
            Debug.LogWarning($"[{obj.name}]에 Hitbox가 없습니다.");
    }
}
