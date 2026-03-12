using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GazeController : MonoBehaviour
{
    [Header("Gaze Settings")]
    [SerializeField] private float _maxGazeDistance = 10f;
    [SerializeField] private LayerMask _targetLayer;        // 設成 "Bacteria"

    private IGazeable _currentTarget;

#if !UNITY_EDITOR
    [Header("XRI")]
    [SerializeField]
    private XRRayInteractor _gazeInteractor;   // 拖入 Gaze Interactor
#endif

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Update()
    {
#if UNITY_EDITOR
        HandleMouseGaze();
#else
        HandleXRGaze();
#endif
    }

    // ── Editor：滑鼠模擬視線 ──────────────────────────────────────────

#if UNITY_EDITOR
    private void HandleMouseGaze()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
 
        if (Physics.Raycast(ray, out RaycastHit hit, _maxGazeDistance, _targetLayer))
        {
            IGazeable gazeable = hit.collider.GetComponent<IGazeable>();
            SetTarget(gazeable);
        }
        else
        {
            SetTarget(null);
        }
    }
#endif

    // ── XRI：裝置視線 ──────────────────────────────────────────

#if !UNITY_EDITOR
    private void HandleXRGaze()
    {
        if (_gazeInteractor == null) return;

        if (_gazeInteractor.TryGetCurrentUIRaycastResult(out var result))
        {
            IGazeable gazeable = result.gameObject.GetComponent<IGazeable>();
            SetTarget(gazeable);
        }
        else if (_gazeInteractor.TryGetHitInfo(out Vector3 _, out Vector3 _, out int _, out bool _))
        {
            // TryGetHitInfo 有命中，但取得 GameObject 需要透過 interactables
            var interactable = _gazeInteractor.interactablesSelected.Count > 0
                ? _gazeInteractor.interactablesSelected[0]
                : null;

            IGazeable gazeable = (interactable as MonoBehaviour)?.GetComponent<IGazeable>();
            SetTarget(gazeable);
        }
        else
        {
            SetTarget(null);
        }
    }
#endif

    // ── 共用邏輯 ──────────────────────────────────────────

    /// <summary>
    /// 切換目前的視線目標，自動呼叫 OnGazeEnter / OnGazeExit
    /// </summary>
    private void SetTarget(IGazeable newTarget)
    {
        if (newTarget == _currentTarget) return;

        _currentTarget?.OnGazeExit();
        _currentTarget = newTarget;
        _currentTarget?.OnGazeEnter();
    }

    /// <summary>
    /// 給 AttackController 查詢目前視線目標
    /// </summary>
    public IGazeable CurrentTarget => _currentTarget;
}
