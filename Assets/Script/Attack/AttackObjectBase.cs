using UnityEngine;

public abstract class AttackObjectBase : ScriptableObject
{
    [Header("기본 정보")]
    public string attackName;
    [TextArea] public string attackDescription;
    public Sprite icon;

    [Header("전투 데이터")]
    public float cooldown;
    public float range;
    public float damage;

    public abstract AttackBase CreateAttackInstance();
}
