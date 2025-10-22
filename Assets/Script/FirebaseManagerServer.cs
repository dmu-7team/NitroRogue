using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManagerServer : MonoBehaviour
{
    public static FirebaseManagerServer Instance;
    private const string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";

    void Awake()
    {
        Instance = this;
    }

    // === 매치 종료 시 서버에서 호출 ===
    public void UploadMatchResult(string userId, int kills, int deaths, int damage, bool isWin)
    {
        string matchId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

        // ① 개별 매치 기록 저장
        string matchJson = $@"
        {{
            ""kills"": {kills},
            ""deaths"": {deaths},
            ""damage"": {damage},
            ""result"": ""{(isWin ? "Win" : "Lose")}"",
            ""timestamp"": ""{DateTime.UtcNow:O}""
        }}";

        StartCoroutine(Put($"{baseUrl}users/{userId}/matches/{matchId}.json", matchJson));

        // ② 요약 통계 업데이트
        StartCoroutine(UpdateSummary(userId, kills, deaths, damage, isWin));
    }

    // === 기존 summary 불러와서 누적 ===
    private IEnumerator UpdateSummary(string userId, int kills, int deaths, int damage, bool isWin)
    {
        string summaryUrl = $"{baseUrl}users/{userId}/summary.json";

        // 기존 summary 가져오기
        UnityWebRequest getReq = UnityWebRequest.Get(summaryUrl);
        yield return getReq.SendWebRequest();

        int totalKills = kills;
        int totalDeaths = deaths;
        int totalDamage = damage;
        int totalMatches = 1;
        int totalWins = isWin ? 1 : 0;

        if (getReq.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(getReq.downloadHandler.text) && getReq.downloadHandler.text != "null")
        {
            try
            {
                var oldData = JsonUtility.FromJson<SummaryData>(getReq.downloadHandler.text);
                if (oldData != null)
                {
                    totalKills += oldData.totalKills;
                    totalDeaths += oldData.totalDeaths;
                    totalDamage += oldData.totalDamage;
                    totalMatches += oldData.totalMatches;
                    totalWins += oldData.totalWins;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Firebase] 기존 summary 파싱 실패: " + e.Message);
            }
        }

        string newJson = $@"
        {{
            ""totalKills"": {totalKills},
            ""totalDeaths"": {totalDeaths},
            ""totalDamage"": {totalDamage},
            ""totalMatches"": {totalMatches},
            ""totalWins"": {totalWins},
            ""lastPlayed"": ""{DateTime.UtcNow:O}""
        }}";

        StartCoroutine(Put(summaryUrl, newJson));
    }

    // === PUT 요청 공용 함수 ===
    private IEnumerator Put(string url, string json)
    {
        using UnityWebRequest req = UnityWebRequest.Put(url, json);
        req.method = "PUT";
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[Firebase] 업로드 성공: {url}");
        else
            Debug.LogError($"[Firebase] 업로드 실패: {req.error}");
    }

    [Serializable]
    private class SummaryData
    {
        public int totalKills;
        public int totalDeaths;
        public int totalDamage;
        public int totalMatches;
        public int totalWins;
    }
}
