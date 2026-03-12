/// <summary>
/// 可被玩家視線偵測的物件介面
/// GazeController 透過此介面通知目標，不需要知道目標的具體類型
/// </summary>
public interface IGazeable
{
    /// <summary>
    /// 視線進入時呼叫
    /// </summary>
    void OnGazeEnter();

    /// <summary>
    /// 視線離開時呼叫
    /// </summary>
    void OnGazeExit();
}

/// <summary>
/// 可被玩家攻擊的物件介面
/// 未來若有其他可攻擊目標（病毒、Boss）只需實作此介面
/// </summary>
public interface IAttackable
{
    /// <summary>
    /// 每幀持續扣血（長壓期間呼叫）
    /// </summary>
    void TakeDamage(float amount);

    /// <summary>
    /// 單次按壓的額外傷害（連按突破同伴保護）
    /// </summary>
    void TakePressDamage();

    /// <summary>
    /// 觸發逃跑（視線中斷或長壓不足時呼叫）
    /// </summary>
    void TriggerFlee();
}