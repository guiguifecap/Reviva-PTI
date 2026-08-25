using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Mixer")]
    public AudioMixer audioMixer;

    [Header("Forest")]
    public AudioSource forestSource;
    public AudioClip[] forestSounds;
    public TextMeshProUGUI forestVolumeText;

    [Header("Walking")]
    public AudioSource walkingSource;
    public AudioClip[] walkingSounds;
    public TextMeshProUGUI walkingVolumeText;

    [Header("Master")]
    public TextMeshProUGUI masterVolumeText;

    private Coroutine forestCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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
            if (forestSounds.Length > 0)
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
    // WALKING
    // ─────────────────────────────────────────────

    public void PlayFootstep()
    {
        if (walkingSounds.Length == 0)
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