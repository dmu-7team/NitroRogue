using UnityEngine;
using Mirror;

namespace NitroGame
{
    public class Box : NetworkBehaviour
    {
        public ItemData[] itemOptions;
        private bool isOpened = false;

        private GameObject currentPlayer;
        private bool playerInRange = false;

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
            if (isOpened) return;

            if (playerInRange && currentPlayer != null && Input.GetKeyDown(KeyCode.E))
            {
                var inventory = currentPlayer.GetComponent<Inventory>();
                var identity = currentPlayer.GetComponent<NetworkIdentity>();

                if (inventory != null && identity != null)
                {
                    inventory.CmdPickupBox(this.netIdentity);
                    isOpened = true;
                }
            }
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
