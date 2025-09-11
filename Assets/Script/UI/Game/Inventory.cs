using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Mirror;
using System.Linq;
using NitroGame;

using System.Collections;
using System.Text.RegularExpressions; // ← 이걸 꼭 추가해야 함!

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
        // 서버든 클라이언트든 공통으로 items 초기화는 반드시 해줘야 함
        items = new ItemData[3]; // 또는 슬롯 수만큼. 서버는 slots가 null이므로 수동 입력

        // 로컬 플레이어가 아니면 UI 끄기만
        if (!isLocalPlayer)
        {
            if (inventoryCanvasRoot != null)
                inventoryCanvasRoot.SetActive(false);
            return;
        }

        // 로컬 플레이어만 여기부터 UI 처리
        if (inventoryCanvasRoot != null)
            inventoryCanvasRoot.SetActive(true);

        // 슬롯 연결
        slots = inventoryCanvasRoot.GetComponentsInChildren<Image>()
            .Where(img => Regex.IsMatch(img.name, @"^Slot\d+$"))
            .OrderBy(img => img.name)
            .ToArray();

        items = new ItemData[slots.Length]; // 로컬 기준으로는 다시 슬롯 수만큼 재할당

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();

        inputActions.Player.UseSlot1.performed += ctx => UseItem(0);
        inputActions.Player.UseSlot2.performed += ctx => UseItem(1);
        inputActions.Player.UseSlot3.performed += ctx => UseItem(2);
        inputActions.Player.Interact.performed += ctx => TryPickupBox();

        for (int i=0; i<slots.Length; i++)
        {
            slots[i].enabled = false;
        }
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
        if (boxNetId == null)
        {
            Debug.LogError("[서버] boxNetId가 null입니다.");
            return;
        }

        GameObject boxObj = boxNetId.gameObject;
        NitroGame.Box box = boxObj.GetComponent<NitroGame.Box>();


        if (box == null || box.isOpened) return;

        box.isOpened = true;

        // 아이템 선택 및 부여
        ItemData item = box.GetRandomItem();
        if (item != null)
        {
            TargetAddItem(connectionToClient, item.name); // 클라에도 보내주기
            AddItem(item); // 서버에서 직접 적용
        }

        StartCoroutine(DestroyAfterDelay(boxObj, 0.1f)); // 지연 후 제거
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        NetworkServer.Destroy(obj);
    }




    [TargetRpc]
    public void TargetAddItem(NetworkConnection conn, string itemName)
    {
        var itemData = Resources.Load<ItemData>($"Items/{itemName}");
        if (itemData == null)
        {
            Debug.LogError($"[클라이언트] Resources에서 {itemName} 로드 실패");
            return;
        }

        Debug.Log($"[클라이언트] {itemName} 로드 성공");

        // items 초기화 누락되었을 경우 대비
        if (items == null || items.Length != slots.Length)
        {
            Debug.LogWarning("[클라이언트] items 배열이 초기화되지 않아 재할당");
            items = new ItemData[slots.Length];
        }

        // 아이템 넣고 index 리턴받아서 UI 업데이트
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
                return;
            }
        }

        Debug.LogWarning("[클라이언트] 인벤토리에 공간이 없습니다.");
    }




    public bool AddItem(ItemData itemData)
    {
        if (items == null)
            items = new ItemData[3];

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                items[i] = itemData;

                // 서버 → 클라이언트 UI 동기화
                if (isServer && connectionToClient != null)
                    TargetUpdateSlotUI(connectionToClient, i);

                return true;
            }
        }

        Debug.LogWarning("[Inventory] 인벤토리가 가득 찼습니다.");
        return false;
    }



    [TargetRpc]
    public void TargetUpdateSlotUI(NetworkConnection target, int index)
    {
        if (!isLocalPlayer)
        {
            Debug.LogWarning("[InventoryUI] 내 로컬 플레이어가 아님 → 무시");
            return;
        }

        if (slots == null || index >= slots.Length || items == null)
        {
            Debug.LogWarning("[InventoryUI] 슬롯 업데이트 실패 (index 불일치)");
            return;
        }

        if (items[index] == null)
        {
            Debug.LogWarning("[InventoryUI] items[index]가 null");
            return;
        }

        slots[index].sprite = items[index].icon;
        slots[index].enabled = true;

        UIManager.Instance?.ShowItemEffectMessage($"{items[index].name} 획득!");
    }


    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 자동 연결 시도
        if (inventoryCanvasRoot == null)
        {
            inventoryCanvasRoot = transform.Find("UI/InventoryCanavas")?.gameObject;
            if (inventoryCanvasRoot == null)
            {
                Debug.LogError("[Inventory] InventoryCanvas 자동 탐색 실패");
                return;
            }
        }

        inventoryCanvasRoot.SetActive(true);

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


    private void UseItem(int index)
    {
        if (items == null || slots == null) return;

        if (index < 0 || index >= items.Length) return;
        if (items[index] == null)
        {
            //UIManager.Instance?.ShowItemEffectMessage("해당 슬롯에 아이템이 없습니다!");
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
