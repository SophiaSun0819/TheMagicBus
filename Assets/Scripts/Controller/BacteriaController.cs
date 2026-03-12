using UnityEngine;
using System;

public class BacteriaController : MonoBehaviour, IAttackable, IGazeable
{
    [SerializeField]
    private BacteriaModel _model = new();

    private BacteriaView _view;

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Awake()
    {
        _view = GetComponent<BacteriaView>();
        _model.Initialize();

        // Model 事件 → 通知 View
        _model.OnStateChanged += _view.OnStateChanged;
        _model.OnHpChanged += _view.OnHpChanged;
        _model.OnDied += OnDied;
    }

    private void Update()
    {
        UpdateAllyCount();
        UpdateMovement();
    }

    private void OnDestroy()
    {
        _model.OnStateChanged -= _view.OnStateChanged;
        _model.OnHpChanged -= _view.OnHpChanged;
        _model.OnDied -= OnDied;
    }

    // ── IAttackable ──────────────────────────────────────────

    public void TakeDamage(float amount)
    {
        if (_model.State == BacteriaState.Dead) return;

        _model.State = BacteriaState.UnderAttack;
        _model.ApplyDamage(amount);
    }

    public void TakePressDamage()
    {
        if (_model.State == BacteriaState.Dead) return;

        _model.ApplyDamage(_model.BonusDecayPerPress);
    }

    public void TriggerFlee()
    {
        if (_model.State == BacteriaState.Dead) return;

        _model.State = BacteriaState.Fleeing;
    }

    // ── IGazeable ──────────────────────────────────────────

    public void OnGazeEnter()
    {
        if (_model.State == BacteriaState.Idle)
            _model.State = BacteriaState.Targeted;
    }

    public void OnGazeExit()
    {
        if (_model.State == BacteriaState.Targeted)
            _model.State = BacteriaState.Idle;

        // UnderAttack 時移開視線的處理由 AttackController 負責呼叫 TriggerFlee()
        // 這裡不重複處理，避免邏輯衝突
    }

    // ── Private ──────────────────────────────────────────

    /// <summary>
    /// 用 Sphere Overlap 偵測附近同伴數量
    /// 細菌自己負責偵測，不依賴外部管理器
    /// </summary>
    private void UpdateAllyCount()
    {
        if (_model.State == BacteriaState.Dead) return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            _model.AllyDetectRadius,
            LayerMask.GetMask("Bacteria")   // 細菌需要在 "Bacteria" Layer
        );

        // 扣掉自己
        _model.AllyCount = hits.Length - 1;
    }

    /// <summary>
    /// 根據目前 State 驅動移動
    /// </summary>
    private void UpdateMovement()
    {
        if (_model.State == BacteriaState.Dead) return;

        float speed = _model.GetCurrentSpeed();

        // 基本隨機遊走，之後可替換成完整 AI（NavMesh、Steering 等）
        transform.Translate(transform.forward * speed * Time.deltaTime);
    }

    private void OnDied()
    {
        // 通知場景管理器（之後擴充用），目前先只做視覺
        Destroy(gameObject, 1.5f);  // 留時間播死亡動畫
    }

    // ── Gizmos（編輯器偵錯用）──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _model.AllyDetectRadius);
    }
}
