using UnityEngine;

public class Degrais : MonoBehaviour
{
    public static Degrais Instance;

    [Header("Referências de Cena")]
    public Transform posicaoComeco;
    public Transform posicaoFinal;
    public GameObject[] pedras;

    [Header("Séries (repetições)")]
    [Tooltip("Prefab do ponto de descanso, instanciado no lugar de uma pedra normal.")]
    public GameObject pedraDescanso;
    [Tooltip("A cada quantas pedras geradas aparece um ponto de descanso (fim de uma série).")]
    public int pedrasPorDescanso;
    [Tooltip("Quantidade total de séries. Ao completar a última série, o ponto de descanso vira a plataforma final e a geração termina ali.")]
    public int numeroDeSeries;
    [Tooltip("Prefab da plataforma final. Se ficar vazio, usa o próprio 'Pedra Descanso' na última série.")]
    public GameObject plataformaFinal;

    [Header("Layer da Montanha")]
    public LayerMask layerMontanha;

    [Header("Distância horizontal das pedras")]
    public float offsetHorizontal = 0.30f;

    [Header("Distância do raycast")]
    public float distanciaRaycast = 20f;

    [Header("Segurança")]
    [Tooltip("Distância vertical mínima aceita entre pedras. Evita loop infinito se a dificuldade/alcance vierem zerados.")]
    public float distanciaVerticalMinima = 0.05f;
    [Tooltip("Número máximo de pedras que o gerador pode tentar criar, como trava de segurança.")]
    public int maxPedras = 500;

    // array de direções fixo, criado uma única vez (evita alocação repetida no loop)
    static readonly Vector3[] Direcoes =
    {
        Vector3.forward,
        Vector3.back,
        Vector3.right,
        Vector3.left
    };

    void Awake()
    {
        Instance = this;
    }

