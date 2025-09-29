using Mirror;
using UnityEngine;

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

        float casterAtk = cachedCaster.GetComponent<EnemyBase>().AttackDamage;
        float finalDamage = casterAtk * attackObj.damageCoefficient;
        GameObject projObj;

        if (MagicBallObj.followSpawner && spawnPoint != null)
        {
            // position/rotation 모두 spawnPoint 그대로 가져옴
            projObj = GameObject.Instantiate(
                attackEntity,
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
        else
        {
            Vector3 spawnPos = (spawnPoint != null)
                ? spawnPoint.position
                : cachedCaster.transform.position + Vector3.up * 1.2f + cachedDirection;
            Quaternion spawnRot = Quaternion.LookRotation(cachedDirection);

            projObj = GameObject.Instantiate(
                attackEntity,
                spawnPos,
                spawnRot
            );
        }

        var casterMatch = cachedCaster.GetComponent<NetworkMatch>();
        var projMatch = projObj.GetComponent<NetworkMatch>();
        if (casterMatch != null && projMatch != null)
        {
            projMatch.matchId = casterMatch.matchId;
        }
        else if (projMatch == null)
        {
            Debug.LogWarning($"[{projObj.name}]에 NetworkMatch 컴포넌트가 없습니다.");
        }
        else
        {
            Debug.LogWarning($"[{cachedCaster.name}]에 NetworkMatch 컴포넌트가 없습니다.");
        }

        var projScript = projObj.GetComponent<Projectile>();
        projScript.casterNetId = cachedCaster.GetComponent<NetworkIdentity>().netId;
        projScript.spawnPointName = MagicBallObj.spawnPoint;
        projScript.followSpawner = MagicBallObj.followSpawner;

        NetworkServer.Spawn(projObj);

        if (projObj.TryGetComponent(out UniversalHitbox ub))
            ub.Initialize(finalDamage, MagicBallObj.duration, cachedCaster);
        else
            Debug.LogWarning($"[{projObj.name}]에 Hitbox가 없습니다.");
    }
}
