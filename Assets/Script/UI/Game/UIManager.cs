using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Mirror;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    private PlayerStats bound;
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
    public bool IsModalUIMode { get; private set; }  // ★ 전역 플래그
    [Header("결과 패널")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;
    public GameObject lobbyPanel;
    [Header("=== HUD 그룹 ===")]
    [SerializeField] private GameObject inGameHUDRoot;     // 플레이어 HUD 전체 부모
    [SerializeField] private GameObject spectatorHUDRoot;  // 관전자 HUD 부모
    [Header("Return Buttons (assign both)")]
    [SerializeField] private GameObject resultCanvasRoot; // ← paenlcanvas 드래그
    [SerializeField] private Button victoryReturnButton; // 승리 패널의 '처음으로' 버튼
    [SerializeField] private Button defeatReturnButton;  // 패배 패널의 '처음으로' 버튼
    public void OnClickReturnToMain()
    {
        // 로컬에서 메인 메뉴 UI 다시 띄우기
        RoomUIManager.Instance?.ShowMainMenu();
        IsModalUIMode = false;            // ★ 모달 해제
        // 현재 플레이어 캐릭터 제거 (선택)
        if (NetworkClient.isConnected && NetworkClient.localPlayer != null)
        {
            NetworkClient.localPlayer.connectionToServer?.Disconnect();
        }
    }

    public void ShowSpectatorPanel(bool on)
    {
        if (resultCanvasRoot && !resultCanvasRoot.activeSelf) resultCanvasRoot.SetActive(true); // ★
        if (spectatorHUDRoot != null) spectatorHUDRoot.SetActive(on);

        // 관전 들어갈 때 일반 HUD를 끄고 싶으면 아래 추가
        if (inGameHUDRoot != null) inGameHUDRoot.SetActive(!on);

        Debug.Log($"[UIManager] spectatorHUDRoot {(on ? "ON" : "OFF")}");
    }
    public void EnterGameplayHUD()
    {
        if (inGameHUDRoot) inGameHUDRoot.SetActive(true);
        if (spectatorHUDRoot) spectatorHUDRoot.SetActive(false);
    }

    public void EnterSpectatorHUD()
    {
        if (inGameHUDRoot) inGameHUDRoot.SetActive(false);
        if (spectatorHUDRoot) spectatorHUDRoot.SetActive(true);
    }
    private void EnterModal()
    {
        IsModalUIMode = true;
        if (inGameHUDRoot) inGameHUDRoot.SetActive(false);
        if (spectatorHUDRoot) spectatorHUDRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }
    public void ShowVictoryPanel()
    {
        if (resultCanvasRoot && !resultCanvasRoot.activeSelf) resultCanvasRoot.SetActive(true); // ★
        if (victoryPanel) victoryPanel.SetActive(true);
        if (defeatPanel) defeatPanel.SetActive(false);

        EnterModal(); // ★ 패배 패널과 동일하게 모달 처리
    }

    public void ShowDefeatPanel()
    {
        if (resultCanvasRoot && !resultCanvasRoot.activeSelf) resultCanvasRoot.SetActive(true); // ★
        if (defeatPanel) defeatPanel.SetActive(true);
        IsModalUIMode = true;
        if (spectatorHUDRoot) spectatorHUDRoot.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        if (inGameHUDRoot) inGameHUDRoot.SetActive(false);

        EnterModal(); // ★ 패배 패널과 동일하게 모달 처리
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        

    }


    private void Start()
    {
        chestMessageText?.gameObject.SetActive(false);
        levelUpMessageText?.gameObject.SetActive(false);
        itemEffectText?.gameObject.SetActive(false);
        RoomUIManager.Instance?.ShowMainMenu();
    }

    public void RegisterPlayer(PlayerStats stats)
    {
        if (stats == null) return;
        if (bound == stats) return;
        if (bound != null) UnregisterPlayer(bound);
        bound = stats;

        Debug.Log("[UIManager] Player 연결 완료");

        // 이벤트 구독 확장 (체력/경험/레벨도)
        bound.OnSpeedChanged += UpdateSpeedUI;
        bound.OnPowerChanged += UpdatePowerUI;
        bound.OnHealthChanged += UpdateHealthUI;
        bound.OnExpChanged += UpdateExpUI;
        bound.OnLevelChanged += UpdateLevelUI;

        // 즉시 반영
        UpdateSpeedUI(bound.MoveSpeed);
        UpdatePowerUI(bound.AttackDamage);
        UpdateLevelUI(bound.Level);
        UpdateHealthUI(bound.CurrentHealth, (bound as CharacterStats)?.MaxHealth ?? 100f);
        UpdateExpUI(bound.CurrentExp, bound.ExpToLevelUp);

        EnterGameplayHUD();
    }

    public void UnregisterPlayer(PlayerStats stats)
    {
        if (stats == null) return;
        if (bound != null && stats != bound) return;

        try
        {
            bound.OnSpeedChanged -= UpdateSpeedUI;
            bound.OnPowerChanged -= UpdatePowerUI;
            bound.OnHealthChanged -= UpdateHealthUI;
            bound.OnExpChanged -= UpdateExpUI;
            bound.OnLevelChanged -= UpdateLevelUI;
        }
        catch { }

        bound = null;
        // 값도 같이 지움(잔재 방지)
        ClearHUDValues();
    }

    private void ClearHUDValues()
    {
        if (healthBarImage) healthBarImage.fillAmount = 0f;
        if (expBarImage) expBarImage.fillAmount = 0f;
        if (healthText) healthText.text = "- / -";
        if (expText) expText.text = "0 / 0";
        if (levelText) levelText.text = "Lv 0";
        if (powerText) powerText.text = "파워 0";
        if (speedText) speedText.text = "스피드 0.0";
        if (ammoText) ammoText.text = "";
    }

    private void UpdateHealthUI(float cur, float max)
    {
        if (healthBarImage) healthBarImage.fillAmount = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
        if (healthText) healthText.text = $"{Mathf.RoundToInt(cur)}/{Mathf.RoundToInt(max)}";
    }

    private void UpdateExpUI(float cur, float toLv)
    {
        if (expBarImage) expBarImage.fillAmount = toLv > 0 ? Mathf.Clamp01(cur / toLv) : 0f;
        if (expText) expText.text = $"{Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(toLv)}";
    }

    private void UpdateLevelUI(int lv)
    {
        if (levelText) levelText.text = $"Lv {lv}";
    }

    
    public void ResetResultPanels()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
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
    // 필드
    private bool swRunning = false;
    private float swStart;
    private float swPausedAccum = 0f;
    [SerializeField] private TextMeshProUGUI stopwatchText;

    // 시작/일시정지/재개/리셋
    public void StartStopwatchLocal()
    {
        swPausedAccum = 0f;
        swStart = Time.time;
        swRunning = true;
    }

    public void PauseStopwatchLocal()
    {
        if (!swRunning) return;
        swPausedAccum += (Time.time - swStart);
        swRunning = false;
    }

    public void ResumeStopwatchLocal()
    {
        if (swRunning) return;
        swStart = Time.time;
        swRunning = true;
    }

    public void ResetStopwatchLocal()
    {
        swRunning = false;
        swPausedAccum = 0f;
        UpdateStopwatchText(0);
    }

    // Update에서 호출
    void Update()
    {
        if (swRunning)
        {
            float elapsed = (Time.time - swStart) + swPausedAccum;
            UpdateStopwatchText(elapsed);
        }
    }

    private void UpdateStopwatchText(double t)
    {
        int minutes = (int)(t / 60);
        int seconds = (int)(t % 60);
        int centi = (int)((t - Mathf.Floor((float)t)) * 100);
        if (stopwatchText != null)
            stopwatchText.text = $"{minutes:00}:{seconds:00}.{centi:00}";
    }
    public void ReturnToMainAndLeaveRoom()
    {
        // 1) 지금 클릭된 버튼이 들어있는 패널을 직접 찾아 끄기
        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (sel != null)
        {
            var panel = sel.transform.GetComponentInParent<Canvas>(true);
            if (panel) panel.gameObject.SetActive(false);
            else sel.transform.root.gameObject.SetActive(false);
        }
        if (NetworkClient.isConnected && NetworkClient.localPlayer != null)
        {
            NetworkClient.localPlayer.connectionToServer?.Disconnect();
        }
        // 2) 모달 해제 (로비에서는 마우스 필요)
        IsModalUIMode = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3) 기존 RoomUIManager의 방나가기 로직 호출
        if (RoomUIManager.Instance != null)
        {
            RoomUIManager.Instance.OnLeaveRoomButtonClicked();
        }
        else
        {
            // 혹시 RoomUIManager가 씬에 없을 때를 위한 안전장치(선택)
            NetworkClient.Disconnect();
            RoomListUI.matchIdToJoin = "";
            RoomListUI.enableAutoJoin = false;
            RoomListUI.triedAutoConnect = false;
        }
        if (lobbyPanel) lobbyPanel.SetActive(true);
        Debug.Log("[UI] ReturnToMainAndLeaveRoom 완료");
    }
  
    public void ResetAllUI()
    {
        // 바인딩 해제 (전판 이벤트 구독 끊기)
        if (bound != null)
        {
            UnregisterPlayer(bound);
            bound = null;
        }

        // 체력/경험치/레벨/스탯
        if (healthBarImage) healthBarImage.fillAmount = 0;
        if (expBarImage) expBarImage.fillAmount = 0;

        if (healthText) healthText.text = "0 / 0";
        if (expText) expText.text = "0 / 0";
        if (levelText) levelText.text = "Lv 0";
        if (powerText) powerText.text = "파워 0";
        if (speedText) speedText.text = "스피드 0.0";

        // 무기 HUD
        if (ammoText) ammoText.text = "";
        if (gunIcon) gunIcon.sprite = null;
        if (crosshair) crosshair.SetActive(false);
        if (scopeOverlay) scopeOverlay.SetActive(false);

        // 메시지/패널/모달
        if (msgText) msgText.text = "";
        chestMessageText?.gameObject.SetActive(false);
        levelUpMessageText?.gameObject.SetActive(false);
        itemEffectText?.gameObject.SetActive(false);

        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
        IsModalUIMode = false;

        // HUD 그룹(상황에 맞게)
        if (inGameHUDRoot) inGameHUDRoot.SetActive(false);
        if (spectatorHUDRoot) spectatorHUDRoot.SetActive(false);

        // ★ 팀 리스트/네임태그 전부 제거
        //var teamUI = TeamStatusUIManager.Instance
        //           ?? FindFirstObjectByType<TeamStatusUIManager>(FindObjectsInactive.Include);
        //teamUI?.ClearAll();

        Debug.Log("[UIManager] ResetAllUI 완료");
    }

}
