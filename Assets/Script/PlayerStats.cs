using UnityEngine;
using System;
using System.Collections;
using Mirror;

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

    // 🟢 자식에서 직접 동기화
    [SyncVar(hook = nameof(OnMoveSpeedChanged))] private float syncedMoveSpeed = 5f;
    [SyncVar(hook = nameof(OnAttackDamageChanged))] private float syncedAttackDamage = 10f;

    private float originalSpeed;
    private float originalDamage;

    public float CurrentExp => currentExp;
    public float ExpToLevelUp => expToLevelUp;
    public int Level => level;

    // 🟢 부모의 속성 override
    public override float MoveSpeed => syncedMoveSpeed;
    public override float AttackDamage => syncedAttackDamage;

    void OnMoveSpeedChanged(float oldVal, float newVal) => OnSpeedChanged?.Invoke(newVal);
    void OnAttackDamageChanged(float oldVal, float newVal) => OnPowerChanged?.Invoke(newVal);

    public override void OnStartClient()
    {
        base.OnStartClient();
        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetHealth(maxHealth, maxHealth);
        UIManager.Instance?.RegisterPlayer(this);

        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;
    }

#if UNITY_EDITOR
    new private void OnValidate()
    {
        originalSpeed = syncedMoveSpeed;
        originalDamage = syncedAttackDamage;
    }
#endif

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
            Die();
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
            LevelUp();
    }

    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        expToLevelUp *= 1.5f;

        maxHealth += healthPerLevel;
        syncedAttackDamage += damagePerLevel;
        syncedMoveSpeed += speedPerLevel;

        SetHealth(maxHealth, maxHealth);

        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
        OnPowerChanged?.Invoke(syncedAttackDamage);

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
        syncedMoveSpeed = originalSpeed * amount;
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
        yield return new WaitForSeconds(duration);
        syncedMoveSpeed = originalSpeed;
        OnSpeedChanged?.Invoke(syncedMoveSpeed);
    }

    private IEnumerator DamageBoost(float amount, float duration)
    {
        syncedAttackDamage = originalDamage * amount;
        OnPowerChanged?.Invoke(syncedAttackDamage);
        yield return new WaitForSeconds(duration);
        syncedAttackDamage = originalDamage;
        OnPowerChanged?.Invoke(syncedAttackDamage);
    }

    protected override void Die()
    {
        Debug.Log("플레이어 사망 처리");
        base.Die();
    }
}
