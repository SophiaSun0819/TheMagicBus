using UnityEngine;

public class BacteriaView : MonoBehaviour
{
    public void OnStateChanged(BacteriaState state)
    {
        // TODO: 根據 state 切換動畫 / 顏色
    }

    public void OnHpChanged(float hpRatio)
    {
        // TODO: 更新血條 UI
    }
}
