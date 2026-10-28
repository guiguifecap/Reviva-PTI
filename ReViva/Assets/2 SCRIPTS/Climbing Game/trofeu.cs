using System.Collections;
using UnityEngine;

public class trofeu : MonoBehaviour
{
    public GameObject fogos;
    public float duracaoFogos = 8f;
    public float fadeOutDuracao = 1.5f;

    public bool jaAtivado = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (jaAtivado) return;

        if (collision.gameObject.CompareTag("Mao"))
        {
            jaAtivado = true;

            if (fogos != null)
                fogos.SetActive(true);

            if (AudioManagerMinigame1.Instance != null)
            {
                AudioManagerMinigame1.Instance.PlayTrophy();
                AudioManagerMinigame1.Instance.PlayFireworks(duracaoFogos, fadeOutDuracao);
            }

            StartCoroutine(StopFogosDepois());
        }
    }

    private IEnumerator StopFogosDepois()
    {
        float fade = Mathf.Min(fadeOutDuracao, duracaoFogos);

        // keep going until the fade starts
        yield return new WaitForSeconds(duracaoFogos - fade);

        // stop emitting new particles, existing ones finish naturally
        if (fogos != null)
        {
            foreach (var ps in fogos.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // audio fades during this same window
        yield return new WaitForSeconds(fade);

        if (fogos != null)
            fogos.SetActive(false);

        if (AudioManagerMinigame1.Instance != null)
            AudioManagerMinigame1.Instance.StopFireworks();
    }
}