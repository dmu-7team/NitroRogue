using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemType {
        SpeedBoost,     // 걷기 + 달리기 속도 증가
        DamageBoost,    // 공격력 증가
        Heal,           // 체력 회복 + 자연 회복 증가
        LifestealBoost, // 피해 흡혈 증가
        CritBoost,      // 치명타 확률 증가
        AmmoBoost       // 최대 탄창 증가
        }

    public ItemType itemType;
    public Sprite icon;
    public float effectAmount = 1.5f;
    public float effectDuration = 5f;
}
