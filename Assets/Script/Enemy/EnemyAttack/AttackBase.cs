using UnityEngine;

public abstract class AttackBase
{
    public AttackObjectBase attackObj;
    public GameObject attackEntity;
    public float lastUsedTime;

    public abstract bool Execute(GameObject caster, GameObject target);

    public abstract void Initialize(GameObject caster);

    public virtual void OnAnimationEvent(string eventName) { }

    /// <summary>
    /// 타겟이 스킬 범위인지 확인
    /// </summary>
    /// <param name="self"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool IsInRange(GameObject self, GameObject target)
    {
        float dis = Vector3.Distance(self.transform.position, target.transform.position);
        return dis <= attackObj.range;
    }

    /// <summary>
    /// 스킬 준비되었는지 확인
    /// </summary>
    /// <returns></returns>
    public bool IsCooldownReady()
    {
        return Time.time >= lastUsedTime + attackObj.cooldown;
    }

    /// <summary>
    /// 공격 쿨타임 초기화
    /// </summary>
    public void ForceCooldownStart()
    {
        lastUsedTime = Time.time;
    }

    /// <summary>
    /// 공격 사용 가능 여부 판단
    /// </summary>
    /// <param name="caster"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public virtual bool IsReadyToExecute(GameObject caster, GameObject target)
    {
        return IsCooldownReady() && IsInRange(caster, target);
    }
}
