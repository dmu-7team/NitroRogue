using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// 씬에 있는 카메라를 '내 매치의 맵 중심'으로 이동시켜
/// 탑다운(정사영)으로 보게 하는 간단 컨트롤러.
/// - 서버 수정 불필요
/// - clientMapPrefab 자식에 "MapCenter"가 있으면 그걸 기준으로,
///   없으면 렌더러 바운드로 중심/크기를 계산해 위로 올라감.
/// </summary>
[DisallowMultipleComponent]
public class MatchTopdownCamera : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;             // 본인 카메라
    [Tooltip("카메라 높이(앵커의 Y에서 얼마나 위로 올릴지)")]
    public float height = 120f;

    [Header("Projection")]
    [Tooltip("정사영으로 전환 (권장)")]
    public bool useOrthographic = true;

    [Tooltip("바운드에 맞춰 자동으로 orthographicSize를 맞출지")]
    public bool fitOrthoToBounds = true;

    [Tooltip("fitOrthoToBounds가 켜져 있을 때 여유 배수")]
    public float orthoMargin = 1.15f;

    [Tooltip("fit이 꺼져 있으면 이 값 사용")]
    public float fixedOrthographicSize = 60f;

    [Header("Smoothing")]
    public bool smoothFollow = true;
    public float followLerp = 10f;

    [Header("Anchor Search")]
    [Tooltip("clientMapPrefab 자식에 이 이름의 Transform이 있으면 우선 사용")]
    public string centerName = "MapCenter";

    [Tooltip("바운드 계산에서 포함할 레이어 (0이면 전체)")]
    public LayerMask boundsLayerMask = 0;

    private Transform _followAnchor; // MapCenter 또는 바운드 중심을 대리하는 임시 Transform
    private Bounds _mapBounds;
    private bool _hasBounds;
    private Quaternion _topdownRot = Quaternion.Euler(90f, 0f, 0f);
    private Transform _matchRoot; // 내 매치의 MatchManager.transform

    void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    void Awake()
    {
        if (!targetCamera) targetCamera = GetComponent<Camera>();
    }

    void OnEnable()
    {
        StartCoroutine(Co_BindAfterMapLoaded());
    }

    /// <summary>
    /// 로컬 플레이어/매치/맵 생성까지 기다린 뒤 앵커/바운드 결정
    /// </summary>
    private IEnumerator Co_BindAfterMapLoaded()
    {
        // 1) 로컬 플레이어 ID 기다림
        while (NetworkClient.active && NetworkClient.localPlayer == null)
            yield return null;

        // 2) 내 매치 루트 찾기 (로컬 플레이어의 부모 체인에서 MatchManager)
        _matchRoot = TryGetMatchRootFromLocalPlayer();

        // 매치 루트를 못 찾으면 월드 기준으로라도 동작
        // 그래도 맵이 뜰 시간을 조금 기다림
        float wait = 0f;
        while (_matchRoot == null && wait < 2f)
        {
            _matchRoot = TryGetMatchRootFromLocalPlayer();
            wait += Time.deltaTime;
            yield return null;
        }

        // 3) 맵 중심 후보 찾기
        Transform center = null;
        if (_matchRoot != null)
        {
            center = FindDeepChild(_matchRoot, centerName);
        }

        if (center != null)
        {
            _followAnchor = center;
            _hasBounds = TryCalcBounds(_matchRoot, out _mapBounds);
        }
        else
        {
            // MapCenter가 없다면 바운드로 중심 대체
            if (_matchRoot != null && TryCalcBounds(_matchRoot, out _mapBounds))
            {
                _hasBounds = true;
                // 바운드 중심을 따라가기 위해 임시 anchor 생성(씬에 남지 않게 HideFlags)
                var temp = new GameObject("~MapBoundsCenter").transform;
                temp.position = _mapBounds.center;
                temp.rotation = Quaternion.identity;
                temp.hideFlags = HideFlags.DontSave;
                _followAnchor = temp;
            }
            else
            {
                // 아무것도 못 찾으면 (0,0,0) 기준
                var temp = new GameObject("~WorldOriginCenter").transform;
                temp.position = Vector3.zero;
                temp.rotation = Quaternion.identity;
                temp.hideFlags = HideFlags.DontSave;
                _followAnchor = temp;
                _hasBounds = false;
            }
        }

        // 4) 카메라 탑뷰 전환
        if (useOrthographic)
        {
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = fitOrthoToBounds && _hasBounds
                ? Mathf.Max(_mapBounds.extents.x, _mapBounds.extents.z) * orthoMargin
                : fixedOrthographicSize;
        }
        targetCamera.transform.rotation = _topdownRot;

        // 첫 위치 스냅
        var basePos = _followAnchor ? _followAnchor.position : Vector3.zero;
        targetCamera.transform.position = new Vector3(basePos.x, basePos.y + height, basePos.z);

        // 5) 바운드 중심용 임시 앵커는 맵이 움직이면 갱신 필요할 수 있어
        // 여기서는 매 프레임 바운드 재계산까지는 과하니, 필요시 외부에서 Refit 요청 메서드 노출
    }

    void LateUpdate()
    {
        if (!targetCamera || !_followAnchor) return;

        Vector3 basePos = _followAnchor.position;
        Vector3 targetPos = new Vector3(basePos.x, basePos.y + height, basePos.z);

        if (smoothFollow)
            targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, targetPos, Time.deltaTime * followLerp);
        else
            targetCamera.transform.position = targetPos;
    }

    /// <summary>
    /// 맵(매치 루트) 하위의 렌더러로 월드 바운드 계산
    /// </summary>
    private bool TryCalcBounds(Transform root, out Bounds b)
    {
        b = default;
        if (root == null) return false;

        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return false;

        bool inited = false;
        foreach (var r in rends)
        {
            if (!r || !r.bounds.size.sqrMagnitude.Equals(r.bounds.size.sqrMagnitude)) continue;

            if (boundsLayerMask != 0 && ((boundsLayerMask.value & (1 << r.gameObject.layer)) == 0))
                continue; // 레이어 필터링

            if (!inited)
            {
                b = r.bounds;
                inited = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }
        return inited;
    }

    /// <summary>
    /// 로컬 플레이어의 부모 체인에서 MatchManager 찾고 그 Transform 반환
    /// </summary>
    private Transform TryGetMatchRootFromLocalPlayer()
    {
        if (!NetworkClient.active || NetworkClient.localPlayer == null) return null;
        var t = NetworkClient.localPlayer.transform;
        var mm = t.GetComponentInParent<MatchManager>();
        return mm ? mm.transform : null;
    }

    /// <summary>
    /// 자식 재귀 탐색 (이름 일치)
    /// </summary>
    private Transform FindDeepChild(Transform root, string name)
    {
        foreach (Transform c in root)
        {
            if (c.name == name) return c;
            var r = FindDeepChild(c, name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// (선택) 외부에서 호출해 바운드 재계산 + 오쏘 리핏
    /// </summary>
    public void RefitToBounds()
    {
        if (_matchRoot == null) return;
        if (TryCalcBounds(_matchRoot, out _mapBounds))
        {
            _hasBounds = true;
            if (useOrthographic && fitOrthoToBounds)
                targetCamera.orthographicSize = Mathf.Max(_mapBounds.extents.x, _mapBounds.extents.z) * orthoMargin;

            if (_followAnchor != null && _followAnchor.name.StartsWith("~MapBoundsCenter"))
                _followAnchor.position = _mapBounds.center;
        }
    }
}
