using UnityEngine;
using Mirror;

public class NetworkManagerChecker : MonoBehaviour
{
    void Start()
    {
        var managers = Object.FindObjectsByType<CustomNetworkManager>(FindObjectsSortMode.None);

        Debug.Log($"씬에 있는 NetworkManager 수: {managers.Length}");
        foreach (var mgr in managers)
        {
            Debug.Log($"NetworkManager 이름: {mgr.gameObject.name}");
        }
    }
}
