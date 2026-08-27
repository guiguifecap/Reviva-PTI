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

    private int pedrasPorDescanso = 20;
    private int numeroDeSeries = 4;

    public int PedrasPorDescanso => pedrasPorDescanso;
    public int NumeroDeSeries => numeroDeSeries;

    [Header("Input Fields do Menu (FONTE ÚNICA DE VERDADE)")]
    public TMP_InputField inputNumeroDeSeries;
    public TMP_InputField inputPedrasPorDescanso;

    [Header("Layer da Montanha")]
    public LayerMask layerMontanha;

    [Header("Distância horizontal das pedras")]
    public float offsetHorizontal = 0.30f;

    [Header("Distância do raycast")]
    public float distanciaRaycast = 20f;

    [Header("Correção de profundidade (Z)")]
    public bool corrigirProfundidadeZ = true;
    public float alcanceCorrecaoZ = 100f;
    public float folgaCorrecaoZ = 0.05f;

    [Header("Segurança")]
    public float distanciaVerticalMinima = 0.05f;
    public int maxPedras = 500;

    [Header("Escala Automática da Montanha")]
    public Transform montanha;
    public float escalaMaximaMontanha = 2f;
    public bool escalarApenasEixoY = false;
    public bool moverPosicaoFinalAoEscalar = true;
    public float margemAlturaMontanha = 10f;

    Vector3 escalaOriginalMontanha;
    float groundYOriginal;
    float alturaAcimaDaAncoraOriginal;
    Vector3 anchorLocalNaMontanha;
    Vector3 anchorMundoOriginal;
    Vector3 offsetFinalComecoOriginal;
    bool escalaInicializada = false;

    [HideInInspector] public bool configuradoPeloMenu = false;

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
                $"Usando fallback: {numeroDeSeries}."
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
                $"Usando fallback: {pedrasPorDescanso}."
            );
        }

        configuradoPeloMenu = true;

        Debug.Log(
            $"[Degrais] Valores no momento da geração: pedrasPorDescanso={pedrasPorDescanso}, " +
            $"numeroDeSeries={numeroDeSeries}"
        );
    }

    public void ConfigurarSeries(int novoPedrasPorDescanso, int novoNumeroDeSeries)
    {
        if (novoPedrasPorDescanso <= 0 || novoNumeroDeSeries <= 0)
        {
            Debug.LogError(
                $"[Degrais] ConfigurarSeries recebeu valores inválidos " +
                $"(pedrasPorDescanso={novoPedrasPorDescanso}, numeroDeSeries={novoNumeroDeSeries})."
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

        AjustarEscalaMontanhaSeNecessario(distanciaVertical);

        float y = posicaoComeco.position.y;
        float alturaFinal = posicaoFinal.position.y;

        float centroX = posicaoComeco.position.x;
        float centroZ = posicaoComeco.position.z;

        int index = 0;
        int contadorDesdeDescanso = 0;
        int serieAtual = 0;
        int seguranca = 0;
        bool plataformaFinalGerada = false;

        bool teveAlgumaPedra = false;
        Vector3 ultimaPosicaoValida = Vector3.zero;
        Vector3 ultimaNormalValida = Vector3.up;

        while (y < alturaFinal && seguranca < maxPedras)
        {
            seguranca++;

            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            float x = (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 centroBusca = new Vector3(x, y, centroZ);

            if (!TentarEncontrarSuperficie(centroBusca, out RaycastHit hit))
                continue;

            Vector3 posicaoPedra = new Vector3(x, y, hit.point.z);

            posicaoPedra =
                CorrigirProfundidadeZSeNecessario(posicaoPedra);

            teveAlgumaPedra = true;
            ultimaPosicaoValida = posicaoPedra;
            ultimaNormalValida = hit.normal;

            contadorDesdeDescanso++;

            bool ehPontoDeDescanso =
                pedraDescanso != null &&
                pedrasPorDescanso > 0 &&
                contadorDesdeDescanso >= pedrasPorDescanso;

            GameObject prefabEscolhido;

            if (ehPontoDeDescanso)
            {
                serieAtual++;

                bool ehUltimaSerie =
                    serieAtual >= numeroDeSeries;

                if (ehUltimaSerie)
                {
                    prefabEscolhido =
                        plataformaFinal != null
                        ? plataformaFinal
                        : pedraDescanso;

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
                prefabEscolhido =
                    pedras[Random.Range(0, pedras.Length)];
            }

            Quaternion rot =
                prefabEscolhido.transform.rotation;

            // =========================================================
            // ALTERAÇÃO:
            // Somente a plataforma final usa o Casa_Root como ponto
            // de referência.
            //
            // O Casa_Root NÃO é movido nem alterado.
            // Apenas calculamos onde o prefab deve ser instanciado
            // para que o Casa_Root fique em posicaoPedra.
            // =========================================================

            Vector3 posicaoInstanciacao = posicaoPedra;

            if (plataformaFinalGerada &&
                prefabEscolhido == plataformaFinal)
            {
                posicaoInstanciacao =
                    CalcularPosicaoInstanciacaoPeloCasaRoot(
                        prefabEscolhido,
                        posicaoPedra,
                        rot
                    );
            }

            GameObject instancia =
                Instantiate(
                    prefabEscolhido,
                    posicaoInstanciacao,
                    rot,
                    transform
                );

            index++;

            if (plataformaFinalGerada)
                break;
        }

        // ─────────────────────────────────────────────
        // REDE DE SEGURANÇA DO PONTO FINAL
        // ─────────────────────────────────────────────

        if (!plataformaFinalGerada && teveAlgumaPedra)
        {
            GameObject prefabFinalFallback =
                plataformaFinal != null
                ? plataformaFinal
                : pedraDescanso;

            if (prefabFinalFallback != null)
            {
                Vector3 posFallback =
                    ultimaPosicaoValida +
                    ultimaNormalValida * 0.03f;

                posFallback =
                    CorrigirProfundidadeZSeNecessario(
                        posFallback
                    );

                Quaternion rotFallback =
                    prefabFinalFallback.transform.rotation;

                Vector3 posicaoInstanciacaoFallback =
                    CalcularPosicaoInstanciacaoPeloCasaRoot(
                        prefabFinalFallback,
                        posFallback,
                        rotFallback
                    );

                Instantiate(
                    prefabFinalFallback,
                    posicaoInstanciacaoFallback,
                    rotFallback,
                    transform
                );

                plataformaFinalGerada = true;

                Debug.LogWarning(
                    "[Degrais] Não foi possível completar todas as séries dentro da altura real da montanha. " +
                    "A plataforma final foi colocada na última posição de superfície válida encontrada."
                );
            }
        }

        if (!plataformaFinalGerada)
        {
            Debug.LogWarning(
                $"[Degrais] A geração terminou antes de completar " +
                $"{numeroDeSeries} séries e nenhuma plataforma final foi criada."
            );
        }

        if (seguranca >= maxPedras)
        {
            Debug.LogWarning(
                $"[Degrais] Geração interrompida pelo limite de segurança ({maxPedras} pedras)."
            );
        }

        Debug.Log(
            $"[Degrais] {index} pedras geradas em {serieAtual} série(s)."
        );
    }

    // =========================================================
    // CASA ROOT
    // =========================================================
    //
    // NÃO altera o Transform do Casa_Root.
    //
    // Procura o Casa_Root dentro do prefab e calcula qual deve ser
    // a posição do objeto raiz para que o Casa_Root fique exatamente
    // na posição desejada.
    // =========================================================

    Vector3 CalcularPosicaoInstanciacaoPeloCasaRoot(
        GameObject prefab,
        Vector3 posicaoAlvo,
        Quaternion rotacao)
    {
        if (prefab == null)
            return posicaoAlvo;

        Transform casaRoot = null;

        Transform[] transforms =
            prefab.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in transforms)
        {
            if (t.name == "Casa_Root")
            {
                casaRoot = t;
                break;
            }
        }

        if (casaRoot == null)
        {
            Debug.LogWarning(
                "[Degrais] O prefab da plataforma final não possui " +
                "um Transform chamado 'Casa_Root'. " +
                "A plataforma será instanciada normalmente."
            );

            return posicaoAlvo;
        }

        // Pega a posição do Casa_Root em relação ao pivot
        // do prefab, SEM alterar o Casa_Root.
        Vector3 casaRootLocal =
            prefab.transform.InverseTransformPoint(
                casaRoot.position
            );

        // Converte o deslocamento local do Casa_Root
        // para o espaço do mundo.
        Vector3 deslocamentoCasaRootNoMundo =
            rotacao *
            Vector3.Scale(
                prefab.transform.localScale,
                casaRootLocal
            );

        // O prefab nasce deslocado para que o Casa_Root
        // fique exatamente na posição desejada.
        return posicaoAlvo -
               deslocamentoCasaRootNoMundo;
    }

    // ─────────────────────────────────────────────
    // CORREÇÃO DE PROFUNDIDADE (Z)
    // ─────────────────────────────────────────────

    Vector3 CorrigirProfundidadeZSeNecessario(
        Vector3 posicaoCandidata)
    {
        if (!corrigirProfundidadeZ)
            return posicaoCandidata;

        float alcance =
            Mathf.Max(alcanceCorrecaoZ, 1f);

        Vector3 origemFrente =
            new Vector3(
                posicaoCandidata.x,
                posicaoCandidata.y,
                posicaoCandidata.z + alcance
            );

        Vector3 origemTras =
            new Vector3(
                posicaoCandidata.x,
                posicaoCandidata.y,
                posicaoCandidata.z - alcance
            );

        bool acertouFrente =
            Physics.Raycast(
                origemFrente,
                Vector3.back,
                out RaycastHit hitFrente,
                alcance * 2f,
                layerMontanha
            );

        bool acertouTras =
            Physics.Raycast(
                origemTras,
                Vector3.forward,
                out RaycastHit hitTras,
                alcance * 2f,
                layerMontanha
            );

        if (!acertouFrente && !acertouTras)
            return posicaoCandidata;

        float zFrente =
            acertouFrente
            ? hitFrente.point.z
            : float.PositiveInfinity;

        float zTras =
            acertouTras
            ? hitTras.point.z
            : float.NegativeInfinity;

        if (zTras >= zFrente)
            return posicaoCandidata;

        float z = posicaoCandidata.z;

        bool enterrada =
            z > zTras &&
            z < zFrente;

        if (!enterrada)
            return posicaoCandidata;

        if (!acertouTras)
            return posicaoCandidata;

        float novoZ =
            zTras - folgaCorrecaoZ;

        return new Vector3(
            posicaoCandidata.x,
            posicaoCandidata.y,
            novoZ
        );
    }

    // ─────────────────────────────────────────────
    // ESCALA AUTOMÁTICA DA MONTANHA
    // ─────────────────────────────────────────────

    void AjustarEscalaMontanhaSeNecessario(
        float distanciaVertical)
    {
        if (montanha == null)
            return;

        if (!escalaInicializada)
        {
            escalaOriginalMontanha =
                montanha.localScale;

            Bounds boundsOriginais =
                ObterBoundsMundoMontanha();

            groundYOriginal =
                boundsOriginais.min.y;

            anchorLocalNaMontanha =
                montanha.InverseTransformPoint(
                    posicaoComeco.position
                );

            anchorMundoOriginal =
                posicaoComeco.position;

            offsetFinalComecoOriginal =
                posicaoFinal.position -
                posicaoComeco.position;

            alturaAcimaDaAncoraOriginal =
                boundsOriginais.max.y -
                anchorMundoOriginal.y;

            escalaInicializada = true;
        }

        int totalPedrasEstimado =
            Mathf.Max(pedrasPorDescanso, 1) *
            Mathf.Max(numeroDeSeries, 1);

        float alturaNecessariaSeries =
            distanciaVertical *
            totalPedrasEstimado;

        float alturaNecessariaMontanha =
            alturaNecessariaSeries +
            margemAlturaMontanha;

        float fator =
            alturaAcimaDaAncoraOriginal > 0f
            ? alturaNecessariaMontanha /
              alturaAcimaDaAncoraOriginal
            : 1f;

        fator =
            Mathf.Clamp(
                fator,
                1f,
                escalaMaximaMontanha
            );

        Vector3 fatorPorEixo =
            escalarApenasEixoY
            ? new Vector3(1f, fator, 1f)
            : new Vector3(fator, fator, fator);

        montanha.localScale =
            Vector3.Scale(
                escalaOriginalMontanha,
                fatorPorEixo
            );

        Vector3 offsetAncoraMundo =
            montanha.TransformVector(
                anchorLocalNaMontanha
            );

        montanha.position =
            anchorMundoOriginal -
            offsetAncoraMundo;

        Bounds boundsAtual =
            ObterBoundsMundoMontanha();

        if (boundsAtual.min.y <
            groundYOriginal - 0.01f)
        {
            float ajuste =
                groundYOriginal -
                boundsAtual.min.y;

            montanha.position +=
                new Vector3(0f, ajuste, 0f);

            boundsAtual =
                ObterBoundsMundoMontanha();
        }

        float alturaRealDisponivel =
            boundsAtual.max.y -
            posicaoComeco.position.y;

        float alturaFinalEfetiva =
            Mathf.Min(
                alturaNecessariaSeries,
                Mathf.Max(
                    alturaRealDisponivel - 1f,
                    0f
                )
            );

        Vector3 offsetFinalEscalado =
            Vector3.Scale(
                offsetFinalComecoOriginal,
                fatorPorEixo
            );

        if (moverPosicaoFinalAoEscalar)
        {
            posicaoFinal.position =
                posicaoComeco.position +
                new Vector3(
                    offsetFinalEscalado.x,
                    alturaFinalEfetiva,
                    offsetFinalEscalado.z
                );
        }
        else
        {
            posicaoFinal.position =
                posicaoComeco.position +
                offsetFinalEscalado;
        }

        bool naoCoube =
            alturaRealDisponivel <
            alturaNecessariaMontanha - 0.01f;

        if (naoCoube)
        {
            Debug.LogWarning(
                $"[Degrais] Mesmo escalando a montanha em " +
                $"{escalaMaximaMontanha:F1}x, ela ainda não tem altura suficiente."
            );
        }
        else
        {
            Debug.Log(
                $"[Degrais] Montanha escalada em {fator:F2}x."
            );
        }
    }

    // ─────────────────────────────────────────────
    // BOUNDS REAIS DA MONTANHA
    // ─────────────────────────────────────────────

    Bounds ObterBoundsMundoMontanha()
    {
        Renderer[] renderers =
            montanha.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(
                    renderers[i].bounds
                );
            }

            return b;
        }

        Collider[] colliders =
            montanha.GetComponentsInChildren<Collider>();

        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
            {
                b.Encapsulate(
                    colliders[i].bounds
                );
            }

            return b;
        }

        Debug.LogWarning(
            "[Degrais] Não encontrei Renderer nem Collider em 'Montanha'."
        );

        return new Bounds(
            montanha.position,
            Vector3.zero
        );
    }

    // ─────────────────────────────────────────────
    // LIMPA FILHOS
    // ─────────────────────────────────────────────

    void LimparFilhos()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child =
                transform.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(
                    child.gameObject
                );

                continue;
            }
