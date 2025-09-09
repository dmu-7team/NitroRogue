using UnityEngine;
using Mirror;
using System;

public class CharacterStats : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCurHpSync))] protected float currentHealth;
    [SyncVar(hook = nameof(OnMaxHpSync))] protected float maxHealth = 100f;

    public virtual float AttackDamage => 0f;
    public virtual float MoveSpeed => 0f;

    public virtual float CurrentHealth => currentHealth;
    public virtual float MaxHealth => maxHealth;

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
    }

    void OnCurHpSync(float oldVal, float newVal) => NotifyDerived();
    void OnMaxHpSync(float oldVal, float newVal) => NotifyDerived();

    protected virtual void OnHealthSynced(float cur, float max) { /* 기본은 아무것도 안 함 */ }

    protected void NotifyDerived() => OnHealthSynced(currentHealth, maxHealth);

    [Server]
    public virtual void SetHealth(float current, float max)
    {
        max = Mathf.Max(1f, max);
        current = Mathf.Clamp(current, 0, max);
        maxHealth = max;
        currentHealth = current;
    }

    [Server]
    public virtual void TakeDamage(float damage, GameObject attacker = null)
    {
        currentHealth = Mathf.Clamp(currentHealth - Mathf.Abs(damage), 0, maxHealth);
        if (currentHealth <= 0) Die();
    }

    [Server]
    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망");
    }
}
