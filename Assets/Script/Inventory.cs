using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class Inventory : MonoBehaviour
{
    public static Inventory instance;

    public Image[] slots;
    private ItemData[] items;

    private PlayerStats playerStatus;
    private PlayerInputActions inputActions;

    private void Awake()
    {
        if (instance == null)
            instance = this;

        playerStatus = GetComponent<PlayerStats>();
        if (playerStatus == null)
            Debug.LogError("[Inventory] PlayerStatus 연결 실패!");
    }

    private void Start()
    {
        items = new ItemData[slots.Length];

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].enabled = false;
        }

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.UseSlot1.performed += ctx => UseItem(0);
        inputActions.Player.UseSlot2.performed += ctx => UseItem(1);
        inputActions.Player.UseSlot3.performed += ctx => UseItem(2);
    }

    public bool AddItem(ItemData itemData)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                items[i] = itemData;

                if (slots[i] != null)
                {
                    slots[i].sprite = itemData.icon;
                    slots[i].enabled = true;
                }

                return true;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다.");
        return false;
    }

    private void UseItem(int index)
    {
        if (playerStatus == null || index < 0 || index >= items.Length || items[index] == null) return;

        var item = items[index];

        playerStatus.ApplyItemEffect(item.itemType, item.effectAmount, item.effectDuration);

        items[index] = null;
        slots[index].sprite = null;
        slots[index].enabled = false;
    }

    private void OnEnable() => inputActions?.Player.Enable();
    private void OnDisable() => inputActions?.Player.Disable();
}
