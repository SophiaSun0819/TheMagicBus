using System;
using UnityEngine;

#if !UNITY_EDITOR
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.InputSystem;
#endif

public class InputController : MonoBehaviour
{
    // Events
    public event Action OnTriggerHoldStarted;   // 開始長壓
    public event Action OnTriggerHoldCanceled;  // 放開按鍵
    public event Action OnTriggerPressed;       // 每次按下（用於連按）

#if !UNITY_EDITOR
    [Header("XRI Input Action")]
    [SerializeField] private InputActionReference _triggerAction;  // 拖入 XRI Trigger Action
#endif

    private bool _wasPressed = false;

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Update()
    {
#if UNITY_EDITOR
        HandleMouseInput();
#else
        HandleXRInput();
#endif
    }

    // ── Editor：滑鼠左鍵模擬 Trigger ──────────────────────────────────────────

#if UNITY_EDITOR
    private void HandleMouseInput()
    {
        bool isPressed = Input.GetMouseButton(0);
 
        if (Input.GetMouseButtonDown(0))
        {
            OnTriggerHoldStarted?.Invoke();
            OnTriggerPressed?.Invoke();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            OnTriggerHoldCanceled?.Invoke();
        }
        else if (isPressed && !_wasPressed)
        {
            OnTriggerPressed?.Invoke();
        }
 
        _wasPressed = isPressed;
    }
#endif

    // ── XRI：食指 Trigger ──────────────────────────────────────────

#if !UNITY_EDITOR
    private void HandleXRInput()
    {
        if (_triggerAction == null) return;

        float triggerValue = _triggerAction.action.ReadValue<float>();
        bool isPressed = triggerValue > 0.8f;   // 0.8 以上才算按下

        if (isPressed && !_wasPressed)
        {
            OnTriggerHoldStarted?.Invoke();
            OnTriggerPressed?.Invoke();
        }
        else if (!isPressed && _wasPressed)
        {
            OnTriggerHoldCanceled?.Invoke();
        }

        _wasPressed = isPressed;
    }
#endif
}
