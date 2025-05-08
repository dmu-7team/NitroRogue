using UnityEngine;

public class Box : MonoBehaviour
{
    public ItemData[] itemOptions; // ��� ������ ������ ����Ʈ
    private bool isOpened = false; // �ߺ� ���� ������

    private void OnTriggerEnter(Collider other)
    {
        if (isOpened) return; // �̹� �������� �ƹ��͵� �� ��

        if (other.CompareTag("Player"))
        {
            isOpened = true; // ù ��° �浹�� ó��
            GiveItemToPlayer(other.gameObject);
            Destroy(gameObject); // �ڽ� ������Ʈ ����
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
            Debug.Log($"[Box] {selectedItem.name} ���޵�");
        }
    }
}
