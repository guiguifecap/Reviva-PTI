using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

public class AudioPedras : MonoBehaviour
{
    private ClimbInteractable climbInteractable;

    private void Awake()
    {
        climbInteractable = GetComponent<ClimbInteractable>();
    }

    private void OnEnable()
    {
        climbInteractable.selectEntered.AddListener(OnClimb);
    }

    private void OnDisable()
    {
        climbInteractable.selectEntered.RemoveListener(OnClimb);
    }

    private void OnClimb(SelectEnterEventArgs args)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayRockGrab();
    }
}
