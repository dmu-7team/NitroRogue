using UnityEngine;
using UnityEngine.UI;

public class MiniMapManager : MonoBehaviour
{
    [Header("카메라 설정")]
    public Transform player;              // 플레이어 트랜스폼
    public Camera miniMapCamera;           // 미니맵 카메라

    [Header("미니맵 설정")]
    public RectTransform miniMapRect;      // 미니맵 UI 영역 (RawImage)
    public Vector2 worldSize = new Vector2(100, 100);    // 실제 월드 크기
    public Vector2 miniMapSize = new Vector2(150, 150);  // 미니맵 UI 크기(px)

    [Header("아이콘 설정")]
    public RectTransform playerIcon;       // 플레이어 아이콘
    public MiniMapIconData[] monsterIcons; // 몬스터 아이콘들 (타겟 + 아이콘 세트)

    void LateUpdate()
    {
        UpdateMiniMapCamera();
        UpdateIcons();
    }

    // 플레이어 따라 카메라 이동
    private void UpdateMiniMapCamera()
    {
        if (player != null && miniMapCamera != null)
        {
            Vector3 newPosition = player.position;
            newPosition.y = miniMapCamera.transform.position.y; // 높이 고정
            miniMapCamera.transform.position = newPosition;
        }
    }

    // 아이콘 위치 업데이트
    private void UpdateIcons()
    {
        if (miniMapRect == null) return;

        // 플레이어 아이콘 이동
        if (playerIcon != null && player != null)
        {
            playerIcon.anchoredPosition = WorldToMiniMapPosition(player.position);
        }

        // 몬스터 아이콘들 이동
        foreach (var iconData in monsterIcons)
        {
            if (iconData != null && iconData.target != null && iconData.icon != null)
            {
                iconData.icon.anchoredPosition = WorldToMiniMapPosition(iconData.target.position);
            }
        }
    }

    // 월드 좌표를 미니맵 좌표로 변환
    private Vector2 WorldToMiniMapPosition(Vector3 worldPosition)
    {
        // World 중심 보정
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

    // 몬스터 아이콘 추가
    public void AddMonsterIcon(MiniMapIconData iconData)
    {
        var tempList = new System.Collections.Generic.List<MiniMapIconData>(monsterIcons);
        tempList.Add(iconData);
        monsterIcons = tempList.ToArray();
    }
}

// 아이콘 데이터 클래스
[System.Serializable]
public class MiniMapIconData
{
    public Transform target;         // 따라갈 대상 (플레이어나 몬스터 Transform)
    public RectTransform icon;       // UI 상의 아이콘
}
