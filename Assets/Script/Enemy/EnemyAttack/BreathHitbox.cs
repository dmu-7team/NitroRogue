using System.Collections.Generic;
using UnityEngine;

public class BreathHitbox : MonoBehaviour
{
    private float damage;
    private float tickInterval; // 1초에 한 번씩 데미지
    private GameObject owner;

    private Dictionary<GameObject, float> nextTickTimes = new Dictionary<GameObject, float>();

    public void Initialize(float damage, float tickInterval, float duration, GameObject ownerObj)
    {
        this.damage = damage;
        this.tickInterval = tickInterval;
        owner = ownerObj;
        Destroy(gameObject, duration); // 브레스 지속 시간 끝나면 자동 제거
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject == owner) return;
        if (!other.CompareTag("Player")) return;

        GameObject target = other.gameObject;

        if (!nextTickTimes.ContainsKey(target))
        {
            nextTickTimes[target] = Time.time;
        }

        if (Time.time >= nextTickTimes[target])
        {
            var stats = target.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(damage);
                nextTickTimes[target] = Time.time + tickInterval;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 범위 벗어나면 딜 간격 관리 딕셔너리에서 제거
        nextTickTimes.Remove(other.gameObject);
    }
}
