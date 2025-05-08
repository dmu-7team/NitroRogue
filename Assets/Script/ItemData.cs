using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemType { SpeedBoost, DamageBoost }

    public ItemType itemType;
    public Sprite icon;
    public float effectAmount = 1.5f;
    public float effectDuration = 5f;
}
