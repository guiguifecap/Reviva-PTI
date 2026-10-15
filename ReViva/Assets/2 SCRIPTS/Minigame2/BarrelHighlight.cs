using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BarrelHighlight : MonoBehaviour
{
    [SerializeField] XRSimpleInteractable interactable;  // drag the barrel here
    [SerializeField] bool triggerOnHoverToo = false;     // tick this if grabbing doesn't count
    [SerializeField] float pulseSpeed = 4f;

    Outline[] outlines;
    bool done;

    void Awake()
    {
        if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
        outlines = GetComponentsInChildren<Outline>(true);

        Debug.Log("[BarrelHighlight] interactable: " + (interactable != null) + " | outlines: " + outlines.Length, this);
    }

    void Update()
    {
        if (!done && interactable != null &&
            (interactable.isSelected || (triggerOnHoverToo && interactable.isHovered)))
        {
            done = true;
            Debug.Log("[BarrelHighlight] Barrel grabbed!", this);
        }

        foreach (var o in outlines)
        {
            if (o == null) continue;

            if (done)
            {
                Color c = o.OutlineColor;
                c.a = 0f;
                o.OutlineColor = c;
                o.OutlineWidth = 0f;
            }
            else
            {
                o.OutlineWidth = 4f + Mathf.Sin(Time.time * pulseSpeed) * 1.5f;
            }
        }
    }
}