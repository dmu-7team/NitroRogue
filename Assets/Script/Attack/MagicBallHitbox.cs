using UnityEngine;

public class MagicBallHitbox : MonoBehaviour
{
    private float damage;
    private GameObject owner;
    private float duration;
    private float speed = 10f;
    private bool initialized = false;
    private Vector3 direction;

    public GameObject hitParticle;

    public void Initialize(float damage, GameObject owner, float duration)
    {
        this.damage = damage;
        this.owner = owner;
        this.duration = duration;
        this.direction = transform.forward;
        initialized = true;
        Destroy(gameObject, duration); // 자동 삭제
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;
        if (other.gameObject == owner) return;
        if (other.gameObject.CompareTag("Enemy")) return;

        if (other.gameObject.CompareTag("Player"))
        {
            other.gameObject.GetComponent<PlayerStats>()?.TakeDamage(damage);
        }

        GameObject effect = Instantiate(hitParticle, gameObject.transform.position, gameObject.transform.rotation);
        Destroy(effect, 1f);
        Destroy(gameObject);
    }
}
