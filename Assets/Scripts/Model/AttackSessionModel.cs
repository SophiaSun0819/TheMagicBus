using UnityEngine;
using System;

[Serializable]
public class AttackSessionModel
{
    private bool _isLooking = false;
    private bool _isHolding = false;
    private float _holdDuration = 0f;
    private int _rapidPressCount = 0;
    private GameObject _currentTarget = null;

    public event Action OnAttackInterrupted;
    public event Action OnRapidPress;

    // ── Properties ──────────────────────────────────────────

    public bool IsLooking => _isLooking;
    public bool IsHolding => _isHolding;
    public float HoldDuration => _holdDuration;
    public int RapidPressCount => _rapidPressCount;
    public GameObject CurrentTarget => _currentTarget;

    /// <summary>
    /// 視線 + 長壓同時成立才算有效攻擊
    /// </summary>
    public bool IsActivelyAttacking => _isLooking && _isHolding;

    // ── Methods ──────────────────────────────────────────

    public void Reset()
    {
        _isLooking = false;
        _isHolding = false;
        _holdDuration = 0f;
        _rapidPressCount = 0;
        _currentTarget = null;
    }

    public void SetLooking(bool looking, GameObject target = null)
    {
        bool wasAttacking = IsActivelyAttacking;

        _isLooking = looking;
        _currentTarget = looking ? target : null;

        // 視線移開且當時正在攻擊 → 中斷
        if (wasAttacking && !IsActivelyAttacking)
        {
            OnAttackInterrupted?.Invoke();
            _holdDuration = 0f;
        }
    }

    public void SetHolding(bool holding)
    {
        bool wasAttacking = IsActivelyAttacking;

        _isHolding = holding;

        if (!holding)
        {
            if (wasAttacking) OnAttackInterrupted?.Invoke();
            _holdDuration = 0f;
        }
    }

    /// <summary>
    /// 每幀由 AttackController 呼叫，累積長壓時間
    /// </summary>
    public void TickHold(float deltaTime)
    {
        if (IsActivelyAttacking)
            _holdDuration += deltaTime;
    }

    /// <summary>
    /// 記錄一次連按
    /// </summary>
    public void RegisterPress()
    {
        _rapidPressCount++;
        OnRapidPress?.Invoke();
    }
}
