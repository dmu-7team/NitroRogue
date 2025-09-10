using UnityEngine;
using TMPro;

public class MissionUIListener : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI missionText;

    private void OnEnable()
    {
        NetworkMissionManager.OnTeamKillUpdated += HandleUpdate;
        if (missionText != null) missionText.text = "□ 에네미 1마리 이상 잡으세요";
    }
    private void OnDisable()
    {
        NetworkMissionManager.OnTeamKillUpdated -= HandleUpdate;
    }
    void HandleUpdate(int teamKill)
    {
        if (missionText == null) return;
        missionText.text = (teamKill >= 1)
            ? "■ 에네미 1마리 이상 잡으세요"
            : "□ 에네미 1마리 이상 잡으세요";
    }
}
