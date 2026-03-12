using UnityEngine;

/// <summary>
/// 掛在 XR Origin / Player 上
/// 整合 GazeController + InputController，驅動攻擊邏輯
/// </summary>
[RequireComponent(typeof(GazeController))]
[RequireComponent(typeof(InputController))]
public class AttackController : MonoBehaviour
{
    private GazeController _gazeController;
    private InputController _inputController;
    private AttackSessionModel _session = new AttackSessionModel();

    // 目前攻擊目標
    private IAttackable _currentAttackTarget;

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Awake()
    {
        _gazeController = GetComponent<GazeController>();
        _inputController = GetComponent<InputController>();

        // 訂閱 Input 事件
        _inputController.OnTriggerHoldStarted += OnHoldStarted;
        _inputController.OnTriggerHoldCanceled += OnHoldCanceled;
        _inputController.OnTriggerPressed += OnPressed;

        // 視線中斷 → 觸發逃跑
        _session.OnAttackInterrupted += OnAttackInterrupted;
    }

    private void Update()
    {
        UpdateGaze();
        _session.TickHold(Time.deltaTime);

        if (_session.IsActivelyAttacking)
            ApplyContinuousDamage();
    }

    private void OnDestroy()
    {
        _inputController.OnTriggerHoldStarted -= OnHoldStarted;
        _inputController.OnTriggerHoldCanceled -= OnHoldCanceled;
        _inputController.OnTriggerPressed -= OnPressed;
        _session.OnAttackInterrupted -= OnAttackInterrupted;
    }

    // ── Gaze ──────────────────────────────────────────

    private void UpdateGaze()
    {
        IGazeable gazeTarget = _gazeController.CurrentTarget;
        GameObject targetObject = (gazeTarget as MonoBehaviour)?.gameObject;

        bool isLooking = gazeTarget != null;
        _session.SetLooking(isLooking, targetObject);

        // 更新攻擊目標
        _currentAttackTarget = isLooking
            ? (gazeTarget as IAttackable)
            : null;
    }

    // ── Input Callbacks ──────────────────────────────────────────

    private void OnHoldStarted()
    {
        _session.SetHolding(true);
    }

    private void OnHoldCanceled()
    {
        _session.SetHolding(false);
    }

    private void OnPressed()
    {
        if (!_session.IsActivelyAttacking) return;

        // 連按：每次按壓給予額外傷害（突破同伴保護）
        _session.RegisterPress();
        _currentAttackTarget?.TakePressDamage();
    }

    // ── Attack Logic ──────────────────────────────────────────

    /// <summary>
    /// 每幀扣血（長壓期間持續呼叫）
    /// </summary>
    private void ApplyContinuousDamage()
    {
        if (_currentAttackTarget == null) return;

        // BacteriaController 自己知道 allyCount，會在 TakeDamage 內計算 actualDecayRate
        _currentAttackTarget.TakeDamage(GetCurrentDecayAmount());
    }

    /// <summary>
    /// 這幀應該扣多少血（交給 BacteriaModel 計算 allyCount 加成）
    /// AttackController 只負責傳遞 deltaTime，不直接計算 allyResist
    /// </summary>
    private float GetCurrentDecayAmount()
    {
        // 實際 decayRate 由 BacteriaModel.GetActualDecayRate() 決定
        // 這裡傳 deltaTime 進去，讓 BacteriaController 自己算這幀扣多少
        return Time.deltaTime;
    }

    // ── Interrupted ──────────────────────────────────────────

    private void OnAttackInterrupted()
    {
        _currentAttackTarget?.TriggerFlee();
        _currentAttackTarget = null;
    }
}
