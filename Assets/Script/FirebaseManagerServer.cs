using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManagerServer : MonoBehaviour
{
    public static FirebaseManagerServer Instance;

    [Header("Firebase RTDB 설정")]
    [SerializeField] private string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";
    [SerializeField] private string authToken = ""; // 필요 시 ?auth=token

    [Header("네트워크 설정")]
    [SerializeField] private int requestTimeoutSec = 10;
    [SerializeField] private int maxRetry = 2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ===========================================================
    // 🔹 매치 전체 업로드 (모든 플레이어 데이터 포함)
    // ===========================================================

    [Serializable]
    public class MatchSummary
    {
        public string matchId;
        public bool victory;
        public int durationSec;
        public string timestamp;
        public List<PlayerMatchRecord> players;  // ✅ 딕셔너리 → 리스트
    }

    public void UploadMatchSummary(string matchId, List<PlayerMatchRecord> records, bool isVictory, float durationSec)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            Debug.LogError("[Firebase] matchId가 비어 있음");
            return;
        }
        if (records == null || records.Count == 0)
        {
            Debug.LogWarning("[Firebase] 업로드할 플레이어 기록이 없음");
            return;
        }
        StartCoroutine(CoUploadMatchSummary(matchId, records, isVictory, durationSec));
    }

    private IEnumerator CoUploadMatchSummary(string matchId, List<PlayerMatchRecord> records, bool isVictory, float durationSec)
    {
        // ✅ 매치 요약 생성
        var match = new MatchSummary
        {
            matchId = matchId,
            victory = isVictory,
            durationSec = Mathf.RoundToInt(durationSec),
            timestamp = DateTime.UtcNow.ToString("O"),
            players = records  // 리스트 그대로 저장
        };

        string matchUrl = BuildUrl($"matches/{Escape(matchId)}.json");
        string matchJson = JsonUtility.ToJson(match, true);

        // 1) 매치 전체 업로드
        yield return SendJson(matchUrl, matchJson, "PUT");

        // 2) 각 유저 matchId 등록
        foreach (var r in records)
        {
            string userMatchUrl = BuildUrl($"users/{Escape(r.userId)}/matches/{Escape(matchId)}.json");
            yield return SendJson(userMatchUrl, "true", "PUT");
        }

        Debug.Log($"[Firebase] 매치 업로드 완료: {matchId}, 플레이어 {records.Count}명");
    }

    // ===========================================================
    // 🔹 공통 JSON 전송 유틸
    // ===========================================================
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
                {
                    yield break;
                }

                attempt++;
                Debug.LogWarning($"[Firebase] {method} 실패({attempt}/{maxRetry + 1}): {req.error}");
                if (attempt > maxRetry)
                {
                    Debug.LogError($"[Firebase] {method} 최종 실패: {req.error}\nurl={url}\nbody={json}");
                    yield break;
                }

                yield return new WaitForSeconds(0.5f * attempt);
            }
        }
    }

    // ===========================================================
    // 🔹 URL / Escape 유틸
    // ===========================================================
    private string BuildUrl(string pathWithJsonSuffix)
    {
        string sep = baseUrl.EndsWith("/") ? "" : "/";
        string url = baseUrl + sep + pathWithJsonSuffix.TrimStart('/');
        if (!string.IsNullOrEmpty(authToken))
        {
            url += (url.Contains("?") ? "&" : "?") + "auth=" + UnityWebRequest.EscapeURL(authToken);
        }
        return url;
    }

    private string Escape(string s)
    {
        return s
            .Replace(".", "_")
            .Replace("#", "_")
            .Replace("$", "_")
            .Replace("[", "_")
            .Replace("]", "_");
    }
}
