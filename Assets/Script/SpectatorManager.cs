using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpectatorManager : MonoBehaviour
{
    public static SpectatorManager Instance;

    // === 설정 ===
    private const string SceneCameraTag = "match";  // 씬에 박아둔 카메라/앵커 태그
    private static readonly string[] AnchorNames = { "match", "MainCamera", "CameraAnchor", "Head" };

    // 로컬에서 보이는 살아있는 플레이어들
    private readonly List<PlayerStats> alive = new();

    // 후보 순회용 인덱스(원하면 키로 다음/이전 구현 가능)
    private int index = 0;

    // 관전 전용 카메라(가능하면 씬의 Tag=match 카메라를 재사용)
    private Camera specCam;
    private AudioListener specListener;

    void Awake()
    {
        if (Instance == null) Instance = this;
        PlayerStats.Spawned += OnSpawned;
        PlayerStats.Despawned += OnDespawned;
    }

    void OnDestroy()
    {
        PlayerStats.Spawned -= OnSpawned;
        PlayerStats.Despawned -= OnDespawned;
    }

    void OnSpawned(PlayerStats ps)
    {
        if (ps && !alive.Contains(ps)) alive.Add(ps);
    }

    void OnDespawned(PlayerStats ps)
    {
        if (ps) alive.Remove(ps);

        // 관전 중 타겟이 사라지면 자동으로 다른 후보에 붙기
        if (specCam && (!CurrentTargetValid() || alive.Count == 0))
            TryAttachToFirstValid(ps);
    }

    public static void EnterSpectate(PlayerStats me)
    {
        if (Instance == null) return;
        Instance.InternalEnter(me);

    }

    // ===== 관전 진입 =====
    void InternalEnter(PlayerStats me)
    {
        // 1) 내 캐릭터(컨트롤/렌더/자기 카메라) 비활성화
        HideLocalPlayer(me);

        // 2) 관전 카메라 확보(우선: 씬의 Tag=match 카메라)
        EnsureSpectateCamera();

        // 3) 후보: 나 제외 + 활성 + 체력>0
        var candidates = alive
            .Where(p => p && p != me && p.isActiveAndEnabled && GetHealthSafe(p) > 0f)
            .ToList();

        if (candidates.Count == 0)
        {
            UIManager.Instance?.ShowDefeatPanel();
            // 아무도 없으면 '씬 카메라(match)'로 주차 시점
            UseFallbackCamera(null);
            return;
        }

        index = Mathf.Clamp(index, 0, candidates.Count - 1);
        AttachToTarget(candidates[index]);
    }

    // ===== 관전 카메라 보장 =====
    void EnsureSpectateCamera()
    {
        if (specCam != null) return;

        // 1) 씬 안에서 태그 'match' 카메라 먼저 찾기
        var sceneTaggedCam = FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c != null && c.gameObject.CompareTag(SceneCameraTag));

        if (sceneTaggedCam != null)
        {
            specCam = sceneTaggedCam;
            specListener = specCam.GetComponent<AudioListener>() ?? specCam.gameObject.AddComponent<AudioListener>();
        }
        else
        {
            // 2) 없으면 새로 생성
            var go = new GameObject("SpectateCamera");
            specCam = go.AddComponent<Camera>();
            specListener = go.AddComponent<AudioListener>();
        }

        // 다른 오디오 리스너는 모두 끄고 이 리스너만 사용
        foreach (var al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (al != specListener) al.enabled = false;

        specCam.enabled = true;
        specListener.enabled = true;
    }

    // ===== 첫 유효 대상 시도 =====
    void TryAttachToFirstValid(PlayerStats exclude)
    {
        var candidates = alive
            .Where(p => p && p != exclude && p.isActiveAndEnabled && GetHealthSafe(p) > 0f)
            .ToList();

        if (candidates.Count > 0)
        {
            index = 0;
            AttachToTarget(candidates[0]);
        }
        else
        {
            UseFallbackCamera(null);
        }
    }

    // ===== 타겟에 붙이기 (상대 카메라를 켜지 않는다! 앵커에 "우리 카메라"를 붙임) =====
    void AttachToTarget(PlayerStats target)
    {
        if (!target) { DetachAndPark(specCam.transform); return; }

        // 1) 플레이어 자식 중 Tag=match 트랜스폼을 앵커로 우선 사용
        Transform anchor = FindAnchorByTagInChildren(target.transform, SceneCameraTag);

        // 2) 없으면, 이름 기반 후보
        if (anchor == null) anchor = FindFpsAnchorByName(target.transform);

        // 3) 그래도 없으면, 타겟 자식의 어떤 카메라 Transform
        if (anchor == null)
        {
            var anyCam = target.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            anchor = anyCam ? anyCam.transform : target.transform;
        }

        // 관전 카메라를 앵커에 붙여 1인칭 느낌
        var t = specCam.transform;
        t.SetParent(anchor, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        specCam.enabled = true;
        specListener.enabled = true;
    }

    // === Tag로 앵커 찾기(자식들만) ===
    Transform FindAnchorByTagInChildren(Transform root, string tag)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            if (tr.gameObject.CompareTag(tag)) return tr;
        return null;
    }

    // === 이름으로 앵커 찾기 ===
    Transform FindFpsAnchorByName(Transform root)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var name in AnchorNames)
        {
            var tr = all.FirstOrDefault(x => x.name.Equals(name));
            if (tr) return tr;
        }
        return null;
    }

    // 현재 관전 타겟이 아직 유효한지 간단 체크
    bool CurrentTargetValid()
    {
        if (!specCam) return false;
        var parent = specCam.transform.parent;
        return parent != null && parent.gameObject.activeInHierarchy;
    }

    // 타겟이 없으면 임시 위치로 주차(→ 씬 카메라(match) 시점으로 우선 복귀)
    void DetachAndPark(Transform cam)
    {
        if (!cam) return;
        cam.SetParent(null);
        UseFallbackCamera(null);
    }

    // 대상이 없거나 앵커를 못 찾았을 때: 씬의 Tag=match 카메라 시점 사용
    void UseFallbackCamera(Transform followTarget)
    {
        if (!specCam) EnsureSpectateCamera();

        // 씬 내 Tag=match 카메라가 있으면 그 위치/회전을 그대로 사용
        var sceneTaggedCam = FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c != null && c.gameObject.CompareTag(SceneCameraTag));

        if (sceneTaggedCam != null)
        {
            specCam.transform.SetParent(null, false);
            specCam.transform.SetPositionAndRotation(sceneTaggedCam.transform.position, sceneTaggedCam.transform.rotation);
        }
        else if (followTarget != null)
        {
            specCam.transform.position = followTarget.position + Vector3.up * 1.7f - followTarget.forward * 3.5f;
            specCam.transform.rotation = Quaternion.LookRotation(followTarget.position + Vector3.up * 1.3f - specCam.transform.position);
        }
        else
        {
            // 최종 기본값
            specCam.transform.position = new Vector3(0, 10, -10);
            specCam.transform.rotation = Quaternion.Euler(20, 0, 0);
        }

        specCam.enabled = true;
        specListener.enabled = true;
    }

    // 내 로컬 플레이어 숨기기(프리팹 파괴 타이밍 꼬임 방지)
    void HideLocalPlayer(PlayerStats me)
    {
        if (!me) return;

        foreach (var mb in me.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (var r in me.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var cam in me.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
    }

    // 안전 체력 조회(캐릭터 베이스에 CurrentHealth 있다고 가정)
    float GetHealthSafe(PlayerStats ps)
    {
        try
        {
            var cs = ps as CharacterStats;
            return cs != null ? cs.CurrentHealth : 1f;
        }
        catch { return 1f; }
    }

    // (선택) 다음/이전 관전 대상 API — 키 바인딩해서 쓰고 싶을 때
    public void SpectateNext(PlayerStats me)
    {
        var candidates = alive.Where(p => p && p != me && p.isActiveAndEnabled && GetHealthSafe(p) > 0f).ToList();
        if (candidates.Count == 0) { UseFallbackCamera(null); return; }
        index = (index + 1) % candidates.Count;
        AttachToTarget(candidates[index]);
    }
    public void SpectatePrev(PlayerStats me)
    {
        var candidates = alive.Where(p => p && p != me && p.isActiveAndEnabled && GetHealthSafe(p) > 0f).ToList();
        if (candidates.Count == 0) { UseFallbackCamera(null); return; }
        index = (index - 1 + candidates.Count) % candidates.Count;
        AttachToTarget(candidates[index]);
    }
    public float sensitivity = 2f;
    private float rotationX = 0f;
    private float rotationY = 0f;
    
    

  
}
