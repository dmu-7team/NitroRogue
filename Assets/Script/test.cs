using UnityEngine;
using Mirror;

public class NetworkManagerChecker : MonoBehaviour
{
    void Start()
    {
        var managers = Object.FindObjectsByType<CustomNetworkManager>(FindObjectsSortMode.None);

        Debug.Log($"���� �ִ� NetworkManager ��: {managers.Length}");
        foreach (var mgr in managers)
        {
            Debug.Log($"NetworkManager �̸�: {mgr.gameObject.name}");
        }
    }
}
