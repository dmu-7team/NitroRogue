using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 플레이어의 스탯 정보를 UI와 동기화하는 매니저
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("슬라이더")]
    public Slider healthSlider;
    public Slider expSlider;

    [Header("메시지")]
    public TextMeshProUGUI msgText;
    public TextMeshProUGUI chestMessageText;
    public TextMeshProUGUI levelUpMessageText;
    public TextMeshProUGUI itemEffectText;

    [Header("상태 텍스트")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI expText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI speedText;

    [Header("체력/경험치 바")]
    public Image healthBarImage;
    public Image expBarImage;

    private Coroutine chestMessageCoroutine;
    private Coroutine levelUpCoroutine;
    private Coroutine itemEffectCoroutine;
    private Coroutine expBarCoroutine;
    private Coroutine healthBarCoroutine;
    private Coroutine healthBlinkCoroutine;
    private Coroutine expLevelUpEffectCoroutine;

    private bool isBlinking = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("UIManager는 하나만 존재해야 합니다. 중복 제거됨.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        chestMessageText?.gameObject.SetActive(false);
        levelUpMessageText?.gameObject.SetActive(false);
        itemEffectText?.gameObject.SetActive(false);
    }

    public void RegisterPlayer(PlayerStats stats)
    {
        if (stats == null)
        {
            Debug.LogError("[UIManager] PlayerStats가 null입니다!");
            return;
        }

        Debug.Log("[UIManager] Player 연결 완료");

        stats.OnHealthChanged += UpdateHealthUI;
        stats.OnExpChanged += UpdateExpUI;
        stats.OnSpeedChanged += UpdateSpeedUI;
        stats.OnPowerChanged += UpdatePowerUI;

        UpdateHealthUI(stats.CurrentHealth, stats.MaxHealth);
        UpdateExpUI(stats.CurrentExp, stats.ExpToLevelUp);
        UpdateSpeedUI(stats.moveSpeed);
        UpdatePowerUI(stats.attackDamage);

        if (levelText != null)
        {
            levelText.text = $"Lv {stats.Level}";
        }
    }

    private void UpdateHealthUI(float current, float max)
    {
        if (healthBarImage != null)
        {
            float targetFill = current / max;
            if (healthBarCoroutine != null) StopCoroutine(healthBarCoroutine);
            healthBarCoroutine = StartCoroutine(AnimateHealthBar(targetFill));

            healthBarImage.color = (targetFill <= 0.3f) ? Color.red : Color.green;

            if (targetFill <= 0.1f)
            {
                if (!isBlinking)
                {
                    isBlinking = true;
                    healthBlinkCoroutine = StartCoroutine(BlinkHealthBar());
                }
            }
            else
            {
                if (isBlinking)
                {
                    isBlinking = false;
                    if (healthBlinkCoroutine != null) StopCoroutine(healthBlinkCoroutine);
                    healthBarImage.enabled = true;
                }
            }
        }

        if (healthText != null)
        {
            healthText.text = $"HP {current:F0} / {max:F0}";
        }
    }

    private void UpdateExpUI(float current, float toLevelUp)
    {
        if (expBarImage != null)
        {
            float targetFill = current / toLevelUp;
            if (expBarCoroutine != null) StopCoroutine(expBarCoroutine);
            expBarCoroutine = StartCoroutine(AnimateExpBar(targetFill));
        }

        if (expText != null)
        {
            expText.text = $"EXP {current:F0} / {toLevelUp:F0}";
        }
    }

    private void UpdatePowerUI(float current)
    {
        if (powerText != null)
        {
            powerText.text = $"Power {current:F0}";
        }
    }

    private void UpdateSpeedUI(float current)
    {
        if (speedText != null)
        {
            speedText.text = $"Speed {current:F1}";
        }
    }

    private IEnumerator AnimateHealthBar(float targetFill)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        float startFill = healthBarImage.fillAmount;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            healthBarImage.fillAmount = Mathf.Lerp(startFill, targetFill, elapsed / duration);
            yield return null;
        }

        healthBarImage.fillAmount = targetFill;
    }

    private IEnumerator AnimateExpBar(float targetFill)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        float startFill = expBarImage.fillAmount;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            expBarImage.fillAmount = Mathf.Lerp(startFill, targetFill, elapsed / duration);
            yield return null;
        }

        expBarImage.fillAmount = targetFill;
    }

    private IEnumerator BlinkHealthBar()
    {
        while (true)
        {
            healthBarImage.enabled = !healthBarImage.enabled;
            yield return new WaitForSeconds(0.3f);
        }
    }

    public void PlayExpLevelUpEffect()
    {
        if (expBarImage == null) return;

        if (expLevelUpEffectCoroutine != null) StopCoroutine(expLevelUpEffectCoroutine);
        expLevelUpEffectCoroutine = StartCoroutine(ExpLevelUpEffect());
    }

    private IEnumerator ExpLevelUpEffect()
    {
        Color originalColor = expBarImage.color;
        Color highlightColor = Color.yellow;

        float flashDuration = 0.2f;
        int flashCount = 3;

        for (int i = 0; i < flashCount; i++)
        {
            expBarImage.color = highlightColor;
            yield return new WaitForSeconds(flashDuration);
            expBarImage.color = originalColor;
            yield return new WaitForSeconds(flashDuration);
        }

        expBarImage.color = originalColor;
    }

    public void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Lv {level}";
        }
    }

    public void ShowChestMessage(string message, float duration = 2f)
    {
        if (chestMessageCoroutine != null) StopCoroutine(chestMessageCoroutine);
        chestMessageCoroutine = StartCoroutine(ShowTempMessage(chestMessageText, message, duration));
    }

    public void ShowLevelUpMessage(string message, float duration = 2f)
    {
        if (levelUpCoroutine != null) StopCoroutine(levelUpCoroutine);
        levelUpCoroutine = StartCoroutine(ShowTempMessage(levelUpMessageText, message, duration));
    }

    public void ShowItemEffectMessage(string message, float duration = 2f)
    {
        if (itemEffectCoroutine != null) StopCoroutine(itemEffectCoroutine);
        itemEffectCoroutine = StartCoroutine(ShowTempMessage(itemEffectText, message, duration));
    }

    private IEnumerator ShowTempMessage(TextMeshProUGUI target, string message, float duration)
    {
        if (target == null) yield break;

        target.text = message;
        target.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        target.gameObject.SetActive(false);
    }
}
