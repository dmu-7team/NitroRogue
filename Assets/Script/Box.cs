using UnityEngine;

public class Box : MonoBehaviour
{
    public ItemData[] itemOptions; // 드랍 가능한 아이템 리스트
    private bool isOpened = false; // 중복 지급 방지용

    private void OnTriggerEnter(Collider other)
    {
        if (isOpened) return; // 이미 열렸으면 아무것도 안 함

        if (other.CompareTag("Player"))
        {
            isOpened = true; // 첫 번째 충돌만 처리
            GiveItemToPlayer(other.gameObject);
            Destroy(gameObject); // 박스 오브젝트 삭제
        }
    }

    private void GiveItemToPlayer(GameObject player)
    {
        if (itemOptions.Length == 0) return;

        int index = Random.Range(0, itemOptions.Length);
        ItemData selectedItem = itemOptions[index];

        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.AddItem(selectedItem);
            Debug.Log($"[Box] {selectedItem.name} 지급됨");
        }
    }
}
