using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FirebaseManagerClient : MonoBehaviour
{
    public static FirebaseManagerClient Instance;

    [Header("Nickname UI")]
    public GameObject nicknamePanel;
    public TMP_InputField nicknameInput;
    public Button confirmButton;
    public Button resetButton;

    private const string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        string nick = PlayerPrefs.GetString("nickname", "");
        nicknamePanel.SetActive(string.IsNullOrEmpty(nick));

        confirmButton.onClick.AddListener(RegisterNickname);
        resetButton.onClick.AddListener(() => StartCoroutine(ResetAllData()));
    }

    void RegisterNickname()
    {
        string nickname = (nicknameInput?.text ?? "").Trim();
        if (string.IsNullOrEmpty(nickname)) return;

        string userId = PlayerPrefs.GetString("userId", System.Guid.NewGuid().ToString());
        PlayerPrefs.SetString("userId", userId);
        PlayerPrefs.SetString("nickname", nickname);
        PlayerPrefs.Save();

        string json = $@"
        {{
            ""nickname"": ""{nickname}"",
            ""summary"": {{
                ""topKills"": 0,
                ""totalKills"": 0,
                ""totalDeaths"": 0,
                ""totalDamage"": 0,
                ""totalMatches"": 0,
                ""totalWins"": 0,
                ""lastPlayed"": null
            }}
        }}";

        string url = $"{baseUrl}users/{userId}.json";
        StartCoroutine(Put(url, json));

        nicknamePanel.SetActive(false);
        Debug.Log($"[Firebase] 닉네임 및 summary 초기화 완료: {nickname} ({userId})");
    }

    IEnumerator Put(string url, string json)
    {
        using UnityWebRequest req = UnityWebRequest.Put(url, json);
        req.method = "PUT";
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("[Firebase] 업로드 성공");
        else
            Debug.LogError($"[Firebase] 업로드 실패: {req.error}");
    }

    public string GetUserId() => PlayerPrefs.GetString("userId", "");
    public string GetNickname() => PlayerPrefs.GetString("nickname", "");

    public IEnumerator ResetAllData()
    {
        Debug.Log("<color=yellow>[Reset] PlayerPrefs 및 Firebase 데이터 초기화 중...</color>");

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[Reset] PlayerPrefs 삭제 완료");

        string url = $"{baseUrl}.json";
        using (UnityWebRequest req = UnityWebRequest.Delete(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log("[Reset] Firebase 전체 데이터 삭제 완료");
            else
                Debug.LogError($"[Reset] Firebase 삭제 실패: {req.error}");
        }

        nicknamePanel.SetActive(true);
        if (nicknameInput) nicknameInput.text = "";
        Debug.Log("<color=green>[Reset] 초기화 완료. 닉네임 다시 입력 가능.</color>");
    }
}
