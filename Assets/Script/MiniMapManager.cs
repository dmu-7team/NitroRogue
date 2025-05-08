using UnityEngine;
using UnityEngine.UI;

public class MiniMapManager : MonoBehaviour
{
    [Header("ī�޶� ����")]
    public Transform player;              // �÷��̾� Ʈ������
    public Camera miniMapCamera;           // �̴ϸ� ī�޶�

    [Header("�̴ϸ� ����")]
    public RectTransform miniMapRect;      // �̴ϸ� UI ���� (RawImage)
    public Vector2 worldSize = new Vector2(100, 100);    // ���� ���� ũ��
    public Vector2 miniMapSize = new Vector2(150, 150);  // �̴ϸ� UI ũ��(px)

    [Header("������ ����")]
    public RectTransform playerIcon;       // �÷��̾� ������
    public MiniMapIconData[] monsterIcons; // ���� �����ܵ� (Ÿ�� + ������ ��Ʈ)

    void LateUpdate()
    {
        UpdateMiniMapCamera();
        UpdateIcons();
    }

    // �÷��̾� ���� ī�޶� �̵�
    private void UpdateMiniMapCamera()
    {
        if (player != null && miniMapCamera != null)
        {
            Vector3 newPosition = player.position;
            newPosition.y = miniMapCamera.transform.position.y; // ���� ����
            miniMapCamera.transform.position = newPosition;
        }
    }

    // ������ ��ġ ������Ʈ
    private void UpdateIcons()
    {
        if (miniMapRect == null) return;

        // �÷��̾� ������ �̵�
        if (playerIcon != null && player != null)
        {
            playerIcon.anchoredPosition = WorldToMiniMapPosition(player.position);
        }

        // ���� �����ܵ� �̵�
        foreach (var iconData in monsterIcons)
        {
            if (iconData != null && iconData.target != null && iconData.icon != null)
            {
                iconData.icon.anchoredPosition = WorldToMiniMapPosition(iconData.target.position);
            }
        }
    }

    // ���� ��ǥ�� �̴ϸ� ��ǥ�� ��ȯ
    private Vector2 WorldToMiniMapPosition(Vector3 worldPosition)
    {
        // World �߽� ����
        float halfWorldWidth = worldSize.x * 0.5f;
        float halfWorldHeight = worldSize.y * 0.5f;

        float normalizedX = (worldPosition.x + halfWorldWidth) / worldSize.x;
        float normalizedY = (worldPosition.z + halfWorldHeight) / worldSize.y;

        float miniMapWidth = miniMapSize.x;
        float miniMapHeight = miniMapSize.y;

        float posX = (normalizedX * miniMapWidth) - (miniMapWidth * 0.5f);
        float posY = (normalizedY * miniMapHeight) - (miniMapHeight * 0.5f);

        return new Vector2(posX, posY);
    }

    // ���� ������ �߰�
    public void AddMonsterIcon(MiniMapIconData iconData)
    {
        var tempList = new System.Collections.Generic.List<MiniMapIconData>(monsterIcons);
        tempList.Add(iconData);
        monsterIcons = tempList.ToArray();
    }
}

// ������ ������ Ŭ����
[System.Serializable]
public class MiniMapIconData
{
    public Transform target;         // ���� ��� (�÷��̾ ���� Transform)
    public RectTransform icon;       // UI ���� ������
}
