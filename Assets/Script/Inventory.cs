using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Mirror;
using System.Linq;
using NitroGame;

public class Inventory : NetworkBehaviour
{
    public GameObject inventoryCanvasRoot;
    public Image[] slots;
    private ItemData[] items;

    private PlayerStats playerStatus;
    private PlayerInputActions inputActions;
    private GameObject boxInRange;

    private void Awake()
    {
        playerStatus = GetComponent<PlayerStats>();
        if (playerStatus == null)
            Debug.LogError("[Inventory] PlayerStats 연결 실패!");
    }

    private void Start()
    {
        if (!isLocalPlayer)
        {
            if (inventoryCanvasRoot != null)
                inventoryCanvasRoot.SetActive(false);
            return;
        }

        if (inventoryCanvasRoot != null)
            inventoryCanvasRoot.SetActive(true);

        // 슬롯 연결
        slots = inventoryCanvasRoot.GetComponentsInChildren<Image>()
            .Where(img => img.name.StartsWith("Slot"))
            .OrderBy(img => img.name)
            .ToArray();

        items = new ItemData[slots.Length];

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.UseSlot1.performed += ctx => UseItem(0);
        inputActions.Player.UseSlot2.performed += ctx => UseItem(1);
        inputActions.Player.UseSlot3.performed += ctx => UseItem(2);
        inputActions.Player.Interact.performed += ctx => TryPickupBox();
    }

    private void TryPickupBox()
    {
        if (boxInRange == null) return;

        var boxIdentity = boxInRange.GetComponent<NetworkIdentity>();
        if (boxIdentity != null)
        {
            CmdPickupBox(boxIdentity);
        }
    }

    [Command]
    public void CmdPickupBox(NetworkIdentity boxNetId)
    {
        if (boxNetId == null) return;

        NitroGame.Box box = boxNetId.GetComponent<NitroGame.Box>();
        if (box == null) return;

        box.GiveItemToPlayer(gameObject);
        NetworkServer.Destroy(box.gameObject);
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

                UIManager.Instance?.ShowItemEffectMessage($"{itemData.name} 획득!");
                return true;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다.");
        return false;
    }

    private void UseItem(int index)
    {
        if (items == null || slots == null) return;

        if (index < 0 || index >= items.Length) return;
        if (items[index] == null)
        {
            UIManager.Instance?.ShowItemEffectMessage("해당 슬롯에 아이템이 없습니다!");
            return;
        }

        playerStatus?.ApplyItemEffect(items[index].itemType, items[index].effectAmount, items[index].effectDuration);
        items[index] = null;

        if (slots[index] != null)
        {
            slots[index].sprite = null;
            slots[index].enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TreasureChest"))
            boxInRange = other.gameObject;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("TreasureChest") && other.gameObject == boxInRange)
            boxInRange = null;
    }

    private void OnEnable() => inputActions?.Player.Enable();
    private void OnDisable() => inputActions?.Player.Disable();
}
