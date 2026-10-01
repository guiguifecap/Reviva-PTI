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

    [Header("Bolas")]
    public AudioSource bolaSource;
    public AudioClip[] bolaSounds;
    public TextMeshProUGUI bolaVolumeText;

    [Header("Master")]
    public TextMeshProUGUI masterVolumeText;


    private Coroutine brisaCoroutine;

    //private void Awake()
    //{
    //    if (Instance == null)
    //    {
    //        Instance = this;
    //        DontDestroyOnLoad(gameObject);
    //    }
    //    else
    //    {
    //        Destroy(gameObject);
    //    }
    //}

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
    // PEDRAS
    // ─────────────────────────────────────────────

   // public void PlayRockGrab()
   // {
   //     if (pedrasSounds.Length == 0)
   //         return;
   //
   //     int random = Random.Range(0, pedrasSounds.Length);
   //
   //     pedrasSource.PlayOneShot(pedrasSounds[random]);
   // }

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

    public void SetBolaVolume(float value)
    {
        audioMixer.SetFloat("bolaVolume", value);
    }
    public void UpdatePedrasVolumeText(float value)
    {
        int percentage = Mathf.RoundToInt(Mathf.InverseLerp(-80f, 0f, value) * 100f);
        bolaVolumeText.text = percentage + "%";
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