    // ─────────────────────────────────────────────
    // GERAR PEDRAS
    // ─────────────────────────────────────────────
    public void GerarDegraus()
    {
        LimparFilhos();

        if (posicaoComeco == null || posicaoFinal == null)
        {
            Debug.LogError("[Degrais] Posições não configuradas.");
            return;
        }

        if (pedras == null || pedras.Length == 0)
        {
            Debug.LogError("[Degrais] Array 'pedras' está vazio.");
            return;
        }

        if (numeroDeSeries <= 0)
        {
            Debug.LogError("[Degrais] 'Numero De Series' precisa ser maior que zero.");
            return;
        }

        if (!TentarObterDistanciaVertical(out float distanciaVertical))
            return;

        float y = posicaoComeco.position.y;
        float alturaFinal = posicaoFinal.position.y;

        float centroX = posicaoComeco.position.x;
        float centroZ = posicaoComeco.position.z;

        int index = 0;
        int contadorDesdeDescanso = 0;
        int serieAtual = 0;
        int seguranca = 0;
        bool plataformaFinalGerada = false;

        // 'seguranca' garante que o loop termina mesmo que algo
        // inesperado aconteça com 'y' ou 'alturaFinal'
        while (y < alturaFinal && seguranca < maxPedras)
        {
            seguranca++;
            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            // alterna lados
            float x = (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 centroBusca = new Vector3(x, y, centroZ);

            if (!TentarEncontrarSuperficie(centroBusca, out RaycastHit hit))
                continue;

            Vector3 posicaoPedra = hit.point + hit.normal * 0.03f;
            Quaternion rotSuperficie = Quaternion.LookRotation(-hit.normal);

            contadorDesdeDescanso++;
            int numeroNestaPedra = contadorDesdeDescanso; // posição desta pedra dentro da série atual (1-indexed)

            bool ehPontoDeDescanso =
                pedraDescanso != null &&
                pedrasPorDescanso > 0 &&
                contadorDesdeDescanso >= pedrasPorDescanso;

            GameObject prefabEscolhido;

            if (ehPontoDeDescanso)
            {
                serieAtual++;
                bool ehUltimaSerie = serieAtual >= numeroDeSeries;

                if (ehUltimaSerie)
                {
                    // última série: vira a plataforma final
                    prefabEscolhido = plataformaFinal != null ? plataformaFinal : pedraDescanso;
                    plataformaFinalGerada = true;
                }
                else
                {
                    prefabEscolhido = pedraDescanso;
                }

                contadorDesdeDescanso = 0;
            }
            else
            {
                prefabEscolhido = pedras[Random.Range(0, pedras.Length)];
            }

            // Pontos de descanso e a plataforma final mantêm a rotação original
            // definida no próprio prefab, ignorando a normal da superfície.
            // As pedras normais continuam se alinhando à montanha via raycast.
            Quaternion rotFinal = ehPontoDeDescanso
                ? prefabEscolhido.transform.rotation
                : rotSuperficie;

            GameObject instancia = Instantiate(prefabEscolhido, posicaoPedra, rotFinal, transform);
            ConfigurarProgresso(instancia, numeroNestaPedra, pedrasPorDescanso, ehPontoDeDescanso);
            index++;

            // ao gerar a plataforma final, a geração termina imediatamente
            if (plataformaFinalGerada)
                break;
        }

        if (!plataformaFinalGerada)
        {
            Debug.LogWarning(
                $"[Degrais] A geração terminou (altura ou limite de segurança) antes de completar " +
                $"as {numeroDeSeries} séries pedidas. Séries completas: {serieAtual}. " +
                "Ajuste 'numeroDeSeries', 'pedrasPorDescanso' ou a distância entre 'posicaoComeco' e 'posicaoFinal'."
            );
        }

        if (seguranca >= maxPedras)
        {
            Debug.LogWarning(
                "[Degrais] Geração interrompida pelo limite de segurança " +
                $"({maxPedras} pedras). Verifique 'distanciaVertical', " +
                "'posicaoComeco' e 'posicaoFinal'."
            );
        }

        Debug.Log($"[Degrais] {index} pedras geradas em {serieAtual} série(s).");
    }

    // ─────────────────────────────────────────────
    // CONFIGURA O COMPONENTE DE PROGRESSO (UI "1/15")
    // ─────────────────────────────────────────────
    void ConfigurarProgresso(GameObject instancia, int numeroNaSerie, int totalNaSerie, bool ehDescanso)
    {
        PedraProgresso progresso = instancia.GetComponent<PedraProgresso>();
        if (progresso == null)
            progresso = instancia.AddComponent<PedraProgresso>();

        progresso.numeroNaSerie = numeroNaSerie;
        progresso.totalNaSerie = totalNaSerie;
        progresso.ehPontoDeDescanso = ehDescanso;
    }

    // ─────────────────────────────────────────────
    // LIMPA FILHOS (funciona em Play Mode e no Editor)
    // ─────────────────────────────────────────────
    void LimparFilhos()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
                continue;
            }
#endif
            Destroy(child.gameObject);
        }
    }

    // ─────────────────────────────────────────────
    // CALCULA E VALIDA A DISTÂNCIA VERTICAL
    // ─────────────────────────────────────────────
    bool TentarObterDistanciaVertical(out float distanciaVertical)
    {
        distanciaVertical = 0f;

        if (GameSettings.Instance == null)
        {
            Debug.LogError("[Degrais] GameSettings.Instance é nulo. Abortando geração.");
            return false;
        }

        float alcanceCM = GameSettings.Instance.alcanceMaximoCM;

        if (GameSettings.Instance.usarMetadeDoAlcance)
            alcanceCM *= 0.5f;

        if (alcanceCM <= 0f)
            alcanceCM = 130f;

        float percentual = GameSettings.Instance.difficulty;
        float alcanceMetros = alcanceCM / 100f;

        distanciaVertical = alcanceMetros * percentual;

        Debug.Log(
            $"[Degrais] Alcance={alcanceCM:F1}cm  " +
            $"Dificuldade={percentual * 100f:F0}%  " +
            $"Distância={distanciaVertical:F2}m"
        );

        if (distanciaVertical < distanciaVerticalMinima)
        {
            Debug.LogError(
                $"[Degrais] Distância vertical calculada ({distanciaVertical:F3}m) é menor que o " +
                $"mínimo permitido ({distanciaVerticalMinima:F3}m). Isso causaria um loop infinito, " +
                "então a geração foi cancelada. Verifique 'difficulty' e 'alcanceMaximoCM' em GameSettings."
            );
            return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────
    // RAYCASTS 4 DIREÇÕES
    // ─────────────────────────────────────────────
    bool TentarEncontrarSuperficie(Vector3 centroBusca, out RaycastHit hit)
    {
        hit = new RaycastHit();

        foreach (Vector3 dir in Direcoes)
        {
            Vector3 origem = centroBusca - dir * 5f;

            if (Physics.Raycast(origem, dir, out hit, distanciaRaycast, layerMontanha))
                return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────
    // DEBUG VISUAL
    // ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (posicaoComeco == null || posicaoFinal == null)
            return;

        float alcanceCM = 130f;
        float percentual = 0.8f;

        if (Application.isPlaying && GameSettings.Instance != null)
        {
            alcanceCM = GameSettings.Instance.alcanceMaximoCM;
            percentual = GameSettings.Instance.difficulty;
        }

        float distanciaVertical = (alcanceCM / 100f) * percentual;

        // mesma trava de segurança do modo de jogo: se a distância for
        // inválida, não tenta desenhar (evitaria travar o Editor)
        if (distanciaVertical < distanciaVerticalMinima)
            return;

        float y = posicaoComeco.position.y;
        float alturaFinal = posicaoFinal.position.y;

        float centroX = posicaoComeco.position.x;
        float centroZ = posicaoComeco.position.z;

        int index = 0;
        int contadorDesdeDescanso = 0;
        int serieAtual = 0;
        int seguranca = 0;

        while (y < alturaFinal && seguranca < maxPedras)
        {
            seguranca++;
            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            float x = (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 p = new Vector3(x, y, centroZ);

            contadorDesdeDescanso++;
            bool ehPontoDeDescanso =
                pedrasPorDescanso > 0 &&
                contadorDesdeDescanso >= pedrasPorDescanso;

            bool ehUltimaSerie = false;

            if (ehPontoDeDescanso)
            {
                serieAtual++;
                ehUltimaSerie = numeroDeSeries > 0 && serieAtual >= numeroDeSeries;
                contadorDesdeDescanso = 0;
            }

            if (ehUltimaSerie)
                Gizmos.color = Color.red;
            else if (ehPontoDeDescanso)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.green;

            float raio = ehUltimaSerie ? 0.16f : (ehPontoDeDescanso ? 0.12f : 0.08f);
            Gizmos.DrawWireSphere(p, raio);

            index++;

            if (ehUltimaSerie)
                break;
        }
    }
}