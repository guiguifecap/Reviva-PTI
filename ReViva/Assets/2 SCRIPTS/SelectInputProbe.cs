using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SelectInputProbe : MonoBehaviour
{
    private XRBaseInputInteractor interactor;
    private bool lastPerformed;

    private void Awake()
    {
        interactor = GetComponent<XRBaseInputInteractor>();
        if (interactor == null)
            Debug.LogError("[Probe] Este script precisa estar no mesmo objeto do Ray Interactor.", this);
    }

    private void Update()
    {
        if (interactor == null) return;

        bool performed = interactor.selectInput.ReadIsPerformed();

        if (performed != lastPerformed)
        {
            lastPerformed = performed;
            Debug.Log("[Probe] Select input = " + performed +
                      " | valor = " + interactor.selectInput.ReadValue() +
                      " | allowSelect = " + interactor.allowSelect +
                      " | hasSelection = " + interactor.hasSelection, this);
        }
    }
}