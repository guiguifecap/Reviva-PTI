using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class AudioPedras : MonoBehaviour
{
    [Header("Haptics")]
    [Range(0f, 1f)] public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.15f;

    private ClimbInteractable climbInteractable;
    private bool isGrabbed;

    // Shared across all rocks
    private static int grabCount = 0;
    private static ContinuousMoveProvider[] moveProviders;
    private static ContinuousTurnProvider[] continuousTurnProviders;
    private static SnapTurnProvider[] snapTurnProviders;
    private static LocomotionProvider[] providers;

    private void Awake()
    {
        climbInteractable = GetComponent<ClimbInteractable>();
    }

    private void OnEnable()
    {
        climbInteractable.selectEntered.AddListener(OnClimb);
        climbInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        climbInteractable.selectEntered.RemoveListener(OnClimb);
        climbInteractable.selectExited.RemoveListener(OnRelease);

        // Safety: if the rock gets disabled while held, don't leave locomotion locked
        if (isGrabbed)
        {
            isGrabbed = false;
            grabCount = Mathf.Max(0, grabCount - 1);
            if (grabCount == 0) SetLocomotionEnabled(true);
        }
    }

    private void OnClimb(SelectEnterEventArgs args)
    {
        Debug.Log("[AudioPedras] OnClimb disparou em: " + name, this);

        if (AudioManagerMinigame1.Instance == null)
            Debug.LogError("[AudioPedras] AudioManagerMinigame1.Instance é NULL! Não existe na cena ou não definiu o Instance.");
        else
        {
            Debug.Log("[AudioPedras] Chamando PlayRockGrab()");
            AudioManagerMinigame1.Instance.PlayRockGrab();
        }

        if (args.interactorObject is XRBaseInputInteractor controllerInteractor)
            controllerInteractor.SendHapticImpulse(hapticIntensity, hapticDuration);

        isGrabbed = true;
        grabCount++;
        //if (grabCount == 1) SetLocomotionEnabled(false);


        if (Timer.Instance != null)
            Timer.Instance.StartTimer();
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (!isGrabbed) return;

        isGrabbed = false;
        grabCount = Mathf.Max(0, grabCount - 1);
        if (grabCount == 0) SetLocomotionEnabled(true);
    }

    private static void SetLocomotionEnabled(bool value)
    {
        if (providers == null || providers.Length == 0)
            providers = FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int count = 0;
        foreach (var p in providers)
        {
            if (p == null || p is ClimbProvider) continue; // never disable climbing itself
            p.enabled = value;
            count++;
            Debug.Log($"[AudioPedras] {(value ? "ENABLED" : "DISABLED")} {p.GetType().Name} on {p.name}");
        }

        if (count == 0)
            Debug.LogWarning("[AudioPedras] No locomotion providers found to toggle!");
    }

    // Reset static state when entering play mode / reloading the scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        grabCount = 0;
        moveProviders = null;
        continuousTurnProviders = null;
        snapTurnProviders = null;
    }
}