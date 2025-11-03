using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class FirebaseManagerServer : MonoBehaviour
{
    public static FirebaseManagerServer Instance;

    [Header("Firebase RTDB 설정")]
    [SerializeField] private string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";
    [SerializeField] private string authToken = "";

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
    // DTO
    // ===========================================================
    [Serializable]
    public class MatchSummary
    {
        public string matchId;
        public bool victory;
        public int durationSec;
        public string timestamp;
        public List<PlayerMatchRecord> players;
    }

    [Serializable]
    private class UserSummary
    {
        public int topKills;
    }

    // ===========================================================
    // 공개 API
    // ===========================================================

    /// <summary>
    /// [All-in-One / 서버용]
    /// 1. 매치 결과를 업로드합니다.
    /// 2. 모든 플레이어를 순회하며 topKills를 갱신합니다.
    /// 3. 신기록을 달성한 유저가 있으면, 랭킹을 조회합니다.
    /// 4. MatchManager.OnRankingResult를 '각 유저별로' 콜백합니다.
    ///    (신기록 = rank > 0, 아니면 -1)
    /// </summary>
    public IEnumerator CoUploadAndGetRankings(
        string matchId, List<PlayerMatchRecord> records, bool isVictory, float durationSec,
        MatchManager caller)
    {
        // 1) 매치 요약 저장 (한 번만 실행)
        var match = new MatchSummary
        {
            matchId = matchId,
            victory = isVictory,
            durationSec = Mathf.RoundToInt(durationSec),
            timestamp = DateTime.UtcNow.ToString("O"),
            players = records
        };

        string matchUrl = BuildUrl($"matches/{Escape(matchId)}.json");
        string matchJson = JsonUtility.ToJson(match, true);
        yield return SendJson(matchUrl, matchJson, "PUT");

        // 2) 각 유저를 순회하며 업로드 + 랭킹 조회
        foreach (var r in records)
        {
            // 2-1. users/{uid}/matches/{matchId} = true
            string userMatchUrl = BuildUrl($"users/{Escape(r.userId)}/matches/{Escape(matchId)}.json");
            yield return SendJson(userMatchUrl, "true", "PUT");

            // 2-2. users/{uid}/summary.topKills 갱신(높을 때만)
            string summaryUrl = BuildUrl($"users/{Escape(r.userId)}/summary.json");

            int prevTopKills = 0;
            using (var getReq = UnityWebRequest.Get(summaryUrl))
            {
                getReq.timeout = requestTimeoutSec;
                yield return getReq.SendWebRequest();

                if (getReq.result == UnityWebRequest.Result.Success &&
                    !string.IsNullOrEmpty(getReq.downloadHandler.text) &&
                    getReq.downloadHandler.text != "null")
                {
                    try
                    {
                        var summary = JsonUtility.FromJson<UserSummary>(getReq.downloadHandler.text);
                        prevTopKills = summary.topKills;
                    }
                    catch { }
                }
            }

            // 2-3. 신기록 판별 및 처리
            if (r.kills > prevTopKills)
            {
                Debug.Log($"[Firebase] 신기록 달성 (UserId: {r.userId}, Kills: {r.kills}). DB갱신 및 랭킹 조회 시작.");

                string patchJson = $@"{{ ""topKills"": {r.kills} }}";
                yield return SendJson(summaryUrl, patchJson, "PATCH");

                yield return StartCoroutine(InternalCoGetRanking(r.userId, r.kills, r.nickname, caller));
            }
        }
    }

    /// <summary>
    /// (내부용) 파이어베이스에서 상위표를 읽어 MatchManager로 콜백합니다.
    /// [수정] 실패 지점을 명확히 알 수 있도록 디버그 로그가 추가되었습니다.
    /// </summary>
    private IEnumerator InternalCoGetRanking(string userId, int currentKills, string nickname, MatchManager caller)
    {
        string orderBy = UnityWebRequest.EscapeURL("\"summary/topKills\"");
        string url = BuildUrl($"users.json?orderBy={orderBy}&limitToLast=1000");

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSec;
            yield return req.SendWebRequest();

            // 1. HTTP 실패
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Firebase] 랭킹 HTTP 실패: {req.error}\nURL: {url}");
                caller?.OnRankingResult(userId, -1, -1f);
                yield break;
            }

            string raw = req.downloadHandler.text;
            // 2. 응답이 비어있음
            if (string.IsNullOrEmpty(raw) || raw == "null")
            {
                Debug.LogWarning("[Firebase] 랭킹 응답이 비어있습니다 (null or empty).");
                caller?.OnRankingResult(userId, -1, -1f);
                yield break;
            }

            raw = raw.Trim('\uFEFF', '\u200B', ' ', '\n', '\r', '\t');

            var list = new List<(string uid, int kills)>();

            try
            {
                // ★★★★★ MiniJSON 대신 JToken.Parse 사용 ★★★★★
                JToken rootToken = JToken.Parse(raw);

                // Firebase가 반환한 데이터가 { "uid1": {...}, "uid2": {...} } 형태 (Object)일 때
                if (rootToken is JObject dictObj)
                {
                    foreach (var kv in dictObj)
                    {
                        string uid = kv.Key;
                        JToken summary = kv.Value?["summary"];
                        if (summary != null && summary["topKills"] != null)
                        {
                            list.Add((uid, (int)summary["topKills"]));
                        }
                    }
                }
                // 로그처럼 [ { "uid1": {...} }, { "uid2": {...} } ] 형태 (Array)일 때
                else if (rootToken is JArray arr)
                {
                    foreach (JObject item in arr.Children<JObject>())
                    {
                        foreach (var kv in item) // { "uid1": {...} }
                        {
                            string uid = kv.Key;
                            JToken summary = kv.Value?["summary"];
                            if (summary != null && summary["topKills"] != null)
                            {
                                list.Add((uid, (int)summary["topKills"]));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 3. JSON 파싱 실패
                Debug.LogWarning($"[Firebase] Newtonsoft JSON 파싱 실패: {ex.Message}\nRaw(head): {raw.Substring(0, Math.Min(200, raw.Length))}");
                caller?.OnRankingResult(userId, -1, -1f);
                yield break;
            }

            // 4. 랭킹 리스트가 비어있음
            if (list.Count == 0)
            {
                Debug.LogWarning("[Firebase] 랭킹 리스트를 파싱했으나 비어있습니다 (list.Count == 0).");
                caller?.OnRankingResult(userId, -1, -1f);
                yield break;
            }

            list.Sort((a, b) => b.kills.CompareTo(a.kills));

            int total = list.Count;
            int idx = list.FindIndex(x => x.uid == userId);

            // 5. 랭킹에 내 ID가 없음 (1000등 밖)
            if (idx < 0)
            {
                Debug.LogWarning($"[Firebase] 랭킹 조회 성공. Kills: {currentKills}, Total: {total}. " +
                                 $"하지만 {userId}가 Top {total} 랭킹 안에 없습니다. (1000등 밖)");
                caller?.OnRankingResult(userId, -1, -1f);
                yield break;
            }

            // ★ 성공
            int rank = idx + 1;
            float percent = (float)rank / Mathf.Max(1, total) * 100f;

            Debug.Log($"[Firebase] 랭킹 계산 성공! Rank: {rank}, Percent: {percent}");
            caller?.OnRankingResult(userId, rank, percent);
        }
    }



    // ===========================================================
    // 공통 JSON 전송 유틸
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
                    yield break;

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
    // URL / Escape 유틸
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
