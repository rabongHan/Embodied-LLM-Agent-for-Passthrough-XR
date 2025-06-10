using UnityEngine;

public class EnablePhysicsOnGrip : MonoBehaviour
{
    Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // start kinematic & no gravity
        _rb.isKinematic = true;
        _rb.useGravity  = false;
    }

    void Update()
    {
        // Left‐hand grip button down?
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger))
        {
            _rb.isKinematic = false;
            _rb.useGravity  = true;
        }
    }
}
