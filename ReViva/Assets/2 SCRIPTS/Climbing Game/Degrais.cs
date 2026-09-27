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
    [Tooltip("Prefab do ponto de descanso (palanque), instanciado JUNTO com a pedra normal, nunca no lugar dela.")]
    public GameObject pedraDescanso;

    [Tooltip("Prefab da plataforma final (casa). Se ficar vazio, usa o próprio 'Pedra Descanso' na última série.")]
    public GameObject plataformaFinal;

    private int pedrasPorDescanso = 15;
    private int numeroDeSeries = 3;

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
    [Tooltip("Aplica-se APENAS às pedras de escalada. O palanque e a casa nunca passam por esta correção — eles são instanciados exatamente onde o Root manda, sem nenhum ajuste automático.")]
    public bool corrigirProfundidadeZ = true;
    public float alcanceCorrecaoZ = 100f;
    public float folgaCorrecaoZ = 0.05f;

    // =========================================================
    // LADO FIXO DO PALANQUE
    // =========================================================
    //
    // As pedras alternam de lado a cada degrau (zigue-zague):
    // um degrau nasce em centroX + offsetHorizontal, o seguinte
    // em centroX - offsetHorizontal.
    //
    // Como o ponto de descanso pode cair num índice PAR numa
    // série e num índice ÍMPAR na outra, o palanque acabava
    // nascendo ora de um lado, ora do outro — por isso um ficava
    // colado nas pedras e o outro parecia "no meio do caminho".
    //
    // Com 'fixarLadoDoPalanque' ligado, o palanque SEMPRE usa o
    // mesmo lado do zigue-zague, independente do índice em que o
    // ponto de descanso caiu.
    //
    // IMPORTANTE: só o X é trocado. O Y e o Z (a profundidade,
    // já resolvida pelo raycast + correção de profundidade da
    // pedra) permanecem EXATAMENTE os mesmos. E isso vale só
    // para o palanque — a casa (plataforma final) não é afetada
    // por nada disto.
    // =========================================================
    [Header("Lado fixo do palanque")]
    [Tooltip("Se ligado, o palanque sempre nasce do mesmo lado do zigue-zague, em vez de alternar conforme o degrau em que a série terminou. Só altera o eixo X — profundidade (Z) e altura (Y) ficam intactas. Não afeta a casa.")]
    public bool fixarLadoDoPalanque = true;

    [Tooltip("Qual lado do zigue-zague o palanque deve usar sempre. Marque para usar o lado positivo (centroX + distância); desmarque para o lado negativo (centroX - distância).")]
    public bool palanqueNoLadoPositivo = true;

    [Tooltip(
        "Se ligado, o palanque usa a 'Distancia Horizontal Palanque' abaixo em vez do " +
        "'Offset Horizontal' das pedras. Serve para afastar ou aproximar o palanque da " +
        "coluna de pedras sem mexer no zigue-zague das pedras em si."
    )]
    public bool usarDistanciaPropriaDoPalanque = false;

    [Tooltip(
        "Distância horizontal do palanque até o centro do percurso, em metros. " +
        "Só é usada quando 'Usar Distancia Propria Do Palanque' está ligado. " +
        "Valor menor = palanque mais perto do centro (mais perto das pedras do lado oposto); " +
        "valor maior = palanque mais afastado para fora."
    )]
    public float distanciaHorizontalPalanque = 0.30f;

    [Tooltip(
        "Ajuste fino aplicado ao palanque DEPOIS de tudo o mais, em metros. " +
        "X afasta/aproxima na horizontal, Y sobe/desce, Z controla a profundidade " +
        "(negativo puxa para fora da rocha, positivo empurra para dentro). " +
        "Deixe em zero para não alterar nada. Não afeta a casa."
    )]
    public Vector3 ajusteFinoPalanque = Vector3.zero;

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

    // =========================================================
    // NOMES DOS ROOTS DE REFERÊNCIA
    // =========================================================
    //
    // A plataforma final usa "Casa_Root" e o ponto de descanso
    // usa "Palanque_Root" como ponto de referência dentro do
    // prefab. Nenhum dos dois Transforms é movido ou alterado —
    // apenas usados para calcular onde o prefab deve nascer para
    // que esse Transform fique exatamente na posição da pedra
    // daquele degrau.
    //
    // O palanque/casa é um objeto ADICIONAL: a pedra de escalada
    // continua nascendo normalmente naquele ponto.
    //
    // SEM NENHUM AJUSTE AUTOMÁTICO: o palanque/casa não passa
    // por raycast de superfície, nem por correção de profundidade,
    // nem por empurrão para fora da rocha. Ele nasce exatamente
    // onde o Root determina, com a rotação ORIGINAL do prefab.
    // Todo o posicionamento fino é feito por você, movendo o
    // Casa_Root / Palanque_Root dentro do próprio prefab.
    // =========================================================
    const string NomeRootCasa = "Casa_Root";
    const string NomeRootPalanque = "Palanque_Root";

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

            // A correção de profundidade vale SÓ para as pedras de escalada.
            posicaoPedra =
                CorrigirProfundidadeZSeNecessario(posicaoPedra);

            teveAlgumaPedra = true;
            ultimaPosicaoValida = posicaoPedra;

            // =========================================================
            // PEDRA DE ESCALADA — SEMPRE nasce, em todo ponto,
            // inclusive nos pontos de descanso. Ela NUNCA é
            // substituída pelo palanque ou pela casa.
            // =========================================================

            GameObject pedraEscolhida =
                pedras[Random.Range(0, pedras.Length)];

            Instantiate(
                pedraEscolhida,
                posicaoPedra,
                pedraEscolhida.transform.rotation,
                transform
            );

            index++;

            contadorDesdeDescanso++;

            bool ehPontoDeDescanso =
                pedrasPorDescanso > 0 &&
                contadorDesdeDescanso >= pedrasPorDescanso;

            if (!ehPontoDeDescanso)
                continue;

            // =========================================================
            // PALANQUE / CASA — objeto ADICIONAL, instanciado JUNTO
            // com a pedra. Nasce exatamente onde o Root manda, com a
            // rotação ORIGINAL do prefab, sem nenhum ajuste automático.
            // =========================================================

            serieAtual++;

            bool ehUltimaSerie =
                serieAtual >= numeroDeSeries;

            GameObject prefabExtra;
            string nomeRootExtra;

            if (ehUltimaSerie)
            {
                if (plataformaFinal != null)
                {
                    prefabExtra = plataformaFinal;
                    nomeRootExtra = NomeRootCasa;
                }
                else
                {
                    prefabExtra = pedraDescanso;
                    nomeRootExtra = NomeRootPalanque;
                }

                plataformaFinalGerada = prefabExtra != null;
            }
            else
            {
                prefabExtra = pedraDescanso;
                nomeRootExtra = NomeRootPalanque;
            }

            contadorDesdeDescanso = 0;

            if (prefabExtra == null)
            {
                if (ehUltimaSerie)
                    break;

                continue;
            }

            // Só o PALANQUE recebe o lado fixo. A casa (plataforma
            // final) usa a posição da pedra sem nenhuma alteração.
            Vector3 posicaoExtra =
                (nomeRootExtra == NomeRootPalanque)
                ? AplicarLadoFixoDoPalanque(posicaoPedra, centroX)
                : posicaoPedra;

            InstanciarExtraPeloRoot(
                prefabExtra,
                nomeRootExtra,
                posicaoExtra
            );

            if (ehUltimaSerie)
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
                string rootFallback =
                    prefabFinalFallback == plataformaFinal
                    ? NomeRootCasa
                    : NomeRootPalanque;

                // mesma regra: só o palanque recebe o lado fixo
                Vector3 posicaoFallback =
                    (rootFallback == NomeRootPalanque)
                    ? AplicarLadoFixoDoPalanque(ultimaPosicaoValida, posicaoComeco.position.x)
                    : ultimaPosicaoValida;

                InstanciarExtraPeloRoot(
                    prefabFinalFallback,
                    rootFallback,
                    posicaoFallback
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
    // LADO FIXO DO PALANQUE
    // =========================================================
    //
    // Troca APENAS o X da posição, para o lado escolhido do
    // zigue-zague. O Y e o Z são devolvidos exatamente como
    // vieram — ou seja, a profundidade do palanque continua
    // sendo a mesma que a pedra daquele degrau já tinha, depois
    // do raycast e da correção de profundidade.
    //
    // Se 'fixarLadoDoPalanque' estiver desligado, o lado não é
    // travado (comportamento antigo, alternando de lado), mas o
    // 'Ajuste Fino Palanque' continua sendo aplicado.
    //
    // A distância horizontal usada é a das pedras
    // ('Offset Horizontal'), a não ser que
    // 'Usar Distancia Propria Do Palanque' esteja ligado — aí
    // vale a 'Distancia Horizontal Palanque'.
    //
    // Por último, o 'Ajuste Fino Palanque' é somado nos três
    // eixos, para acerto no olho pelo Inspector. Tudo isto vale
    // só para o palanque; a casa não é afetada.
    // =========================================================
    Vector3 AplicarLadoFixoDoPalanque(Vector3 posicaoPedra, float centroX)
    {
        Vector3 resultado = posicaoPedra;

        if (fixarLadoDoPalanque)
        {
            float distancia = usarDistanciaPropriaDoPalanque
                ? distanciaHorizontalPalanque
                : offsetHorizontal;

            float xFixo = palanqueNoLadoPositivo
                ? centroX + distancia
                : centroX - distancia;

            resultado = new Vector3(
                xFixo,
                posicaoPedra.y,
                posicaoPedra.z
            );
        }

        return resultado + ajusteFinoPalanque;
    }

    // =========================================================
    // INSTANCIA O PALANQUE / CASA PELO ROOT
    // =========================================================
    //
    // Calcula onde o prefab deve nascer para que o Root
    // (Casa_Root / Palanque_Root) fique EXATAMENTE na posição
    // informada, e instancia com a rotação ORIGINAL do prefab.
    //
    // Nenhuma alteração automática é feita: sem raycast de
    // superfície, sem correção de profundidade, sem empurrão para
    // fora da rocha, sem mexer na rotação. Se quiser ajustar como
    // o palanque/casa encosta na montanha, mova o Root dentro do
    // próprio prefab.
    // =========================================================

    void InstanciarExtraPeloRoot(
        GameObject prefab,
        string nomeDoRoot,
        Vector3 posicaoAlvo)
    {
        if (prefab == null)
            return;

        // A rotação é SEMPRE a do prefab, nunca a da pedra.
        Quaternion rotacaoOriginal = prefab.transform.rotation;

        Vector3 posicaoInstanciacao =
            CalcularPosicaoInstanciacaoPeloRoot(
                prefab,
                nomeDoRoot,
                posicaoAlvo,
                rotacaoOriginal
            );

        Instantiate(
            prefab,
            posicaoInstanciacao,
            rotacaoOriginal,
            transform
        );
    }

    // =========================================================
    // ROOT DE REFERÊNCIA (Casa_Root / Palanque_Root)
    // =========================================================
    //
    // NÃO altera o Transform do Root procurado.
    //
    // Procura, dentro do prefab, um Transform filho com o nome
    // informado e calcula qual deve ser a posição do objeto raiz
    // do prefab para que esse Transform fique exatamente na
    // posição desejada.
    //
    // Se o Transform não for encontrado, o prefab é instanciado
    // normalmente na posição desejada.
    // =========================================================

    Vector3 CalcularPosicaoInstanciacaoPeloRoot(
        GameObject prefab,
        string nomeDoRoot,
        Vector3 posicaoAlvo,
        Quaternion rotacao)
    {
        if (prefab == null || string.IsNullOrEmpty(nomeDoRoot))
            return posicaoAlvo;

        Transform root = null;

        Transform[] transforms =
            prefab.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in transforms)
        {
            if (t.name == nomeDoRoot)
            {
                root = t;
                break;
            }
        }

        if (root == null)
        {
            Debug.LogWarning(
                $"[Degrais] O prefab '{prefab.name}' não possui " +
                $"um Transform chamado '{nomeDoRoot}'. " +
                "O prefab será instanciado normalmente."
            );

            return posicaoAlvo;
        }

        // Posição do Root em relação ao pivot do prefab,
        // SEM alterar o Root.
        Vector3 rootLocal =
            prefab.transform.InverseTransformPoint(
                root.position
            );

        // Converte o deslocamento local do Root
        // para o espaço do mundo.
        Vector3 deslocamentoNoMundo =
            rotacao *
            Vector3.Scale(
                prefab.transform.localScale,
                rootLocal
            );

        // O prefab nasce deslocado para que o Root
        // fique exatamente na posição desejada.
        return posicaoAlvo -
               deslocamentoNoMundo;
    }

    // ─────────────────────────────────────────────
    // CORREÇÃO DE PROFUNDIDADE (Z) — SÓ PARA AS PEDRAS
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