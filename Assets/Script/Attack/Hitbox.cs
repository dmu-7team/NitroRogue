using UnityEngine;

public class Hitbox : MonoBehaviour
{
    float damage;
    GameObject owner;

    public void Initialize(float dmg, GameObject ownerObj)
    {
        damage = dmg;
        owner = ownerObj;
    }

    public void Initialize(float dmg, GameObject ownerObj, float duration = 10.0f)
    {
        damage = dmg;
        owner = ownerObj;
        Destroy(gameObject, duration);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner) return;
        if (other.gameObject.CompareTag("Player"))
        {
            other.gameObject.GetComponent<PlayerStats>().TakeDamage(damage);
        }
    }
}
