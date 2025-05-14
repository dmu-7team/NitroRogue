using UnityEngine;

[CreateAssetMenu(menuName = "Attack/Melee")]
public class AttackObjectMelee : AttackObjectBase
{
    [Header("��Ʈ�ڽ� �̸���")]
    [Tooltip("�� ���ݿ��� ����� ��Ʈ�ڽ��� �̸��� (�ִϸ��̼� �̺�Ʈ���� ���)")]
    public string[] hitboxName;
    public override AttackBase CreateAttackInstance()
    {
        return new MeleeAttack(this);
    }
}
