using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    // ���� ���� ��ư�� ����
    public void StartGame()
    {
        // ���� ���� �� �κ� ���� ������ ���� ���� �ü����� �̵�
        SceneManager.LoadScene("MainMenu");
    }

    // ȯ�漳�� ��ư�� ����
    public void OpenSettings()
    {
        Debug.Log("ȯ�漳�� ����");
        // ȯ�漳�� UI ǥ�ø� �ٷ�� �غ�����
    }

    // �� ���� ��ư�� ����
    public void OpenMyInfo()
    {
        Debug.Log("�� ���� ǥ��");
        // �� ���� ǥ�ø� �ٷ�� �غ�����
    }

    public void GoToTitleScene()
    {
        SceneManager.LoadScene("Title"); // �� �̸� ��Ȯ�� �Է�
    }

    public void Test()
    {
        SceneManager.LoadScene("WoojinScene"); // �� �̸� ��Ȯ�� �Է�
    }
}