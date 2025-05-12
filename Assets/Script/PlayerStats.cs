using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 플레이어 전용 스탯: 경험치, 레벨, 체력 증가 등
/// </summary>
public class PlayerStats : CharacterStats
{
    public event Action<float, float> OnExpChanged;     // 현재 경험치, 필요 경험치 전달
    public event Action<int> OnLevelChanged;            // 레벨 변경 시 호출 (UI에서 사용 가능)
    public event Action<float> OnSpeedChanged;
    public event Action<float> OnPowerChanged;

    [SerializeField] private float currentExp = 0f;
    [SerializeField] private float expToLevelUp = 100f;
    [SerializeField] private int level = 1;

    // 레벨업 시 스탯 증가 수치
    public float healthPerLevel = 10f;
    public float damagePerLevel = 5f;
    public float speedPerLevel = 0.5f;

    public float CurrentExp => currentExp;
    public float ExpToLevelUp => expToLevelUp;
    public int Level => level;

    private bool isDead = false;
    private float originalSpeed;
    private float originalDamage;

    public override void Start()
    {
        base.Start();

        SetHealth(maxHealth, maxHealth);
        UIManager.Instance?.RegisterPlayer(this);

        originalSpeed = moveSpeed;
        originalDamage = attackDamage;
    }

    // 경험치를 추가하고, 레벨업 조건을 만족하면 자동으로 상승
    public void AddExp(float exp)
    {
        currentExp += exp;
        OnExpChanged?.Invoke(currentExp, expToLevelUp);

        while (currentExp >= expToLevelUp)
        {
            LevelUp();
        }
    }

    // 레벨업 처리: 경험치 차감, 스탯 증가
    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        expToLevelUp *= 1.5f;

        // 스탯 증가
        maxHealth += healthPerLevel;
        attackDamage += damagePerLevel;
        moveSpeed += speedPerLevel;

        // 체력은 항상 풀 회복
        SetHealth(maxHealth, maxHealth);

        // UI 갱신
        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(moveSpeed);
        OnPowerChanged?.Invoke(attackDamage);

        UIManager.Instance?.UpdateLevelText(level);
        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    protected override void Die()
    {
        Debug.Log("플레이어 사망 처리");
        base.Die();
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
}
