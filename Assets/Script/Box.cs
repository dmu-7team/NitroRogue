using UnityEngine;
using Mirror;

namespace NitroGame
{
    public class Box : NetworkBehaviour
    {
        [Header("아이템 목록")]
        public ItemData[] itemOptions; // 인벤토리에 추가할 아이템들 (ScriptableObject)

        [HideInInspector]
        public bool isOpened = false;


        private GameObject currentPlayer;
        private bool playerInRange = false;
        private NetworkIdentity myNetId;

        private void Awake()
        {
            myNetId = GetComponent<NetworkIdentity>();
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                currentPlayer = other.gameObject;
                playerInRange = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") && other.gameObject == currentPlayer)
            {
                currentPlayer = null;
                playerInRange = false;
            }
        }

        private void Update()
        {
            if (!isLocalPlayer || isOpened) return;

            if (playerInRange && currentPlayer != null && Input.GetKeyDown(KeyCode.E))
            {
                var inventory = currentPlayer.GetComponent<Inventory>();

                if (inventory != null && myNetId != null)
                {
                    inventory.CmdPickupBox(myNetId);
                    isOpened = true;
                }
                else
                {
                    Debug.LogWarning("[Box] inventory 또는 myNetId가 null입니다");
                }
            }
        }
        public ItemData GetRandomItem()
        {
            if (itemOptions == null || itemOptions.Length == 0) return null;
            int index = Random.Range(0, itemOptions.Length);
            return itemOptions[index];
        }

        public void GiveItemToPlayer(GameObject player)
        {
            if (itemOptions == null || itemOptions.Length == 0) return;

            int index = Random.Range(0, itemOptions.Length);
            ItemData selectedItem = itemOptions[index];

            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory != null)
            {
                inventory.AddItem(selectedItem);
                Debug.Log($"[Box] {selectedItem.name} 지급 완료");
            }
            else
            {
                Debug.LogWarning("[Box] Inventory 컴포넌트를 찾을 수 없습니다.");
            }
        }
    }
}
