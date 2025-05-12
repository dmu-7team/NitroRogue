using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// �÷��̾� ���� ����: ����ġ, ����, ü�� ���� ��
/// </summary>
public class PlayerStats : CharacterStats
{
    public event Action<float, float> OnExpChanged;     // ���� ����ġ, �ʿ� ����ġ ����
    public event Action<int> OnLevelChanged;            // ���� ���� �� ȣ�� (UI���� ��� ����)
    public event Action<float> OnSpeedChanged;
    public event Action<float> OnPowerChanged;

    [SerializeField] private float currentExp = 0f;
    [SerializeField] private float expToLevelUp = 100f;
    [SerializeField] private int level = 1;

    // ������ �� ���� ���� ��ġ
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

    // ����ġ�� �߰��ϰ�, ������ ������ �����ϸ� �ڵ����� ���
    public void AddExp(float exp)
    {
        currentExp += exp;
        OnExpChanged?.Invoke(currentExp, expToLevelUp);

        while (currentExp >= expToLevelUp)
        {
            LevelUp();
        }
    }

    // ������ ó��: ����ġ ����, ���� ����
    private void LevelUp()
    {
        currentExp -= expToLevelUp;
        level++;
        expToLevelUp *= 1.5f;

        // ���� ����
        maxHealth += healthPerLevel;
        attackDamage += damagePerLevel;
        moveSpeed += speedPerLevel;

        // ü���� �׻� Ǯ ȸ��
        SetHealth(maxHealth, maxHealth);

        // UI ����
        OnExpChanged?.Invoke(currentExp, expToLevelUp);
        OnLevelChanged?.Invoke(level);
        OnSpeedChanged?.Invoke(moveSpeed);
        OnPowerChanged?.Invoke(attackDamage);

        UIManager.Instance?.UpdateLevelText(level);
        Debug.Log($"���� ��! ���� ����: {level}");
    }

    protected override void Die()
    {
        Debug.Log("�÷��̾� ��� ó��");
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
