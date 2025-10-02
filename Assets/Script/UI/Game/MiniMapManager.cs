using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Mirror;

public class MiniMapManager : MonoBehaviour
{
    [Header("MiniMap")]
    [SerializeField] private Camera miniMapCamera;
    [SerializeField] private RectTransform miniMapRect;     // RawImage 또는 틀 RectTransform
    [SerializeField] private RectTransform iconParent;      // 보통 miniMapRect와 같게 지정

    [Header("Prefabs")]
    [SerializeField] private RectTransform playerIconPrefab;       // 내 아이콘(컬러)
    [SerializeField] private RectTransform playerIconPrefabGray;   // 다른 플레이어용
    [SerializeField] private RectTransform enemyIconPrefab;

    [Header("Search")]
    [SerializeField] private float rescanInterval = 0.5f;   // 태그 재스캔 주기

    private readonly Dictionary<Transform, RectTransform> trackedIcons = new();
    private WaitForSeconds wait;

    void OnEnable()
    {
        wait = new WaitForSeconds(rescanInterval);
        StartCoroutine(RescanLoop());
    }

    IEnumerator RescanLoop()
    {
        while (true)
        {
            ScanByTag("Player");
            ScanByTag("Enemy");
            yield return wait;
        }
    }

    void ScanByTag(string tag)
    {
        var objs = GameObject.FindGameObjectsWithTag(tag);
        foreach (var go in objs)
        {
            var tr = go.transform;
            if (trackedIcons.ContainsKey(tr)) continue;

            RectTransform icon = null;

            if (tag == "Player")
            {
                var ni = go.GetComponent<NetworkIdentity>();
                bool isLocal = ni && ni.isLocalPlayer;
                icon = Instantiate(isLocal ? playerIconPrefab : playerIconPrefabGray, iconParent);
            }
            else // Enemy
            {
                icon = Instantiate(enemyIconPrefab, iconParent);
            }

            icon.gameObject.SetActive(true);
            trackedIcons.Add(tr, icon);

            // 생성 직후 한 번 위치 갱신
            icon.anchoredPosition = WorldToMiniMapPosition(tr.position);
        }
    }

    void LateUpdate()
    {
        // 위치 갱신 + 사라진 대상 정리
        var toRemove = new List<Transform>();

        foreach (var kv in trackedIcons)
        {
            var target = kv.Key;
            var icon = kv.Value;

            if (!target || !icon)
            {
                if (icon) Destroy(icon.gameObject);
                toRemove.Add(target);
                continue;
            }

            // 카메라 뷰포트 안/밖 처리
            Vector3 vp = miniMapCamera ? miniMapCamera.WorldToViewportPoint(target.position) : Vector3.zero;

            if (!miniMapCamera || vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
            {
                // 화면 밖이면 일단 감춤 (필요 시 가장자리 클램프 로직으로 교체 가능)
                icon.gameObject.SetActive(false);
            }
            else
            {
                icon.gameObject.SetActive(true);
                icon.anchoredPosition = ViewportToMiniMapPosition(vp);
            }
        }

        foreach (var r in toRemove)
        {
            if (r && trackedIcons.TryGetValue(r, out var ic) && ic) Destroy(ic.gameObject);
            trackedIcons.Remove(r);
        }

        FollowLocalPlayerWithCamera();
    }

    void FollowLocalPlayerWithCamera()
    {
        if (!miniMapCamera) return;

        foreach (var kv in trackedIcons)
        {
            var tr = kv.Key;
            if (!tr || !tr.CompareTag("Player")) continue;

            var ni = tr.GetComponent<NetworkIdentity>();
            if (ni && ni.isLocalPlayer)
            {
                Vector3 pos = tr.position;
                pos.y = miniMapCamera.transform.position.y;
                miniMapCamera.transform.position = pos;
                break;
            }
        }
    }

    // === 좌표 변환: 카메라 기준으로 1:1 매칭 ===
    Vector2 WorldToMiniMapPosition(Vector3 worldPos)
    {
        if (!miniMapCamera || !miniMapRect) return Vector2.zero;
        return ViewportToMiniMapPosition(miniMapCamera.WorldToViewportPoint(worldPos));
    }

    Vector2 ViewportToMiniMapPosition(Vector3 vp)
    {
        // vp.x/y: 0~1 → 미니맵 Rect 중앙 기준 좌표
        Vector2 size = miniMapRect.rect.size;
        float x = (vp.x - 0.5f) * size.x;
        float y = (vp.y - 0.5f) * size.y;
        return new Vector2(x, y);
    }

    // 외부에서 수동 제거할 때 호출 가능
    public void RemoveTarget(Transform target)
    {
        if (target && trackedIcons.TryGetValue(target, out var icon))
        {
            if (icon) Destroy(icon.gameObject);
            trackedIcons.Remove(target);
        }
    }
}
