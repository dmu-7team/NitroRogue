using UnityEngine;
using System;

public class CharacterStats : MonoBehaviour
{
    // 체력 변경 시 UIManager가 구독할 수 있는 이벤트
    public event Action<float, float> OnHealthChanged;

    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float currentHealth;
    public float attackDamage = 20f;
    public float moveSpeed = 5f;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    protected virtual void Start()
    {
        SetHealth(maxHealth, maxHealth);
    }

    public virtual void TakeDamage(float damage, GameObject attacker=null)
    {
        SetHealth(currentHealth - damage, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();

            //골드 떨어지면서 마지막에 공격한 사람한테 가도록 하면 좋을듯
            attacker?.GetComponent<PlayerStats>()?.AddExp(10);
        }
    }
    

    // 체력 변경을 처리하고 이벤트를 발생시킴
    public void SetHealth(float current, float max)
    {
        maxHealth = max;
        currentHealth = Mathf.Clamp(current, 0, max);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망 확실히 사망");
    }
}
