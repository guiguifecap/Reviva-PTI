using UnityEngine;

public class BarrelHighlight : MonoBehaviour
{
    [SerializeField] float pulseSpeed = 4f;

    Outline[] outlines;

    void Awake()
    {
        outlines = GetComponentsInChildren<Outline>(true);
    }

    void Update()
    {
        float width = 7f + Mathf.Sin(Time.time * pulseSpeed) * 1.5f;
        foreach (var o in outlines)
            if (o != null) o.OutlineWidth = width;
    }
}