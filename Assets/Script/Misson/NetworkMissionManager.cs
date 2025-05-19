using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class NetworkMissionManager : NetworkBehaviour
{
    public static NetworkMissionManager Instance;

    [Header("미션 UI")]
    public TextMeshProUGUI missionText;

    [SyncVar(hook = nameof(OnMissionChanged))]
    private bool missionCompleted = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (isClient && missionText != null)
        {
            // 시작 시 기본 텍스트 초기화
            missionText.text = "□ 에네미 1마리 이상 잡으세요";
        }
    }

    public void CheckMissionProgress()
    {
        if (!isServer) return;

        if (!missionCompleted)
        {
            missionCompleted = true;
        }
    }

    private void OnMissionChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            ShowMissionCompletedUI();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (missionText == null) return;

        if (missionCompleted)
        {
            ShowMissionCompletedUI();
        }
        else
        {
            // 아직 미션 미완료 상태인 경우에도 텍스트 세팅
            missionText.text = "□ 에네미 1마리 이상 잡으세요";
        }
    }

    private void ShowMissionCompletedUI()
    {
        if (missionText != null)
        {
            missionText.text = "■ 에네미 1마리 이상 잡으세요";
        }
    }
}
