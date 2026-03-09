using UnityEngine;

public class FragmentSlot : MonoBehaviour
{
    public FragmentType acceptType;
    private ShipMagnet shipMagnet;
    void Start()
{
    shipMagnet = GameObject.Find("SpaceshipCube").GetComponent<ShipMagnet>();
    if(shipMagnet!=null) Debug.Log("find ship");
}
   

    private void OnTriggerEnter(Collider other)
    {
        Fragment fragment = other.GetComponent<Fragment>();

        if (fragment != null)
        {
            if (fragment.type == acceptType)
            {
                
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
            else
            {
                Debug.Log("错误位置");
            }
        }
    }
}