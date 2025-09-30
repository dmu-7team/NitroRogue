using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class Submunition : NetworkBehaviour
{
    [Header("Nitro Stats")]
    [SerializeField] private float fuseTime = 1.5f;        //폭발까지 걸리는 시간
    [SerializeField] private float explosionRadius = 4f;   // 폭발반경
    [SerializeField] private float damage = 300f;          

    [Header("Effect and Sound")]
    [SerializeField] private GameObject explosionEffectPrefab; 
    [SerializeField] private AudioClip explosionSound;       

    [SyncVar]
    public GameObject owner; //스킬을 시전한 플레이어

    private AudioSource audioSource;
    private TrailRenderer trailRenderer;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        trailRenderer = GetComponent<TrailRenderer>();
    }

    
    public override void OnStartServer()
    {       
        Invoke(nameof(Explode), fuseTime);
    }

    [Server]
    private void Explode()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in colliders)
        {
            if (hit.TryGetComponent<EnemyBase>(out var enemy))
            {

                enemy.TakeDamage(damage, owner);
            }
        }
        RpcPlayExplosionEffects();
        NetworkServer.Destroy(gameObject);
    }

    
    [ClientRpc]
    private void RpcPlayExplosionEffects()
    {
        
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        
        if (audioSource != null && explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        
        if (trailRenderer != null)
        {
            trailRenderer.transform.SetParent(null); 
            Destroy(trailRenderer.gameObject, trailRenderer.time); 
        }
    }
}