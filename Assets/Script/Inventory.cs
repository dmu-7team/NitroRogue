using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Mirror;
using System.Linq;
using NitroGame; // �� ���ӽ����̽� �߰�

public class Inventory : NetworkBehaviour
{
    public Image[] slots;
    private ItemData[] items;

    private PlayerStats playerStatus;
    private PlayerInputActions inputActions;

    private GameObject boxInRange;

    private void Awake()
    {
        playerStatus = GetComponent<PlayerStats>();
        if (playerStatus == null)
            Debug.LogError("[Inventory] PlayerStats ���� ����!");
    }

    private void Start()
    {
        if (!isLocalPlayer) return;

        // �� ������ ���� Canvas �ڵ� ã��
        var canvas = GetComponentInChildren<Canvas>(true); // true = ��Ȱ��ȭ ����
        if (canvas != null)
        {
            slots = canvas.GetComponentsInChildren<Image>()
                .Where(img => img.name.StartsWith("Slot"))
                .OrderBy(img => img.name)
                .ToArray();

            Debug.Log($"[Inventory] ���� �ڵ� �����: {slots.Length}��");
        }
        else
        {
            Debug.LogWarning("[Inventory] �� ������ Canvas �� ã��");
        }


        items = new ItemData[slots.Length];

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.UseSlot1.performed += ctx => UseItem(0);
        inputActions.Player.UseSlot2.performed += ctx => UseItem(1);
        inputActions.Player.UseSlot3.performed += ctx => UseItem(2);
        inputActions.Player.Interact.performed += ctx => TryPickupBox(); // E Ű
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

        NitroGame.Box box = boxNetId.GetComponent<NitroGame.Box>(); // ��Ȯ�� Ÿ�� ����
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

                UIManager.Instance?.ShowItemEffectMessage($"{itemData.name} ȹ��!");
                Debug.Log($"[Inventory] ������ {itemData.name} ���� {i}���� �߰���");
                return true;
            }
        }

        Debug.Log("�κ��丮�� ���� á���ϴ�.");
        return false;
    }

    private void UseItem(int index)
    {
        if (items == null || slots == null) return;

        if (index < 0 || index >= items.Length)
        {
            Debug.LogWarning($"[Inventory] �ε��� {index}�� ������ ������ϴ�.");
            return;
        }

        if (items[index] == null)
        {
            Debug.Log("[Inventory] ������ ����ֽ��ϴ�.");
            UIManager.Instance?.ShowItemEffectMessage("�ش� ���Կ� �������� �����ϴ�!");
            return;
        }

        if (playerStatus == null) return;

        var item = items[index];
        playerStatus.ApplyItemEffect(item.itemType, item.effectAmount, item.effectDuration);

        items[index] = null;
        if (slots[index] != null)
        {
            slots[index].sprite = null;
            slots[index].enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TreasureChest")) // �ʰ� ���� �±� ����
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
