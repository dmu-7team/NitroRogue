using UnityEngine;

public abstract class AttackObjectBase : ScriptableObject
{
    [Header("�⺻ ����")]
    public string attackName;
    [TextArea] public string attackDescription;
    public Sprite icon;

    [Header("���� ������")]
    public float cooldown;
    public float range;
    public float damage;

    public abstract AttackBase CreateAttackInstance();
}
