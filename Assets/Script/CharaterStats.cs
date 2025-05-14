using UnityEngine;
using Mirror;
using System;

public class CharacterStats : NetworkBehaviour
{
    [SyncVar] public float currentHealth;
    [SyncVar] public float maxHealth = 100f;
    [SyncVar] public float attackDamage = 10f;
    [SyncVar] public float moveSpeed = 5f;

    public virtual float CurrentHealth => currentHealth;
    public virtual float MaxHealth => maxHealth;
    public virtual float AttackDamage => attackDamage;
    public virtual float MoveSpeed => moveSpeed;

    public virtual event Action<float, float> OnHealthChanged;

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
    }

    [Server]
    public virtual void SetHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    [Server]
    public virtual void TakeDamage(float damage, GameObject attacker = null)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    [Server]
    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} »ç¸Á");
    }
}
