using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버 전용: 경기별 통계 업로드 + 누적(summary) 갱신
/// </summary>
public class FirebaseManagerServer : MonoBehaviour
{
    public static FirebaseManagerServer Instance;
    private const string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";

    void Awake() => Instance = this;

    /// <summary>
    /// 한 경기의 결과 업로드 및 summary 누적 갱신
    /// </summary>
    public void UploadMatchResult(string userId, int kills, int deaths, int damage, bool isWin)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[Firebase] userId가 없음");
            return;
        }

        string matchId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

        // --- (1) 개별 매치 기록 ---
        var matchData = new
        {
            kills,
            deaths,
            damage,
            result = isWin ? "Win" : "Lose",
            timestamp = DateTime.UtcNow.ToString("o")
        };

        string matchJson = JsonUtility.ToJson(matchData);
        string matchUrl = $"{baseUrl}users/{userId}/matches/{matchId}.json";
        StartCoroutine(Put(matchUrl, matchJson));

        // --- (2) summary 누적 갱신 ---
        UpdateSummary(userId, kills, deaths, damage, isWin);
    }

    /// <summary>
    /// 총합(summary) 데이터 갱신 (누적)
    /// </summary>
    private void UpdateSummary(string userId, int kills, int deaths, int damage, bool isWin)
    {
        string url = $"{baseUrl}users/{userId}/summary.json";

        // PATCH 요청으로 해당 필드만 갱신
        string json = $@"
        {{
            ""totalKills"": {{"".sv"": ""increment"", ""by"": {kills}}},
            ""totalDeaths"": {{"".sv"": ""increment"", ""by"": {deaths}}},
            ""totalDamage"": {{"".sv"": ""increment"", ""by"": {damage}}},
            ""totalMatches"": {{"".sv"": ""increment"", ""by"": 1}},
            ""totalWins"": {{"".sv"": ""increment"", ""by"": {(isWin ? 1 : 0)}}},
            ""lastPlayed"": ""{DateTime.UtcNow:o}""
        }}";

        // Realtime Database의 REST API는 .sv를 직접 지원하지 않음.
        // 따라서 REST만 쓴다면 기존 summary를 읽고 직접 계산해야 함.
        StartCoroutine(PatchSummaryManual(url, kills, deaths, damage, isWin));
    }

    /// <summary>
    /// PATCH 대신 summary를 직접 불러와서 누적 계산 (REST만 사용할 때 필요)
    /// </summary>
    private IEnumerator PatchSummaryManual(string url, int kills, int deaths, int damage, bool isWin)
    {
        using UnityWebRequest getReq = UnityWebRequest.Get(url);
        yield return getReq.SendWebRequest();

        int totalKills = 0, totalDeaths = 0, totalDamage = 0, totalMatches = 0, totalWins = 0;
        if (getReq.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(getReq.downloadHandler.text))
        {
            var data = JsonUtility.FromJson<SummaryData>(getReq.downloadHandler.text);
            if (data != null)
            {
                totalKills = data.totalKills;
                totalDeaths = data.totalDeaths;
                totalDamage = data.totalDamage;
                totalMatches = data.totalMatches;
                totalWins = data.totalWins;
            }
        }

        totalKills += kills;
        totalDeaths += deaths;
        totalDamage += damage;
        totalMatches += 1;
        if (isWin) totalWins += 1;

        var updated = new SummaryData
        {
            totalKills = totalKills,
            totalDeaths = totalDeaths,
            totalDamage = totalDamage,
            totalMatches = totalMatches,
            totalWins = totalWins,
            lastPlayed = DateTime.UtcNow.ToString("o")
        };

        string json = JsonUtility.ToJson(updated);

        using UnityWebRequest putReq = UnityWebRequest.Put(url, json);
        putReq.method = "PUT";
        putReq.SetRequestHeader("Content-Type", "application/json");
        yield return putReq.SendWebRequest();

        if (putReq.result == UnityWebRequest.Result.Success)
            Debug.Log("[Firebase] summary 누적 갱신 성공");
        else
            Debug.LogError($"[Firebase] summary 누적 갱신 실패: {putReq.error}");
    }

    IEnumerator Put(string url, string json)
    {
        using UnityWebRequest req = UnityWebRequest.Put(url, json);
        req.method = "PUT";
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[Firebase] 매치 업로드 성공");
        else
            Debug.LogError($"[Firebase] 매치 업로드 실패: {req.error}");
    }

    [Serializable]
    private class SummaryData
    {
        public int totalKills;
        public int totalDeaths;
        public int totalDamage;
        public int totalMatches;
        public int totalWins;
        public string lastPlayed;
    }
}
