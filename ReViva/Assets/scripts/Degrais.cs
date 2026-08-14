using UnityEngine;
using TMPro;

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

    [Tooltip("Prefab da plataforma final. Se ficar vazio, usa o próprio 'Pedra Descanso' na última série.")]
    public GameObject plataformaFinal;

    // ─────────────────────────────────────────────
    // NÃO SERIALIZADOS DE PROPÓSITO: não aparecem no Inspector,
    // então não têm como alguém digitar um valor direto aqui por engano.
    // A ÚNICA forma de alterar esses dois valores é via ConfigurarSeries(),
    // chamado pelo MenuUI_Jogo a partir dos Input Fields.
    // ─────────────────────────────────────────────
    private int pedrasPorDescanso = 5;
    private int numeroDeSeries = 3;

    /// <summary>Somente leitura. Para alterar, use ConfigurarSeries().</summary>
    public int PedrasPorDescanso => pedrasPorDescanso;
    /// <summary>Somente leitura. Para alterar, use ConfigurarSeries().</summary>
    public int NumeroDeSeries => numeroDeSeries;

    [Header("Input Fields do Menu (FONTE ÚNICA DE VERDADE)")]
    [Tooltip(
        "Arraste aqui o MESMO Input Field usado no menu do médico para 'Número de Séries'. " +
        "Se preenchido, GerarDegraus() SEMPRE lê o valor direto daqui antes de gerar — " +
        "não importa quem chamou GerarDegraus() ou em que ordem os scripts rodaram."
    )]
    public TMP_InputField inputNumeroDeSeries;
    [Tooltip(
        "Arraste aqui o MESMO Input Field usado no menu do médico para 'Pedras Por Descanso'. " +
        "Se preenchido, GerarDegraus() SEMPRE lê o valor direto daqui antes de gerar."
    )]
    public TMP_InputField inputPedrasPorDescanso;

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

    // true assim que o MenuUI_Jogo chamar ConfigurarSeries() pelo menos uma vez
    [HideInInspector] public bool configuradoPeloMenu = false;

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
    // LÊ OS INPUT FIELDS DIRETAMENTE (fonte única de verdade).
    // Chamado no início de TODA chamada a GerarDegraus(), então não importa
    // quem chamou GerarDegraus() nem em que ordem os Start() rodaram —
    // o valor usado é sempre o que está escrito no campo NESTE momento.
    // ─────────────────────────────────────────────
    void LerValoresDosInputFields()
    {
        if (inputNumeroDeSeries != null)
        {
            if (int.TryParse(inputNumeroDeSeries.text, out int n) && n > 0)
            {
                numeroDeSeries = n;
            }
            else
            {
                Debug.LogWarning(
                    $"[Degrais] Input Field de 'Número de Séries' tem valor inválido " +
                    $"('{inputNumeroDeSeries.text}'). Mantendo o último valor válido: {numeroDeSeries}."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                $"[Degrais] 'Input Numero De Series' não está atribuído neste componente Degrais. " +
                $"Usando fallback: {numeroDeSeries}. Arraste o Input Field do menu no Inspector do Degrais " +
                "para que ele seja sempre a fonte da verdade."
            );
        }

        if (inputPedrasPorDescanso != null)
        {
            if (int.TryParse(inputPedrasPorDescanso.text, out int p) && p > 0)
            {
                pedrasPorDescanso = p;
            }
            else
            {
                Debug.LogWarning(
                    $"[Degrais] Input Field de 'Pedras Por Descanso' tem valor inválido " +
                    $"('{inputPedrasPorDescanso.text}'). Mantendo o último valor válido: {pedrasPorDescanso}."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                $"[Degrais] 'Input Pedras Por Descanso' não está atribuído neste componente Degrais. " +
                $"Usando fallback: {pedrasPorDescanso}. Arraste o Input Field do menu no Inspector do Degrais " +
                "para que ele seja sempre a fonte da verdade."
            );
        }

        configuradoPeloMenu = true;

        Debug.Log(
            $"[Degrais] Valores no momento da geração: pedrasPorDescanso={pedrasPorDescanso}, " +
            $"numeroDeSeries={numeroDeSeries}"
        );
    }

    // ─────────────────────────────────────────────
    // ÚNICA PORTA DE ENTRADA PARA "PEDRAS POR DESCANSO" E "NÚMERO DE SÉRIES"
    // Chamado pelo MenuUI_Jogo a partir dos Input Fields, logo no início do jogo.
    // Serve para o VALOR JÁ FICAR CERTO antes mesmo da primeira geração.
    // A garantia final, porém, é LerValoresDosInputFields() acima.
    // ─────────────────────────────────────────────
    public void ConfigurarSeries(int novoPedrasPorDescanso, int novoNumeroDeSeries)
    {
        if (novoPedrasPorDescanso <= 0 || novoNumeroDeSeries <= 0)
        {
            Debug.LogError(
                $"[Degrais] ConfigurarSeries recebeu valores inválidos " +
                $"(pedrasPorDescanso={novoPedrasPorDescanso}, numeroDeSeries={novoNumeroDeSeries}). " +
                "Os valores precisam ser maiores que zero. Configuração ignorada."
            );
            return;
        }

        pedrasPorDescanso = novoPedrasPorDescanso;
        numeroDeSeries = novoNumeroDeSeries;
        configuradoPeloMenu = true;

        Debug.Log(
            $"[Degrais] Configurado pelo menu: pedrasPorDescanso={pedrasPorDescanso}, " +
            $"numeroDeSeries={numeroDeSeries}"
        );
    }

    // ─────────────────────────────────────────────
    // GERAR PEDRAS
    // ─────────────────────────────────────────────
    public void GerarDegraus()
    {
        LerValoresDosInputFields();

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

            contadorDesdeDescanso++;

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

            // pedras normais seguem a rotação da parede (raycast);
            // pontos de descanso e a plataforma final mantêm a rotação ORIGINAL do prefab
            Quaternion rot = ehPontoDeDescanso
                ? prefabEscolhido.transform.rotation
                : Quaternion.LookRotation(-hit.normal);

            GameObject instancia = Instantiate(prefabEscolhido, posicaoPedra, rot, transform);
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