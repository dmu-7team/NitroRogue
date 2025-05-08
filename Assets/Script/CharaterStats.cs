using UnityEngine;
using System;

public class CharacterStats : MonoBehaviour
{
    // ü�� ���� �� UIManager�� ������ �� �ִ� �̺�Ʈ
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

            //��� �������鼭 �������� ������ ������� ������ �ϸ� ������
            attacker?.GetComponent<PlayerStats>()?.AddExp(10);
        }
    }
    

    // ü�� ������ ó���ϰ� �̺�Ʈ�� �߻���Ŵ
    public void SetHealth(float current, float max)
    {
        maxHealth = max;
        currentHealth = Mathf.Clamp(current, 0, max);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} ��� Ȯ���� ���");
    }
}
