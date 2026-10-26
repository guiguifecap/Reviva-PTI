using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManagerMinigame2 : MonoBehaviour
{
    public static AudioManagerMinigame2 Instance;

    [Header("Mixer")]
    public AudioMixer audioMixer;

    [Header("Som de Fundo")]
    public AudioSource brisaSource;
    public AudioClip[] brisaSounds;
    public TextMeshProUGUI brisaVolumeText;

    [Header("Passos")]
    public AudioSource walkingSource;
    public AudioClip[] walkingSounds;
    public TextMeshProUGUI walkingVolumeText;

    [Header("Alvos")]
    [Tooltip("Usado apenas pelo mixer group (para o slider alvoVolume funcionar)")]
    public AudioSource alvoSource;
    public AudioClip[] alvoSounds;
    public TextMeshProUGUI alvoVolumeText;
    [Range(0f, 1f)] public float alvoBreakVolume = 1f;
    [Tooltip("0 = som 2D, 1 = som 3D (some com a distância)")]
    [Range(0f, 1f)] public float alvoSpatialBlend = 0.5f;
    [Tooltip("Varia levemente o tom a cada quebra (0 = sem variação)")]
    [Range(0f, 0.5f)] public float alvoPitchVariation = 0.1f;

    [Header("Vitória")]
    [Tooltip("Som tocado quando o jogador acerta todos os alvos.")]
    public AudioClip somVitoria;
    [Tooltip("AudioSource usado para tocar o som. Se vazio, é criado automaticamente.")]
    public AudioSource vitoriaSource;
    [Range(0f, 1f)] public float volumeVitoria = 1f;
    public AudioClip somFogos;
    public AudioSource fogosSource;
    [Range(0f, 1f)] public float volumeFogos = 1f;

    [Header("Master")]
    public TextMeshProUGUI masterVolumeText;


    private Coroutine brisaCoroutine;

    private void Awake()
    {
        // Manager por cena, então sem DontDestroyOnLoad
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        brisaCoroutine = StartCoroutine(brisaSoundsLoop());
    }

    // ─────────────────────────────────────────────
    // FOREST
    // ─────────────────────────────────────────────

    private IEnumerator brisaSoundsLoop()
    {
        while (true)
        {
            if (brisaSounds.Length > 0)
            {
                int random = Random.Range(0, brisaSounds.Length);

                brisaSource.clip = brisaSounds[random];
                brisaSource.Play();

                yield return new WaitForSeconds(brisaSource.clip.length);
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
    // ALVOS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called by WaterTarget when it breaks. Plays a random alvo sound at the given position.
    /// </summary>
    public void PlayAlvoBreak(Vector3 position)
    {
        if (alvoSounds == null || alvoSounds.Length == 0)
            return;

        AudioClip clip = alvoSounds[Random.Range(0, alvoSounds.Length)];
        if (clip == null) return;

        // Objeto temporário: continua tocando mesmo com o alvo voltando para o pool
        GameObject go = new GameObject("AlvoBreakSound");
        go.transform.position = position;

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = alvoBreakVolume;
        src.spatialBlend = alvoSpatialBlend;
        src.pitch = 1f + Random.Range(-alvoPitchVariation, alvoPitchVariation);
        src.playOnAwake = false;

        // Usa o mesmo mixer group do alvoSource para o slider de volume funcionar
        if (alvoSource != null)
            src.outputAudioMixerGroup = alvoSource.outputAudioMixerGroup;

        src.Play();

        Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
    }

    // ─────────────────────────────────────────────
    // VITÓRIA
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called by MenuUI_Minigame2 when the player hits all targets.
    /// </summary>
    public void PlayVitoria()
    {
        if (somVitoria == null) return;

        if (vitoriaSource == null)
        {
            vitoriaSource = gameObject.GetComponent<AudioSource>();
            if (vitoriaSource == null)
                vitoriaSource = gameObject.AddComponent<AudioSource>();
            vitoriaSource.playOnAwake = false;
        }

        vitoriaSource.PlayOneShot(somVitoria, volumeVitoria);
    }

    public void PlayFireworks()
    {
        if (somFogos == null) return;

        if (fogosSource == null)
        {
            fogosSource = gameObject.GetComponent<AudioSource>();
            if (fogosSource == null)
                fogosSource = gameObject.AddComponent<AudioSource>();
            fogosSource.playOnAwake = false;
        }

        fogosSource.PlayOneShot(somFogos, volumeFogos);
    }
    // ─────────────────────────────────────────────
    // VOLUME
    // ─────────────────────────────────────────────

    public void SetBrisaVolume(float value)
    {
        audioMixer.SetFloat("brisaVolume", value);
    }
    public void UpdateBrisaVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        brisaVolumeText.text = percentage + "%";
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

    public void SetAlvoVolume(float value)
    {
        audioMixer.SetFloat("alvoVolume", value);
    }
    public void UpdateAlvoVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        alvoVolumeText.text = percentage + "%";
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