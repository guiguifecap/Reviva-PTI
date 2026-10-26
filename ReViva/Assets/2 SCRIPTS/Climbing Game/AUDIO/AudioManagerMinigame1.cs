using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManagerMinigame1 : MonoBehaviour
{
    public static AudioManagerMinigame1 Instance;

    [Header("Mixer")]
    public AudioMixer audioMixer;

    [Header("Floresta")]
    public AudioSource forestSource;
    public AudioClip[] forestSounds;
    public TextMeshProUGUI forestVolumeText;

    [Header("Passos")]
    public AudioSource walkingSource;
    public AudioClip[] walkingSounds;
    public TextMeshProUGUI walkingVolumeText;

    [Header("Pedras")]
    public AudioSource pedrasSource;
    public AudioClip[] pedrasSounds;
    public TextMeshProUGUI pedrasVolumeText;

    [Header("Troféu")]
    public AudioSource trofeuSource;
    public AudioClip somTrofeu;
    [Range(0f, 1f)] public float volumeTrofeu = 1f;

    [Header("Fogo de Artifício")]
    public AudioSource fireworksSource;
    public AudioClip[] fireworksSounds;

    [Header("Master")]
    public TextMeshProUGUI masterVolumeText;

    private Coroutine forestCoroutine;

    // Reset the static when entering play mode / reloading the scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Uncomment only if you want this manager to persist across scenes:
        // DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        forestCoroutine = StartCoroutine(ForestSoundsLoop());
    }

    // ─────────────────────────────────────────────
    // FOREST
    // ─────────────────────────────────────────────

    private IEnumerator ForestSoundsLoop()
    {
        while (true)
        {
            if (forestSource != null && forestSounds.Length > 0)
            {
                int random = Random.Range(0, forestSounds.Length);

                forestSource.clip = forestSounds[random];
                forestSource.Play();

                yield return new WaitForSeconds(forestSource.clip.length);
            }
            else
            {
                yield return null;
            }
        }
    }

    // ─────────────────────────────────────────────
    // PEDRAS
    // ─────────────────────────────────────────────

    public void PlayRockGrab()
    {
        if (pedrasSounds == null || pedrasSounds.Length == 0)
        {
            Debug.LogWarning("[AudioManager] pedrasSounds está vazio.", this);
            return;
        }

        if (pedrasSource == null)
        {
            Debug.LogError("[AudioManager] pedrasSource não está atribuído.", this);
            return;
        }

        int random = Random.Range(0, pedrasSounds.Length);
        Debug.Log("[AudioManager] A tocar: " + pedrasSounds[random].name);
        pedrasSource.PlayOneShot(pedrasSounds[random]);
    }

    // ─────────────────────────────────────────────
    // TROFÉU
    // ─────────────────────────────────────────────

    public void PlayTrophy()
    {
        if (somTrofeu == null)
        {
            Debug.LogWarning("[AudioManager] somTrofeu não está atribuído.", this);
            return;
        }

        if (trofeuSource == null)
        {
            Debug.LogError("[AudioManager] trofeuSource não está atribuído.", this);
            return;
        }

        trofeuSource.PlayOneShot(somTrofeu, volumeTrofeu);
    }

    // ─────────────────────────────────────────────
    // FOGO DE ARTIFÍCIO
    // ─────────────────────────────────────────────

    public void PlayFireworks()
    {
        if (fireworksSounds == null || fireworksSounds.Length == 0)
        {
            Debug.LogWarning("[AudioManager] fireworksSounds está vazio.", this);
            return;
        }

        if (fireworksSource == null)
        {
            Debug.LogError("[AudioManager] fireworksSource não está atribuído.", this);
            return;
        }

        int random = Random.Range(0, fireworksSounds.Length);
        fireworksSource.PlayOneShot(fireworksSounds[random]);
    }

    // ─────────────────────────────────────────────
    // WALKING
    // ─────────────────────────────────────────────

    public void PlayFootstep()
    {
        if (walkingSource == null || walkingSounds.Length == 0)
            return;

        int random = Random.Range(0, walkingSounds.Length);

        walkingSource.clip = walkingSounds[random];
        walkingSource.Play();
    }

    // ─────────────────────────────────────────────
    // VOLUME
    // ─────────────────────────────────────────────

    public void SetForestVolume(float value)
    {
        audioMixer.SetFloat("ForestVolume", value);
    }
    public void UpdateForestVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        forestVolumeText.text = percentage + "%";
    }

    public void SetWalkingVolume(float value)
    {
        audioMixer.SetFloat("WalkingVolume", value);
    }
    public void UpdateWalkingVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        walkingVolumeText.text = percentage + "%";
    }

    public void SetPedrasVolume(float value)
    {
        audioMixer.SetFloat("pedrasVolume", value); // must match the exposed parameter name exactly
    }
    public void UpdatePedrasVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        pedrasVolumeText.text = percentage + "%";
    }

    public void SetMasterVolume(float value)
    {
        audioMixer.SetFloat("MasterVolume", value);
    }
    public void UpdateMasterVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        masterVolumeText.text = percentage + "%";
    }
}