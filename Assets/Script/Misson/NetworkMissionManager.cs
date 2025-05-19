using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class NetworkMissionManager : NetworkBehaviour
{
    public static NetworkMissionManager Instance;

    [Header("�̼� UI")]
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
            // ���� �� �⺻ �ؽ�Ʈ �ʱ�ȭ
            missionText.text = "�� ���׹� 1���� �̻� ��������";
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
            // ���� �̼� �̿Ϸ� ������ ��쿡�� �ؽ�Ʈ ����
            missionText.text = "�� ���׹� 1���� �̻� ��������";
        }
    }

    private void ShowMissionCompletedUI()
    {
        if (missionText != null)
        {
            missionText.text = "�� ���׹� 1���� �̻� ��������";
        }
    }
}
