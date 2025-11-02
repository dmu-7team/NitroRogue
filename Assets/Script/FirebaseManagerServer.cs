using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManagerServer : MonoBehaviour
{
    public static FirebaseManagerServer Instance;

    [Header("Firebase RTDB")]
    [SerializeField] private string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";
    [SerializeField] private string authToken = ""; // 필요 시 ?auth=토큰

    [Header("Network")]
    [SerializeField] private int requestTimeoutSec = 10;
    [SerializeField] private int maxRetry = 2;

    private void Awake() => Instance = this;

    // ===============================
    // 유저별 경기 결과 업로드
    // ===============================
    public void UploadMatchResult(string userId, int kills, int deaths, int damage, bool isWin)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            Debug.LogError("[Firebase] userId가 비어 있습니다.");
            return;
        }
        StartCoroutine(CoUpload(userId, kills, deaths, damage, isWin));
    }

    private IEnumerator CoUpload(string userId, int kills, int deaths, int damage, bool isWin)
    {
        // 1) 경기 데이터 (POST)
        string matchesUrl = BuildUrl($"users/{Escape(userId)}/matches.json");
        string matchJson = $@"
        {{
            ""kills"": {kills},
            ""deaths"": {deaths},
            ""damage"": {damage},
            ""result"": ""{(isWin ? "Win" : "Lose")}"",
            ""timestamp"": ""{DateTime.UtcNow:O}""
        }}";

        yield return SendJson(matchesUrl, matchJson, "POST");

        // 2) 요약 통계 (PATCH 증분)
        string summaryUrl = BuildUrl($"users/{Escape(userId)}/summary.json");
        string patchJson = $@"
        {{
            ""totalKills"": {{ "".sv"": {{ ""increment"": {kills} }} }},
            ""totalDeaths"": {{ "".sv"": {{ ""increment"": {deaths} }} }},
            ""totalDamage"": {{ "".sv"": {{ ""increment"": {damage} }} }},
            ""totalMatches"": {{ "".sv"": {{ ""increment"": 1 }} }},
            ""totalWins"": {{ "".sv"": {{ ""increment"": {(isWin ? 1 : 0)} }} }},
            ""lastPlayed"": ""{DateTime.UtcNow:O}""
        }}";

        yield return SendJson(summaryUrl, patchJson, "PATCH");

        Debug.Log("[Firebase] 경기 업로드 및 요약 완료");
    }

    // ===============================
    // 공통 전송
    // ===============================
    private IEnumerator SendJson(string url, string json, string method)
    {
        int attempt = 0;

        while (true)
        {
            using (var req = new UnityWebRequest(url, method))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(string.IsNullOrEmpty(json) ? "{}" : json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                req.timeout = requestTimeoutSec;

                if (method == "PATCH")
                    req.SetRequestHeader("X-HTTP-Method-Override", "PATCH");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                    yield break;

                attempt++;
                Debug.LogWarning($"[Firebase] {method} 실패({attempt}/{maxRetry + 1}): {req.error}");
                if (attempt > maxRetry)
                {
                    Debug.LogError($"[Firebase] {method} 최종 실패: {req.error}\n{url}\n{json}");
                    yield break;
                }

                yield return new WaitForSeconds(0.5f * attempt);
            }
        }
    }

    // ===============================
    // URL / 키 유틸
    // ===============================
    private string BuildUrl(string pathWithJsonSuffix)
    {
        string sep = baseUrl.EndsWith("/") ? "" : "/";
        string url = baseUrl + sep + pathWithJsonSuffix.TrimStart('/');
        if (!string.IsNullOrEmpty(authToken))
            url += (url.Contains("?") ? "&" : "?") + "auth=" + UnityWebRequest.EscapeURL(authToken);
        return url;
    }

    private string Escape(string s)
    {
        return s.Replace(".", "_").Replace("#", "_").Replace("$", "_").Replace("[", "_").Replace("]", "_");
    }
}
