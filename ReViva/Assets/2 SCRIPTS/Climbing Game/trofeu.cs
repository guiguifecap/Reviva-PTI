using UnityEngine;

public class trofeu : MonoBehaviour
{
    public GameObject fogos;

    private bool jaAtivado = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (jaAtivado) return;

        if (collision.gameObject.CompareTag("Mao"))
        {
            jaAtivado = true;

            if (fogos != null)
                fogos.SetActive(true);

            if (AudioManagerMinigame1.Instance != null)
                AudioManagerMinigame1.Instance.PlayTrophy();
        }
    }
}