using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Mirror;
using System.Linq;
using NitroGame; // ← 네임스페이스 추가

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
            Debug.LogError("[Inventory] PlayerStats 연결 실패!");
    }

    private void Start()
    {
        if (!isLocalPlayer) return;

        // 내 하위에 붙은 Canvas 자동 찾기
        var canvas = GetComponentInChildren<Canvas>(true); // true = 비활성화 포함
        if (canvas != null)
        {
            slots = canvas.GetComponentsInChildren<Image>()
                .Where(img => img.name.StartsWith("Slot"))
                .OrderBy(img => img.name)
                .ToArray();

            Debug.Log($"[Inventory] 슬롯 자동 연결됨: {slots.Length}개");
        }
        else
        {
            Debug.LogWarning("[Inventory] 내 하위에 Canvas 못 찾음");
        }


        items = new ItemData[slots.Length];

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.UseSlot1.performed += ctx => UseItem(0);
        inputActions.Player.UseSlot2.performed += ctx => UseItem(1);
        inputActions.Player.UseSlot3.performed += ctx => UseItem(2);
        inputActions.Player.Interact.performed += ctx => TryPickupBox(); // E 키
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

        NitroGame.Box box = boxNetId.GetComponent<NitroGame.Box>(); // 명확한 타입 지정
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
                Debug.Log($"[Inventory] 아이템 {itemData.name} 슬롯 {i}번에 추가됨");
                return true;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다.");
        return false;
    }

    private void UseItem(int index)
    {
        if (items == null || slots == null) return;

        if (index < 0 || index >= items.Length)
        {
            Debug.LogWarning($"[Inventory] 인덱스 {index}가 범위를 벗어났습니다.");
            return;
        }

        if (items[index] == null)
        {
            Debug.Log("[Inventory] 슬롯이 비어있습니다.");
            UIManager.Instance?.ShowItemEffectMessage("해당 슬롯에 아이템이 없습니다!");
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
        if (other.CompareTag("TreasureChest")) // 너가 말한 태그 기준
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
