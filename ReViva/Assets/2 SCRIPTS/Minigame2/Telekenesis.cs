using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Telekinesis : MonoBehaviour
{
    [Header("References")]
    public Transform hand;
    public Camera vrCamera;

    [Header("Settings")]
    public float maxDistance = 15f;
    public float pullSpeed = 12f;
    public float stopDistance = 0.2f;

    [Header("Input")]
    public InputActionProperty grabAction;

    private Rigidbody target;
    private bool pulling;

    void OnEnable()
    {
        grabAction.action.Enable();
    }

    void OnDisable()
    {
        grabAction.action.Disable();
    }

    void Update()
    {
        if (grabAction.action.WasPressedThisFrame() && !pulling)
            TryGrab();

        if (pulling && target != null)
            PullObject();
    }

    void TryGrab()
    {
        Ray ray = new Ray(vrCamera.transform.position, vrCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            XRGrabInteractable grab = hit.collider.GetComponentInParent<XRGrabInteractable>();

            if (grab != null && grab.GetComponent<Rigidbody>() != null)
            {
                target = grab.GetComponent<Rigidbody>();

                target.useGravity = false;
                pulling = true;
            }
        }
    }

    void PullObject()
    {
        Vector3 direction = hand.position - target.position;

        target.linearVelocity = direction * pullSpeed;

        if (direction.magnitude <= stopDistance)
        {
            target.linearVelocity = Vector3.zero;
            target.useGravity = true;

            pulling = false;
            target = null;
        }
    }
}