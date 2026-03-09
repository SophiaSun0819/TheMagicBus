using UnityEngine;

public class WallSocket : MonoBehaviour
{
    public string fragmentTag = "Fragment";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(fragmentTag)) return;

        // 关键：先解除原父物体（飞船）
        other.transform.SetParent(null);

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 再吸附到墙
        other.transform.SetParent(transform, true);
        other.transform.localPosition = Vector3.zero;
        other.transform.localRotation = Quaternion.identity;

        Debug.Log("Fragment snapped to wall");
    }
}