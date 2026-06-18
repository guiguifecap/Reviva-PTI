using UnityEngine;

public class Degrais : MonoBehaviour
{
    public static Degrais Instance;

    [Header("Referências de Cena")]
    public Transform posicaoComeco;
    public Transform posicaoFinal;
    public GameObject[] pedras;

    [Header("Layer da Montanha")]
    public LayerMask layerMontanha;

    [Header("Distância horizontal das pedras")]
    public float offsetHorizontal = 0.30f;

    [Header("Distância do raycast")]
    public float distanciaRaycast = 20f;

    void Awake()
    {
        Instance = this;
    }

    // ─────────────────────────────────────────────
    // GERAR PEDRAS
    // ─────────────────────────────────────────────
    public void GerarDegraus()
    {
        // limpa antigas
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (posicaoComeco == null || posicaoFinal == null)
        {
            Debug.LogError("Posições não configuradas.");
            return;
        }

        // alcance calibrado
        float alcanceCM = GameSettings.Instance.alcanceMaximoCM;

        if (GameSettings.Instance.usarMetadeDoAlcance)
        {
            alcanceCM *= 0.5f;
        }

        if (alcanceCM <= 0f)
            alcanceCM = 130f;

        // dificuldade agora é percentual direto
        float percentual = GameSettings.Instance.difficulty;

        // cm → metros
        float alcanceMetros = alcanceCM / 100f;

        // distância entre pedras
        float distanciaVertical = alcanceMetros * percentual;

        Debug.Log(
            $"[Degrais] Alcance={alcanceCM:F1}cm  " +
            $"Dificuldade={percentual * 100f:F0}%  " +
            $"Distância={distanciaVertical:F2}m"
        );

        float y = posicaoComeco.position.y;
        float alturaFinal = posicaoFinal.position.y;

        float centroX = posicaoComeco.position.x;
        float centroZ = posicaoComeco.position.z;

        int index = 0;

        while (y < alturaFinal)
        {
            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            // alterna lados
            float x = (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 centroBusca = new Vector3(x, y, centroZ);

            bool encontrou = false;
            RaycastHit hit = new RaycastHit();

            // ─────────────────────────────────────────────
            // RAYCASTS 4 DIREÇÕES
            // ─────────────────────────────────────────────

            Vector3[] direcoes =
            {
                Vector3.forward,
                Vector3.back,
                Vector3.right,
                Vector3.left
            };

            foreach (Vector3 dir in direcoes)
            {
                Vector3 origem = centroBusca - dir * 5f;

                if (Physics.Raycast(
                    origem,
                    dir,
                    out hit,
                    distanciaRaycast,
                    layerMontanha
                ))
                {
                    encontrou = true;
                    break;
                }
            }

            // ─────────────────────────────────────────────
            // SE ACHOU SUPERFÍCIE
            // ─────────────────────────────────────────────

            if (encontrou)
            {
                Vector3 posicaoPedra = hit.point;

                // empurra levemente pra fora
                posicaoPedra += hit.normal * 0.03f;

                // rotação alinhada na parede
                Quaternion rot =
                    Quaternion.LookRotation(-hit.normal);

                if (pedras.Length == 0) return;

                GameObject pedraEscolhida = pedras[Random.Range(0, pedras.Length)];

                Instantiate(
                    pedraEscolhida,
                    posicaoPedra,
                    rot,
                    transform
                );

                index++;
            }
        }

        Debug.Log($"[Degrais] {index} pedras geradas.");
    }

    // ─────────────────────────────────────────────
    // DEBUG VISUAL
    // ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (posicaoComeco == null || posicaoFinal == null)
            return;

        Gizmos.color = Color.green;

        float alcanceCM = Application.isPlaying
            ? GameSettings.Instance.alcanceMaximoCM
            : 130f;

        float percentual = Application.isPlaying
            ? GameSettings.Instance.difficulty
            : 0.8f;

        float distanciaVertical =
            (alcanceCM / 100f) * percentual;

        float y = posicaoComeco.position.y;
        float alturaFinal = posicaoFinal.position.y;

        float centroX = posicaoComeco.position.x;
        float centroZ = posicaoComeco.position.z;

        int index = 0;

        while (y < alturaFinal)
        {
            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            float x = (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 p = new Vector3(x, y, centroZ);

            Gizmos.DrawWireSphere(p, 0.08f);

            index++;
        }
    }
}