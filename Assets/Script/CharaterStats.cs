using UnityEngine;
using Mirror;
using System;

public class CharacterStats : NetworkBehaviour
{
    [SyncVar] public float currentHealth;
    [SyncVar] public float maxHealth = 100f;

    //  SyncVar 필드 제거
    // → 자식이 따로 관리
    // public float attackDamage;
    // public float moveSpeed;

    // 속성만 남기고 자식에서 override 하게 만들기
    public virtual float AttackDamage => 0f;
    public virtual float MoveSpeed => 0f;

    public virtual float CurrentHealth => currentHealth;
    public virtual float MaxHealth => maxHealth;

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
            Die();
    }

    [Server]
    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망");
    }
}
