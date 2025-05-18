using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;

public class MiniMapManager : MonoBehaviour
{
    public Camera miniMapCamera;
    public Vector2 worldSize = new Vector2(100, 100);
    public Vector2 miniMapSize = new Vector2(230, 230);

    public RectTransform miniMapRect;
    public Transform iconParent;
    public RectTransform playerIconPrefab;
    public RectTransform enemyIconPrefab;

    private Dictionary<Transform, RectTransform> trackedIcons = new();

    void Start()
    {
        InvokeRepeating(nameof(UpdateTrackedObjects), 0f, 1f);
    }

    void LateUpdate()
    {
        List<Transform> toRemove = new();

        foreach (var pair in trackedIcons)
        {
            if (pair.Key == null || pair.Value == null)
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);
                toRemove.Add(pair.Key);
                continue;
            }

            pair.Value.anchoredPosition = WorldToMiniMapPosition(pair.Key.position);
        }

        foreach (var r in toRemove)
            trackedIcons.Remove(r);

        UpdateMiniMapCamera();
    }

    void UpdateTrackedObjects()
    {
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
        {
            NetworkIdentity net = player.GetComponent<NetworkIdentity>();
            if (net != null && net.isLocalPlayer && !trackedIcons.ContainsKey(player.transform))
            {
                RectTransform icon = Instantiate(playerIconPrefab, iconParent);
                icon.gameObject.SetActive(true);
                trackedIcons.Add(player.transform, icon);
            }
        }

        foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (!trackedIcons.ContainsKey(enemy.transform))
            {
                RectTransform icon = Instantiate(enemyIconPrefab, iconParent);
                icon.gameObject.SetActive(true);
                trackedIcons.Add(enemy.transform, icon);
            }
        }
    }

    void UpdateMiniMapCamera()
    {
        foreach (var kv in trackedIcons)
        {
            if (kv.Key.CompareTag("Player"))
            {
                NetworkIdentity net = kv.Key.GetComponent<NetworkIdentity>();
                if (net != null && net.isLocalPlayer)
                {
                    Vector3 newPos = kv.Key.position;
                    newPos.y = miniMapCamera.transform.position.y;
                    miniMapCamera.transform.position = newPos;
                    break;
                }
            }
        }
    }

    Vector2 WorldToMiniMapPosition(Vector3 worldPos)
    {
        float halfW = worldSize.x * 0.5f;
        float halfH = worldSize.y * 0.5f;

        float normX = (worldPos.x + halfW) / worldSize.x;
        float normY = (worldPos.z + halfH) / worldSize.y;

        float posX = (normX * miniMapSize.x) - (miniMapSize.x * 0.5f);
        float posY = (normY * miniMapSize.y) - (miniMapSize.y * 0.5f);

        return new Vector2(posX, posY);
    }

    public void RemoveTarget(Transform target)
    {
        if (trackedIcons.ContainsKey(target))
        {
            Destroy(trackedIcons[target].gameObject);
            trackedIcons.Remove(target);
        }
    }
}
