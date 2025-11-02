using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private TextMeshProUGUI killsText;

    public void SetValues(string nickname, Sprite weaponIcon, int kills)
    {
        nicknameText.text = nickname;
        killsText.text = kills.ToString();

        if (weaponIconImage)
        {
            weaponIconImage.sprite = weaponIcon;
            weaponIconImage.enabled = weaponIcon != null;
        }
    }
}