using UnityEngine;

public enum BacteriaState
{
    Idle,           // 閒置，尚未被玩家注意
    Targeted,       // 被玩家視線鎖定
    UnderAttack,    // 正在被攻擊（長壓中）
    Fleeing,        // 逃跑中（視線中斷後）
    Dead            // 已被消滅
}
