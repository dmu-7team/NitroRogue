using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float transitionTime = 0.2f; // ✅ 유도 전환 시간 (초 기준)

    private Vector3 startDirection;
    private Vector3 targetDirection;
    private float speed;
    private bool isProjectile = false;
    private float elapsedTime = 0f;
    private bool initialized = false;

    public void Init(Vector3 fireDirection, float bulletSpeed, bool projectile)
    {
        startDirection = transform.forward;
        targetDirection = fireDirection.normalized;
        speed = bulletSpeed;
        isProjectile = projectile;
        initialized = true;

        if (isProjectile && TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.linearVelocity = targetDirection * speed;
        }
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!isProjectile && initialized)
        {
            float moveThisFrame = speed * Time.deltaTime;
            elapsedTime += Time.deltaTime;

            // ✅ 시간 기반 보간 (속도와 무관하게 일정 시간 내 꺾임)
            float t = Mathf.Clamp01(elapsedTime / transitionTime);
            Vector3 currentDir = Vector3.Slerp(startDirection, targetDirection, t);

            transform.position += currentDir * moveThisFrame;
            transform.rotation = Quaternion.LookRotation(currentDir);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isProjectile)
        {
            EnemyHealth enemy = collision.gameObject.GetComponent<EnemyHealth>();
            if (enemy != null)
                enemy.TakeDamage(1);

            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
