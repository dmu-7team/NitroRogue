using UnityEngine;
using System;
using System.Collections;
using Mirror;
/// <summary>
/// 플레이어 전용 스탯: 경험치, 레벨, 체력 증가 등
/// </summary>
public class PlayerStats : CharacterStats
{
    public override event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnExpChanged;
    public event Action<int> OnLevelChanged;
    public event Action<float> OnSpeedChanged;
    public event Action<float> OnPowerChanged;

    [SerializeField] private float currentExp = 0f;
    [SerializeField] private float expToLevelUp = 100f;
    [SerializeField] private int level = 1;

    public float healthPerLevel = 10f;
    public float damagePerLevel = 5f;
    public float speedPerLevel = 0.5f;

    public float CurrentExp => currentExp;
    public float ExpToLevelUp => expToLevelUp;
    public int Level => level;

    private float originalSpeed;
    private float originalDamage;

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetHealth(maxHealth, maxHealth);
        UIManager.Instance?.RegisterPlayer(this);

        originalSpeed = moveSpeed;
        originalDamage = attackDamage;
    }

    [Server]
    public override void SetHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    [Server]
    public override void TakeDamage(float damage, GameObject attacker = null)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void AddExp(float exp)
    {
        currentExp += exp;
        OnExpChanged?.Invoke(currentExp, expToLevelUp);

        while (currentExp >= expToLevelUp)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        expToLevelUp *= 1.5f;

        maxHealth += healthPerLevel;
        attackDamage += damagePerLevel;
        moveSpeed += speedPerLevel;

        SetHealth(maxHealth, maxHealth);

        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(moveSpeed);
        OnPowerChanged?.Invoke(attackDamage);

        UIManager.Instance?.UpdateLevelText(level);
        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    public void ApplyItemEffect(ItemData.ItemType itemType, float amount, float duration)
    {
        switch (itemType)
        {
            case ItemData.ItemType.SpeedBoost:
                ApplySpeedBoost(amount, duration);
                UIManager.Instance.ShowItemEffectMessage("Speed Up!", duration);
                break;
            case ItemData.ItemType.DamageBoost:
                ApplyDamageBoost(amount, duration);
                UIManager.Instance.ShowItemEffectMessage("Damage Up", duration);
                break;
        }
    }

    public void ApplySpeedBoost(float amount, float duration)
    {
        StartCoroutine(SpeedBoost(amount, duration));
    }

    public void ApplyDamageBoost(float amount, float duration)
    {
        StartCoroutine(DamageBoost(amount, duration));
    }

    private IEnumerator SpeedBoost(float amount, float duration)
    {
        moveSpeed = originalSpeed * amount;
        OnSpeedChanged?.Invoke(moveSpeed);
        yield return new WaitForSeconds(duration);
        moveSpeed = originalSpeed;
        OnSpeedChanged?.Invoke(moveSpeed);
    }

    private IEnumerator DamageBoost(float amount, float duration)
    {
        attackDamage = originalDamage * amount;
        OnPowerChanged?.Invoke(attackDamage);
        yield return new WaitForSeconds(duration);
        attackDamage = originalDamage;
        OnPowerChanged?.Invoke(attackDamage);
    }

    protected override void Die()
    {
        Debug.Log("플레이어 사망 처리");
        base.Die();
    }
}
