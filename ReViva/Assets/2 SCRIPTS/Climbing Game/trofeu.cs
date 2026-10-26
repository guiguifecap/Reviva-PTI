using System.Collections;
using UnityEngine;

public class trofeu : MonoBehaviour
{
    public GameObject fogos;
    public float duracaoFogos = 8f;

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
                AudioManagerMinigame1.Instance.PlayFireworks(duracaoFogos);
            }

            StartCoroutine(StopFogosDepois());
        }
    }

    private IEnumerator StopFogosDepois()
    {
        yield return new WaitForSeconds(duracaoFogos);

        if (fogos != null)
            fogos.SetActive(false);

        if (AudioManagerMinigame1.Instance != null)
            AudioManagerMinigame1.Instance.StopFireworks();
    }
}