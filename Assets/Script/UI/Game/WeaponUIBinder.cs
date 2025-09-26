using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무기 컨트롤러 이벤트만 구독해서 HUD 반영.
/// </summary>
public class WeaponUIBinder : MonoBehaviour
{
    private WeaponControllerClient weapon;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private GameObject scopeOverlay;
    [SerializeField] private GameObject crosshair;

    void OnEnable()
    {
        PlayerStats.Spawned += OnPlayerSpawned;
        PlayerStats.Despawned += OnPlayerDespawned;
        Bind(weapon);
    }

    void OnDisable()
    {
        PlayerStats.Spawned -= OnPlayerSpawned;
        PlayerStats.Despawned -= OnPlayerDespawned;
        Unbind();
    }
    private void OnPlayerSpawned(PlayerStats stats)
    {
        if (stats.isLocalPlayer) Bind(GetWeapon(stats));
    }

    private void OnPlayerDespawned(PlayerStats stats)
    {
        if (!stats.isLocalPlayer) return;
        Unbind();
        ClearUI();
    }


    public void Bind(WeaponControllerClient newWeapon)
    {
        if (weapon == newWeapon || !newWeapon) return;
        Unbind();

        weapon = newWeapon;
        weapon.AmmoChanged += OnAmmo;
        weapon.ScopedChanged += OnScoped;
        weapon.WeaponChanged += OnWeapon;
        weapon.ReloadingChanged += OnReloading;

        // ★ 초기 동기화: 현재 상태를 즉시 한 번 받기
        weapon.EmitAll();

        if (weaponIcon && weapon.Config) weaponIcon.sprite = weapon.Config.icon;
    }

    public void Unbind()
    {
        if (!weapon) return;
        weapon.AmmoChanged -= OnAmmo;
        weapon.ScopedChanged -= OnScoped;
        weapon.WeaponChanged -= OnWeapon;
        weapon.ReloadingChanged -= OnReloading;
        weapon = null;
    }

    private void ClearUI()
    {
        if (ammoText) ammoText.text = "- / -";
        if (weaponIcon) weaponIcon.sprite = null;
        if (scopeOverlay) scopeOverlay.SetActive(false);
        if (crosshair) crosshair.SetActive(true);
    }

    void OnAmmo(int cur, int max) { if (ammoText) ammoText.text = $"{cur} / {max}"; }
    void OnScoped(bool on) { if (scopeOverlay) scopeOverlay.SetActive(on); if (crosshair) crosshair.SetActive(!on); }
    void OnWeapon(int id) { if (weaponIcon && weapon && weapon.Config) weaponIcon.sprite = weapon.Config.icon; }
    void OnReloading(bool isReloading) { /* 인디케이터 토글 */ }

    private WeaponControllerClient GetWeapon(PlayerStats stats)
    {
        if (!stats) return null;
        WeaponControllerClient weapon = stats.gameObject.GetComponent<WeaponControllerClient>();
        return weapon;
    }
}
