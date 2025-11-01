using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusRow : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text levelText;

    [Header("HP Bar (Image Type=Filled, Horizontal)")]
    [SerializeField] Image hpFill;
    [SerializeField] float lerpSpeed = 8f;

    private PlayerStats bound;
    private float targetFill = 1f;

    public void Bind(PlayerStats p)
    {
        bound = p;
        p.OnLevelChanged += OnLevel;
        p.OnHealthChanged += OnHp;

        // ★ 닉네임 변경 이벤트 구독
        p.OnNicknameChangedEvt += OnName;

        // 초기 표시
        OnName(p.Nickname);
        OnLevel(p.Level);
        OnHp(p.CurrentHealth, p.MaxHealth);
    }

    void OnDestroy()
    {
        if (bound == null) return;
        bound.OnLevelChanged -= OnLevel;
        bound.OnHealthChanged -= OnHp;
        bound.OnNicknameChangedEvt -= OnName;     // ★ 해제
    }

    void Update()
    {
        if (hpFill)
            hpFill.fillAmount = Mathf.Lerp(hpFill.fillAmount, targetFill, Time.deltaTime * lerpSpeed);
    }

    void OnName(string nick) => nameText.text = string.IsNullOrEmpty(nick) ? "-" : nick;
    void OnLevel(int v) => levelText.text = $"LV.{v}";
    void OnHp(float hp, float max) => targetFill = (max <= 0f) ? 0f : hp / max;
}
