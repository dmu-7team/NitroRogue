using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("게임 HUD 루트")]
    [SerializeField] public GameObject crosshair;  // HUD 크로스헤어
    [SerializeField] public TextMeshProUGUI ammoText;  // HUD 총알개수
    [SerializeField] public GameObject scopeOverlay;  // HUD 스코프
    [SerializeField] public Image gunIcon;  // 총 아이콘

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

    [Header("결과 패널")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    public void ShowVictoryPanel()
    {
        if (victoryPanel) victoryPanel.SetActive(true);
        if (defeatPanel) defeatPanel.SetActive(false);
    }

    public void ShowDefeatPanel()
    {
            if (defeatPanel) defeatPanel.SetActive(true);
            if (victoryPanel) victoryPanel.SetActive(false);
        }
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

        stats.OnSpeedChanged += UpdateSpeedUI;
        stats.OnPowerChanged += UpdatePowerUI;

        UpdateSpeedUI(stats.MoveSpeed);
        UpdatePowerUI(stats.AttackDamage);

        if (levelText != null)
        {
            levelText.text = $"Lv {stats.Level}";
        }
    }

    private void UpdatePowerUI(float current)
    {
        if (powerText != null)
        {
            powerText.text = $"파워 {current:F0}";
        }
    }

    private void UpdateSpeedUI(float current)
    {
        if (speedText != null)
        {
            speedText.text = $"스피드 {current:F1}";
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

    public void SetGunIcon(Sprite gunIcon)
    {
        this.gunIcon.sprite = gunIcon;
    }

}
