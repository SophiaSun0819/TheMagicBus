using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class ShipMagnet : MonoBehaviour
{
    public InputActionReference leftTrigger;
    public InputActionReference rightTrigger;
    public Transform attachPoint;

    private GameObject candidate;
    private GameObject attachedFragment;
   private UnityEngine.XR.InputDevice leftDevice;
private UnityEngine.XR.InputDevice rightDevice;
private float hapticTimer;

    private void OnEnable()
    {
        leftTrigger?.action?.Enable();
        rightTrigger?.action?.Enable();
    }

    private void OnDisable()
    {
        leftTrigger?.action?.Disable();
        rightTrigger?.action?.Disable();
    }
    private void Start()
{
    leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
    rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
}

private void SendHaptics(float amplitude, float duration)
{
    if (!leftDevice.isValid)
        leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

    if (!rightDevice.isValid)
        rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

    if (leftDevice.isValid)
        leftDevice.SendHapticImpulse(0, amplitude, duration);

    if (rightDevice.isValid)
        rightDevice.SendHapticImpulse(0, amplitude, duration);
}
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fragment") && attachedFragment == null)
        {
            candidate = other.gameObject;
            Debug.Log("Fragment in range");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == candidate)
            candidate = null;
    }

    private void Update()
    {
        bool leftPressed  = leftTrigger.action.ReadValue<float>() > 0.1f;
        bool rightPressed = rightTrigger.action.ReadValue<float>() > 0.1f;

        // ===== 吸附逻辑 =====
        if (attachedFragment == null && candidate != null)
        {
            if (leftPressed && rightPressed)
            {
                AttachFragment(candidate);
            }
        }

        // ===== 掉落逻辑 =====
        if (attachedFragment != null)
        {
            if (!leftPressed && !rightPressed)
            {
                ReleaseFragment();
            }
        }
        if (attachedFragment != null)
{
    hapticTimer -= Time.deltaTime;

    if (hapticTimer <= 0f)
    {
        SendHaptics(0.4f, 0.1f);
        hapticTimer = 0.1f; // 每 0.1 秒发一次
    }
}
    }

    public void AttachFragment(GameObject fragment)
    {
        // SendHaptics(0.6f, 0.15f);
        Rigidbody rb = fragment.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;      // 修正 linearVelocity
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        fragment.transform.SetParent(attachPoint, true);
        fragment.transform.localPosition = Vector3.zero;
        fragment.transform.localRotation = Quaternion.identity;

        attachedFragment = fragment;
        candidate = null;

        Debug.Log("Fragment attached");
        
    }

    public void ReleaseFragment()
    {
        Rigidbody rb = attachedFragment.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        attachedFragment.transform.SetParent(null);

        Debug.Log("Fragment released");

        attachedFragment = null;
    }
}