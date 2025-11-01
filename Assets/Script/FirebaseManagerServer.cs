using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManagerServer : MonoBehaviour
{
    public static FirebaseManagerServer Instance;

    // ★ 필수: 본인 프로젝트 DB URL로 교체 (맨끝에 슬래시 OK)
    [SerializeField] private string baseUrl = "https://nitrorogue-24e5c-default-rtdb.firebaseio.com/";
    // ★ 선택: RTDB 규칙이 인증 필요하면 토큰 넣기 (없으면 빈 문자열)
    [SerializeField] private string authToken = ""; // e.g., "eyJhbGciOi..." (ID 토큰/커스텀 토큰 등)

    [SerializeField] private int requestTimeoutSec = 10;
    [SerializeField] private int maxRetry = 2;

    void Awake() => Instance = this;

    // ========= 공개 API =========
    /// <summary>
    /// 매치 종료 시 서버에서 호출 (서버 스레드)
    /// </summary>
    public void UploadMatchResult(string userId, int kills, int deaths, int damage, bool isWin)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            Debug.LogError("[Firebase] userId가 비어 있습니다.");
            return;
        }
        StartCoroutine(CoUpload(userId, kills, deaths, damage, isWin));
    }

    // ========= 내부 구현 =========
    private IEnumerator CoUpload(string userId, int kills, int deaths, int damage, bool isWin)
    {
        // ① 개별 매치 기록: POST로 고유키(push id) 생성 (동시성 안전)
        string matchesUrl = BuildUrl($"users/{Escape(userId)}/matches.json");
        string matchJson = $@"
        {{
            ""kills"": {kills},
            ""deaths"": {deaths},
            ""damage"": {damage},
            ""result"": ""{(isWin ? "Win" : "Lose")}"",
            ""timestamp"": ""{DateTime.UtcNow:O}""
        }}";
        yield return SendJson(matchesUrl, matchJson, method: "POST");  // ★ POST

        // ② 요약 통계: 원자적 증가(atomic increment)로 레이스 방지
        // RTDB REST: {".sv":{"increment": N}} 형태
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

        // PATCH는 UnityWebRequest에선 X-HTTP-Method-Override=PATCH 또는 custom method 지원
        yield return SendJson(summaryUrl, patchJson, method: "PATCH");

        Debug.Log("[Firebase] 매치 업로드 및 summary 업데이트 완료");
    }

    // ========= 공통 송신 유틸 =========
    private IEnumerator SendJson(string url, string json, string method)
    {
        int attempt = 0;
        while (true)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, method))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                req.timeout = requestTimeoutSec;

                // 유니티 일부 런타임에서 PATCH 미지원 시 대비
                if (method == "PATCH")
                    req.SetRequestHeader("X-HTTP-Method-Override", "PATCH");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    // Debug.Log($"[Firebase] {method} OK: {url} -> {req.downloadHandler.text}");
                    yield break;
                }

                attempt++;
                Debug.LogWarning($"[Firebase] {method} 실패({attempt}/{maxRetry + 1}): {req.error} url={url}");
                if (attempt > maxRetry)
                {
                    Debug.LogError($"[Firebase] {method} 최종 실패: {req.error}\nurl={url}\nbody={json}");
                    yield break;
                }

                // 간단한 지수 백오프
                yield return new WaitForSeconds(0.5f * attempt);
            }
        }
    }

    // ========= URL/인코딩 유틸 =========
    private string BuildUrl(string pathWithJsonSuffix)
    {
        // baseUrl + path(.json) + ?auth=token (옵션)
        string sep = baseUrl.EndsWith("/") ? "" : "/";
        string url = baseUrl + sep + pathWithJsonSuffix.TrimStart('/');
        if (!string.IsNullOrEmpty(authToken))
        {
            url += (url.Contains("?") ? "&" : "?") + "auth=" + UnityWebRequest.EscapeURL(authToken);
        }
        return url;
    }

    // Firebase 경로에서 안전한 문자만 사용 (간단 이스케이프)
    private string Escape(string s)
    {
        // RTDB 키 제약: ".", "#", "$", "[", "]" 금지. 필요시 치환.
        return s.Replace(".", "_").Replace("#", "_").Replace("$", "_").Replace("[", "_").Replace("]", "_");
    }
}
