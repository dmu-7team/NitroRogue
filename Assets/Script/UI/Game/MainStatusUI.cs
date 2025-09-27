using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainStatusUI : MonoBehaviour
{
    private PlayerStats stats;

    [Header("UI References")]
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image expFill;
    [SerializeField] private TextMeshProUGUI expText;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float barLerpDuration = 0.5f;
    [SerializeField, Min(0f)] private float blinkInterval = 0.3f;

    [Header("Health Visuals")]
    [SerializeField, Range(0f, 1f)] private float lowHpThreshold = 0.3f;
    [SerializeField, Range(0f, 1f)] private float criticalHpThreshold = 0.1f;
    [SerializeField] private Color hpNormalColor = Color.red;
    [SerializeField] private Color hpLowColor = Color.black;

    [Header("EXP Level-Up Effect")]
    [SerializeField] private Color expFlashColor = Color.yellow;
    [SerializeField, Min(0f)] private float expFlashDuration = 0.2f;
    [SerializeField, Min(1)] private int expFlashCount = 3;

    // 내부 상태 (레벨+경험치 한 줄 표기)
    private int currentLevel;
    private float currentExp, maxExp;

    // 코루틴 핸들
    private Coroutine healthLerpCo;
    private Coroutine expLerpCo;
    private Coroutine hpBlinkCo;
    private Coroutine expLevelUpCo;

    private bool isBlinking;

    void OnEnable()
    {
        PlayerStats.Spawned += OnPlayerSpawned;
        PlayerStats.Despawned += OnPlayerDespawned;
        var localObj = NetworkClient.localPlayer;
        if (localObj)
        {
            var stats = localObj.GetComponent<PlayerStats>();
            if (stats) Bind(stats);
        }
    }

    void OnDisable()
    {
        PlayerStats.Spawned -= OnPlayerSpawned;
        PlayerStats.Despawned -= OnPlayerDespawned;
        StopAllVisualCoroutines();
        Unbind();
    }

    private void OnPlayerSpawned(PlayerStats s)
    {
        if (s != null && s.isLocalPlayer)
        {
            Bind(s);
        }
    }

    private void OnPlayerDespawned(PlayerStats s)
    {
        if (s != null && s == stats)
        {
            Unbind();
            StopAllVisualCoroutines();
            ClearUI();
        }
    }

    public void Bind(PlayerStats newTarget)
    {
        // 재활성화 시 null 타겟으로 초기화되는 경우를 방지
        if (newTarget == null)
        {
            if (stats != null) ClearUI();
            return;
        }

        // 이미 같은 대상을 추적 중이면 아무것도 안함
        if (stats == newTarget) return;

        Unbind();

        stats = newTarget;
        stats.OnHealthChanged += OnHealth;
        stats.OnExpChanged += OnExp;
        stats.OnLevelChanged += OnLevel;

        // 초기값 즉시 반영
        stats.EmitAll();
    }

    public void Unbind()
    {
        if (!stats) return;

        stats.OnHealthChanged -= OnHealth;
        stats.OnExpChanged -= OnExp;
        stats.OnLevelChanged -= OnLevel;

        stats = null;
    }

    // ========= Handlers =========
    private void OnHealth(float cur, float max)
    {
        // 게이지 애니메이션
        float target = (max > 0f) ? cur / max : 0f;
        if (healthFill)
        {
            if (healthLerpCo != null) StopCoroutine(healthLerpCo);
            healthLerpCo = StartCoroutine(AnimateFill(healthFill, target, barLerpDuration));
        }

        // 텍스트
        if (healthText) healthText.text = $"{Mathf.FloorToInt(cur)} / {Mathf.FloorToInt(max)}";

        // 색상/블링크
        ApplyHealthVisuals(target);
    }

    private void OnExp(float cur, float max)
    {
        currentExp = cur;
        maxExp = max;

        float target = (max > 0f) ? cur / max : 0f;
        if (expFill)
        {
            if (expLerpCo != null) StopCoroutine(expLerpCo);
            expLerpCo = StartCoroutine(AnimateFill(expFill, target, barLerpDuration));
        }

        UpdateLevelExpUI(); // "Lv. N  cur / max"
    }

    private void OnLevel(int lv)
    {
        // 레벨업 이펙트
        if (lv > currentLevel)
            PlayExpLevelUpEffect();

        currentLevel = lv;
        UpdateLevelExpUI();
    }

    // ========= Visual Helpers =========
    private void UpdateLevelExpUI()
    {
        if (expText)
            expText.text = $"Lv. {currentLevel}  {Mathf.FloorToInt(currentExp)} / {Mathf.FloorToInt(maxExp)}";
    }

    private void ApplyHealthVisuals(float hpFill)
    {
        // 색상
        if (healthFill)
            healthFill.color = (hpFill <= lowHpThreshold) ? hpLowColor : hpNormalColor;

        // 블링크
        if (hpFill <= criticalHpThreshold)
        {
            if (!isBlinking && healthFill)
            {
                isBlinking = true;
                hpBlinkCo = StartCoroutine(BlinkImage(healthFill, blinkInterval));
            }
        }
        else
        {
            if (isBlinking)
            {
                isBlinking = false;
                if (hpBlinkCo != null) StopCoroutine(hpBlinkCo);
                if (healthFill) healthFill.enabled = true; // 보이도록 복구
            }
        }
    }

    private void PlayExpLevelUpEffect()
    {
        if (!expFill) return;

        if (expLevelUpCo != null) StopCoroutine(expLevelUpCo);
        expLevelUpCo = StartCoroutine(FlashImageColor(expFill, expFlashColor, expFlashDuration, expFlashCount));
    }

    // ========= Coroutines =========
    private IEnumerator AnimateFill(Image img, float target, float duration)
    {
        float start = img.fillAmount;
        if (Mathf.Approximately(start, target) || duration <= 0f)
        {
            img.fillAmount = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            img.fillAmount = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        img.fillAmount = target;
    }

    private IEnumerator BlinkImage(Graphic g, float interval)
    {
        while (true)
        {
            g.enabled = !g.enabled;
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator FlashImageColor(Graphic g, Color flashColor, float flashDuration, int count)
    {
        Color original = g.color;

        for (int i = 0; i < count; i++)
        {
            g.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            g.color = original;
            yield return new WaitForSeconds(flashDuration);
        }

        g.color = original;
    }

    private void StopAllVisualCoroutines()
    {
        if (healthLerpCo != null) StopCoroutine(healthLerpCo);
        if (expLerpCo != null) StopCoroutine(expLerpCo);
        if (hpBlinkCo != null) StopCoroutine(hpBlinkCo);
        if (expLevelUpCo != null) StopCoroutine(expLevelUpCo);

        healthLerpCo = null;
        expLerpCo = null;
        hpBlinkCo = null;
        expLevelUpCo = null;

        isBlinking = false;
        if (healthFill) healthFill.enabled = true;
    }

    // ========= Reset =========
    private void ClearUI()
    {
        if (healthFill) { healthFill.fillAmount = 0f; healthFill.color = hpNormalColor; healthFill.enabled = true; }
        if (expFill) { expFill.fillAmount = 0f; }
        if (healthText) healthText.text = "- / -";
        if (expText) expText.text = "Lv. -  - / -";

        currentLevel = 0;
        currentExp = 0;
        maxExp = 0;
    }
}
