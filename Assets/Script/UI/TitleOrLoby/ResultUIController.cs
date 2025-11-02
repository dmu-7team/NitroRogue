using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms.Impl;
using Mirror;

public class ResultUIController : MonoBehaviour
{
    [SerializeField] private Transform scoreboardContainer;
    [SerializeField] private GameObject scoreboardRowPrefab;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("My Result UI")]
    [SerializeField] private Image myWeaponIconImage;
    [SerializeField] private TMP_Text myNicknameText;
    [SerializeField] private TMP_Text myKillCountText;
    [SerializeField] private TMP_Text mySurvivalTimeText;
    [SerializeField] private TMP_Text myLevelText;
    [SerializeField] private TMP_Text myTotalDamageText;

    [Header("Weapon Configs (UI 참조용)")]
    [SerializeField] private WeaponConfig[] weaponConfigs;

    private Dictionary<string, Sprite> weaponIconMap;

    void Awake()
    {
        weaponIconMap = new Dictionary<string, Sprite>();
        foreach (var cfg in weaponConfigs)
        {
            if (cfg != null && !weaponIconMap.ContainsKey(cfg.displayName))
                weaponIconMap.Add(cfg.displayName, cfg.resultIcon);
        }
    }

    void OnEnable()
    {
        MatchManager.OnMatchSummaryReady += HandleMatchSummary;
    }

    void OnDisable()
    {
        MatchManager.OnMatchSummaryReady -= HandleMatchSummary;
    }

    private void HandleMatchSummary(List<PlayerMatchRecord> records, bool isVictory)
    {
        // UIManager: 패널 열기 및 모달 세팅
        UIManager.Instance?.ShowScoreboard();

        resultText.text = isVictory ? "승리" : "패배";

        if (!scoreboardContainer || !scoreboardRowPrefab) return;

        // 내 userId 가져오기
        string localUserId = NetworkClient.localPlayer
            ? NetworkClient.localPlayer.GetComponent<PlayerStats>().UserId
            : string.Empty;

        // 기존 행 제거
        foreach (Transform child in scoreboardContainer)
            Destroy(child.gameObject);

        foreach (var r in records)
        {
            Debug.Log("웨폰이름: " + r.weaponName);
            var row = Instantiate(scoreboardRowPrefab, scoreboardContainer);
            var rowUI = row.GetComponent<ScoreboardRowUI>();
            if (rowUI != null)
                rowUI.SetValues(r.nickname, GetWeaponIcon(r.weaponName), r.kills);

            if (r.userId == localUserId)
            {
                if (myWeaponIconImage)
                {
                    myWeaponIconImage.sprite = GetWeaponIcon(r.weaponName);
                    myWeaponIconImage.enabled = GetWeaponIcon(r.weaponName) != null;
                }

                myNicknameText.text = r.nickname;
                myKillCountText.text = r.kills.ToString();
                mySurvivalTimeText.text = $"{Mathf.FloorToInt(r.survivalTime)}s";
                myLevelText.text = $"Lv. {r.level}";
                myTotalDamageText.text = r.damage.ToString("F0");
            }
        }
    }
    private Sprite GetWeaponIcon(string weaponName)
    {
        if (weaponIconMap.TryGetValue(weaponName, out var icon))
            return icon;
        return null;
    }
}
