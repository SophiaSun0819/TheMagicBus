using UnityEngine;
using System;

[Serializable]
public class BacteriaModel
{
    [Header("Health")]
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _hp;

    [Header("Attack Resistance")]
    [SerializeField] private float _baseDecayRate = 20f;       // 每秒基礎扣血量
    [SerializeField] private float _allyResistFactor = 0.5f;   // 每個同伴降低多少倍率
    [SerializeField] private float _bonusDecayPerPress = 10f;   // 每次快速按壓的額外扣血

    [Header("Movement")]
    [SerializeField] private float _normalSpeed = 1.5f;
    [SerializeField] private float _slowedSpeed = 0.5f;        // 被攻擊時速度
    [SerializeField] private float _fleeSpeed = 4f;            // 逃跑速度

    [Header("Ally Detection")]
    [SerializeField] private float _allyDetectRadius = 3f;
    [SerializeField] private int _allyCount = 0;

    private BacteriaState _state = BacteriaState.Idle;

    public event Action<BacteriaState> OnStateChanged;
    public event Action<float> OnHpChanged;
    public event Action OnDied;

    // ── Properties ──────────────────────────────────────────

    public float Hp => _hp;
    public float MaxHp => _maxHp;
    public float HpRatio => _hp / _maxHp;

    public float BaseDecayRate => _baseDecayRate;
    public float AllyResistFactor => _allyResistFactor;
    public float BonusDecayPerPress => _bonusDecayPerPress;

    public float NormalSpeed => _normalSpeed;
    public float SlowedSpeed => _slowedSpeed;
    public float FleeSpeed => _fleeSpeed;

    public float AllyDetectRadius => _allyDetectRadius;

    public int AllyCount
    {
        get => _allyCount;
        set => _allyCount = Mathf.Max(0, value);
    }

    public BacteriaState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            OnStateChanged?.Invoke(_state);
        }
    }

    // ── Methods ──────────────────────────────────────────

    public void Initialize()
    {
        _hp = _maxHp;
        _state = BacteriaState.Idle;
        _allyCount = 0;
    }

    /// <summary>
    /// 計算實際每秒扣血量（已考慮同伴加成）
    /// </summary>
    public float GetActualDecayRate()
    {
        return _baseDecayRate / (1f + _allyCount * _allyResistFactor);
    }

    /// <summary>
    /// 扣除血量，回傳是否死亡
    /// </summary>
    public bool ApplyDamage(float amount)
    {
        if (_state == BacteriaState.Dead) return false;

        _hp = Mathf.Max(0f, _hp - amount);
        OnHpChanged?.Invoke(HpRatio);

        if (_hp <= 0f)
        {
            State = BacteriaState.Dead;
            OnDied?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 目前移動速度（根據 State 決定）
    /// </summary>
    public float GetCurrentSpeed()
    {
        return _state switch
        {
            BacteriaState.UnderAttack => _slowedSpeed,
            BacteriaState.Fleeing => _fleeSpeed,
            _ => _normalSpeed
        };
    }
}
