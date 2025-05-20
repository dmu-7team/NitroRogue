using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemType {
        SpeedBoost,     // �ȱ� + �޸��� �ӵ� ����
        DamageBoost,    // ���ݷ� ����
        Heal,           // ü�� ȸ�� + �ڿ� ȸ�� ����
        LifestealBoost, // ���� ���� ����
        CritBoost,      // ġ��Ÿ Ȯ�� ����
        AmmoBoost       // �ִ� źâ ����
        }

    public ItemType itemType;
    public Sprite icon;
    public float effectAmount = 1.5f;
    public float effectDuration = 5f;
}
