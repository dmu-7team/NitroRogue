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
    /// Ÿ���� ��ų �������� Ȯ��
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
    /// ��ų �غ�Ǿ����� Ȯ��
    /// </summary>
    /// <returns></returns>
    public bool IsCooldownReady()
    {
        return Time.time >= lastUsedTime + attackObj.cooldown;
    }

    /// <summary>
    /// ���� ��Ÿ�� �ʱ�ȭ
    /// </summary>
    public void ForceCooldownStart()
    {
        lastUsedTime = Time.time;
    }

    /// <summary>
    /// ���� ��� ���� ���� �Ǵ�
    /// </summary>
    /// <param name="caster"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public virtual bool IsReadyToExecute(GameObject caster, GameObject target)
    {
        return IsCooldownReady() && IsInRange(caster, target);
    }
}
