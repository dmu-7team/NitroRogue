using UnityEngine;

public enum WeaponType { DMR, SMG, AR, SG }
public enum FireMode { Auto, Semi, Burst, Shotgun }
public enum AimMode { Zoom, Scope }

[CreateAssetMenu(menuName = "FPS/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("Identity")]
    public int id;
    public string displayName;
    public Sprite icon;
    public Sprite resultIcon;

    [Header("Type & Modes")]
    public WeaponType type = WeaponType.AR;
    public FireMode fireMode = FireMode.Auto;
    public AimMode aimMode = AimMode.Zoom;

    [Header("Core Stats")]
    [Min(1)] public int magSize = 30;
    [Min(0f)] public float reloadTime = 2.5f;
    [Min(1)] public int rpm = 600;   // 발사 간격 = 60 / rpm

    [Header("Burst")]
    public int burstCount = 3;
    public float burstDelay = 0.1f;

    [Header("Shotgun")]
    public int pelletCount = 8;
    public float spreadDeg = 6f;

    [Header("Aim")]
    public float scopedFOV = 30f;

    [Header("Audio")]
    public AudioClip fireClip;
    public AudioClip reloadClip;
}