#endif

            Destroy(child.gameObject);
        }
    }

    // ─────────────────────────────────────────────
    // CALCULA DISTÂNCIA VERTICAL
    // ─────────────────────────────────────────────

    bool TentarObterDistanciaVertical(
        out float distanciaVertical)
    {
        distanciaVertical = 0f;

        if (GameSettings.Instance == null)
        {
            Debug.LogError(
                "[Degrais] GameSettings.Instance é nulo."
            );

            return false;
        }

        float alcanceCM =
            GameSettings.Instance.alcanceMaximoCM;

        if (GameSettings.Instance.usarMetadeDoAlcance)
            alcanceCM *= 0.5f;

        if (alcanceCM <= 0f)
            alcanceCM = 130f;

        float percentual =
            GameSettings.Instance.difficulty;

        float alcanceMetros =
            alcanceCM / 100f;

        distanciaVertical =
            alcanceMetros *
            percentual;

        Debug.Log(
            $"[Degrais] Alcance={alcanceCM:F1}cm  " +
            $"Dificuldade={percentual * 100f:F0}%  " +
            $"Distância={distanciaVertical:F2}m"
        );

        if (distanciaVertical <
            distanciaVerticalMinima)
        {
            Debug.LogError(
                $"[Degrais] Distância vertical calculada " +
                $"({distanciaVertical:F3}m) é menor que o mínimo permitido."
            );

            return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────
    // RAYCASTS 4 DIREÇÕES
    // ─────────────────────────────────────────────

    bool TentarEncontrarSuperficie(
        Vector3 centroBusca,
        out RaycastHit hit)
    {
        hit = new RaycastHit();

        bool achou = false;
        float menorDistancia = float.MaxValue;

        foreach (Vector3 dir in Direcoes)
        {
            Vector3 origem =
                centroBusca - dir * 5f;

            if (Physics.Raycast(
                origem,
                dir,
                out RaycastHit hitAtual,
                distanciaRaycast,
                layerMontanha))
            {
                if (hitAtual.distance <
                    menorDistancia)
                {
                    menorDistancia =
                        hitAtual.distance;

                    hit = hitAtual;
                    achou = true;
                }
            }
        }

        return achou;
    }

    // ─────────────────────────────────────────────
    // DEBUG VISUAL
    // ─────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (posicaoComeco == null ||
            posicaoFinal == null)
            return;

        float alcanceCM = 130f;
        float percentual = 0.8f;

        if (Application.isPlaying &&
            GameSettings.Instance != null)
        {
            alcanceCM =
                GameSettings.Instance.alcanceMaximoCM;

            percentual =
                GameSettings.Instance.difficulty;
        }

        float distanciaVertical =
            (alcanceCM / 100f) *
            percentual;

        if (distanciaVertical <
            distanciaVerticalMinima)
            return;

        float y =
            posicaoComeco.position.y;

        float alturaFinal =
            posicaoFinal.position.y;

        float centroX =
            posicaoComeco.position.x;

        float centroZ =
            posicaoComeco.position.z;

        int index = 0;
        int contadorDesdeDescanso = 0;
        int serieAtual = 0;
        int seguranca = 0;

        while (
            y < alturaFinal &&
            seguranca < maxPedras)
        {
            seguranca++;

            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            float x =
                (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 p =
                new Vector3(
                    x,
                    y,
                    centroZ
                );

            contadorDesdeDescanso++;

            bool ehPontoDeDescanso =
                pedrasPorDescanso > 0 &&
                contadorDesdeDescanso >=
                pedrasPorDescanso;

            bool ehUltimaSerie = false;

            if (ehPontoDeDescanso)
            {
                serieAtual++;

                ehUltimaSerie =
                    numeroDeSeries > 0 &&
                    serieAtual >= numeroDeSeries;

                contadorDesdeDescanso = 0;
            }

            if (ehUltimaSerie)
                Gizmos.color = Color.red;
            else if (ehPontoDeDescanso)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.green;

            float raio =
                ehUltimaSerie
                ? 0.16f
                : (ehPontoDeDescanso
                    ? 0.12f
                    : 0.08f);

            Gizmos.DrawWireSphere(
                p,
                raio
            );

            index++;

            if (ehUltimaSerie)
                break;
        }
    }
}