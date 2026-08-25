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
    [Tooltip(
        "Deslocamento horizontal fixo do zigue-zague das pedras, em metros REAIS. " +
        "Este valor NUNCA muda com a escala da montanha — ele representa o alcance " +
        "físico do paciente, então precisa ficar constante independente da dificuldade."
    )]
    public float offsetHorizontal = 0.30f;

    [Header("Distância do raycast")]
    [Tooltip("Alcance fixo do raycast em metros. Também NUNCA escala com a montanha.")]
    public float distanciaRaycast = 20f;

    [Header("Correção de profundidade (Z)")]
    [Tooltip(
        "Depois de achar a posição da pedra/ponto, o script verifica se ela ficou ENTERRADA " +
        "dentro do volume sólido da montanha (entre a face de trás e a face da frente, no " +
        "mesmo X/Y). Se estiver, empurra ela só no eixo Z, e SOMENTE DIMINUINDO o valor de Z " +
        "(nunca aumentando) até a face de trás mais próxima — X e Y nunca são alterados. " +
        "Nunca empurra pra face da frente, pra não jogar a pedra pro lado errado da montanha. " +
        "Vale igualmente para pedras normais, pontos de descanso e a plataforma final."
    )]
    public bool corrigirProfundidadeZ = true;
    [Tooltip("Distância máxima (bem generosa) usada para lançar os raios de frente/trás na correção de profundidade.")]
    public float alcanceCorrecaoZ = 100f;
    [Tooltip("Pequena folga deixada entre a pedra corrigida e a face de trás da rocha, para não ficar colada exatamente na superfície.")]
    public float folgaCorrecaoZ = 0.05f;

    [Header("Segurança")]
    [Tooltip("Distância vertical mínima aceita entre pedras. Evita loop infinito se a dificuldade/alcance vierem zerados.")]
    public float distanciaVerticalMinima = 0.05f;
    [Tooltip("Número máximo de pedras que o gerador pode tentar criar, como trava de segurança.")]
    public int maxPedras = 500;

    [Header("Escala Automática da Montanha")]
    [Tooltip(
        "Transform da montanha (o objeto com o Collider na layer 'Montanha'). Se atribuído, o Degrais " +
        "aumenta a escala dele automaticamente quando o número de séries/pedras pedido não couber na " +
        "altura real da montanha. Deixe vazio para desativar esse recurso."
    )]
    public Transform montanha;
    [Tooltip("Escala máxima permitida, como múltiplo do tamanho original da montanha (ex: 2 = no máximo o dobro).")]
    public float escalaMaximaMontanha = 2f;
    [Tooltip("Se true, escala só o eixo Y (altura) da montanha. Se false, escala os 3 eixos igualmente.")]
    public bool escalarApenasEixoY = false;
    [Tooltip("Se true, move 'Posicao Final' para a altura exata que as séries pedem (limitada pela altura real da montanha), sempre que a montanha for reajustada.")]
    public bool moverPosicaoFinalAoEscalar = true;
    [Tooltip("A montanha sempre fica pelo menos essa quantidade de metros mais alta que 'Posicao Final'. Ex: 10 → se o ponto final está em 120m, a montanha terá pelo menos 130m de altura.")]
    public float margemAlturaMontanha = 10f;

    // ─────────────────────────────────────────────
    // Dados capturados UMA ÚNICA VEZ, na primeira geração, e usados como
    // base fixa para todo cálculo de escala daí em diante. Não são
    // recapturados a cada chamada, senão a escala iria "derivar" (crescer
    // sem controle) a cada regeneração.
    // ─────────────────────────────────────────────
    Vector3 escalaOriginalMontanha;
    float groundYOriginal;             // altura (Y) da base real da montanha, para a trava de chão
    float alturaAcimaDaAncoraOriginal; // quantos metros de montanha existem ACIMA da âncora, na escala original
    Vector3 anchorLocalNaMontanha;     // coordenada FIXA (espaço local do modelo) do ponto que coincide com 'Posicao Comeco'
    Vector3 anchorMundoOriginal;       // posição no MUNDO que esse ponto deve manter sempre, em qualquer escala
    Vector3 offsetFinalComecoOriginal; // deslocamento horizontal original entre 'Posicao Comeco' e 'Posicao Final'
    bool escalaInicializada = false;

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

        // guarda a última posição de superfície válida encontrada, pra caso as
        // séries não caibam todas dentro da altura da montanha — assim sempre
        // sobra um ponto real onde colocar a plataforma final no fim do loop.
        bool teveAlgumaPedra = false;
        Vector3 ultimaPosicaoValida = Vector3.zero;
        Vector3 ultimaNormalValida = Vector3.up;

        // 'seguranca' garante que o loop termina mesmo que algo
        // inesperado aconteça com 'y' ou 'alturaFinal'
        while (y < alturaFinal && seguranca < maxPedras)
        {
            seguranca++;
            y += distanciaVertical;

            if (y > alturaFinal)
                break;

            // alterna lados — offsetHorizontal é SEMPRE o valor fixo do Inspector,
            // nunca escalado pela montanha, para a distância entre pedras não mudar.
            float x = (index % 2 == 0)
                ? centroX + offsetHorizontal
                : centroX - offsetHorizontal;

            Vector3 centroBusca = new Vector3(x, y, centroZ);

            if (!TentarEncontrarSuperficie(centroBusca, out RaycastHit hit))
                continue;

            // IMPORTANTE: força X e Y a serem SEMPRE os valores exatos da coluna
            // de busca (x, y), nunca o que veio de 'hit.point'. Quando o acerto
            // mais próximo vem de uma direção LATERAL (direita/esquerda), o raio
            // viaja ao longo do eixo X, então 'hit.point.x' é onde a rocha
            // realmente está — não o X pedido — e isso é o que tirava as pedras
            // da linha reta do zigue-zague. Só o Z (profundidade) vem do hit,
            // e mesmo esse Z é só um ponto de partida: é refinado logo abaixo.
            Vector3 posicaoPedra = new Vector3(x, y, hit.point.z);

            // corrige o Z se a pedra caiu enterrada dentro do volume sólido da
            // montanha — só DIMINUI o Z (nunca aumenta), e nunca mexe em X/Y.
            // Vale para pedras normais, pontos de descanso e plataforma final,
            // porque é aplicado aqui antes de qualquer um dos três ser escolhido.
            posicaoPedra = CorrigirProfundidadeZSeNecessario(posicaoPedra);

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

            // TODAS as pedras (normais, descanso e plataforma final) mantêm a
            // rotação ORIGINAL do prefab — nenhuma delas gira mais de acordo
            // com a normal da parede. Isso evita que a pedra vire pro lado
            // errado quando a posição é corrigida pela CorrigirProfundidadeZ.
            Quaternion rot = prefabEscolhido.transform.rotation;

            GameObject instancia = Instantiate(prefabEscolhido, posicaoPedra, rot, transform);
            index++;

            // ao gerar a plataforma final, a geração termina imediatamente
            if (plataformaFinalGerada)
                break;
        }

        // ─── REDE DE SEGURANÇA DO PONTO FINAL ───
        // Se por algum motivo (altura da montanha limitada por 'Escala Maxima
        // Montanha', geometria irregular perto do topo, etc.) as séries não
        // couberam todas e a plataforma final nunca foi colocada, coloca ela
        // na ÚLTIMA posição de superfície válida encontrada. Assim o percurso
        // SEMPRE tem um ponto de chegada visível — nunca "desaparece".
        if (!plataformaFinalGerada && teveAlgumaPedra)
        {
            GameObject prefabFinalFallback = plataformaFinal != null ? plataformaFinal : pedraDescanso;

            if (prefabFinalFallback != null)
            {
                Vector3 posFallback = ultimaPosicaoValida + ultimaNormalValida * 0.03f;
                posFallback = CorrigirProfundidadeZSeNecessario(posFallback);

                Instantiate(prefabFinalFallback, posFallback, prefabFinalFallback.transform.rotation, transform);
                plataformaFinalGerada = true;

                Debug.LogWarning(
                    "[Degrais] Não foi possível completar todas as séries dentro da altura real da montanha. " +
                    "A plataforma final foi colocada na última posição de superfície válida encontrada, " +
                    "para o percurso sempre ter um ponto de chegada visível."
                );
            }
        }

        if (!plataformaFinalGerada)
        {
            Debug.LogWarning(
                $"[Degrais] A geração terminou (altura ou limite de segurança) antes de completar " +
                $"as {numeroDeSeries} séries pedidas, e nenhuma superfície válida foi encontrada em nenhum " +
                $"momento para servir de plataforma final. Séries completas: {serieAtual}. " +
                "Verifique 'layerMontanha', a posição de 'posicaoComeco'/'posicaoFinal' e o collider da montanha."
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
    // CORREÇÃO DE PROFUNDIDADE (Z)
    //
    // Dispara um raio de bem longe na FRENTE (indo pra trás) e outro de bem
    // longe ATRÁS (indo pra frente), sempre no mesmo X/Y da pedra. Isso dá
    // a face externa da frente e a face externa de trás da montanha naquele
    // "poste" vertical. Se o Z da pedra estiver ENTRE essas duas faces, ela
    // está enterrada dentro do volume sólido.
    //
    // A correção SÓ EMPURRA PARA TRÁS (diminui o Z, até a face de trás mais
    // uma pequena folga) — NUNCA empurra para a face da frente. Isso evita
    // que a pedra seja jogada pro lado errado da montanha. Se não houver
    // face de trás detectada, a posição não é alterada (não há como corrigir
    // com segurança sem aumentar o Z). X e Y NUNCA são alterados por esta função.
    // ─────────────────────────────────────────────
    Vector3 CorrigirProfundidadeZSeNecessario(Vector3 posicaoCandidata)
    {
        if (!corrigirProfundidadeZ)
            return posicaoCandidata;

        float alcance = Mathf.Max(alcanceCorrecaoZ, 1f);

        Vector3 origemFrente = new Vector3(posicaoCandidata.x, posicaoCandidata.y, posicaoCandidata.z + alcance);
        Vector3 origemTras = new Vector3(posicaoCandidata.x, posicaoCandidata.y, posicaoCandidata.z - alcance);

        bool acertouFrente = Physics.Raycast(origemFrente, Vector3.back, out RaycastHit hitFrente, alcance * 2f, layerMontanha);
        bool acertouTras = Physics.Raycast(origemTras, Vector3.forward, out RaycastHit hitTras, alcance * 2f, layerMontanha);

        // não achou montanha nesse X/Y por nenhum dos dois lados — nada pra corrigir
        if (!acertouFrente && !acertouTras)
            return posicaoCandidata;

        float zFrente = acertouFrente ? hitFrente.point.z : float.PositiveInfinity;
        float zTras = acertouTras ? hitTras.point.z : float.NegativeInfinity;

        // se as duas faces vieram invertidas (zTras > zFrente), a geometria
        // nesse ponto é ambígua demais para essa correção simples — não mexe.
        if (zTras >= zFrente)
            return posicaoCandidata;

        float z = posicaoCandidata.z;
        bool enterrada = z > zTras && z < zFrente;

        if (!enterrada)
            return posicaoCandidata; // já está do lado de fora, não precisa corrigir

        // só corrige se achou a face de TRÁS — nunca empurra pra frente,
        // pra não jogar a pedra pro lado errado da montanha.
        if (!acertouTras)
            return posicaoCandidata;

        float novoZ = zTras - folgaCorrecaoZ;
        return new Vector3(posicaoCandidata.x, posicaoCandidata.y, novoZ);
    }

    // ─────────────────────────────────────────────
    // ESCALA AUTOMÁTICA DA MONTANHA (até no máximo 'escalaMaximaMontanha')
    //
    // A escala é calculada com base na altura REAL da montanha (medida por
    // bounds) e é ANCORADA em 'Posicao Comeco': depois de mudar o
    // localScale, a montanha é reposicionada para que o ponto do modelo
    // que coincidia com 'Posicao Comeco' continue exatamente no mesmo
    // lugar do mundo. Além disso, 'Posicao Final' NUNCA é colocado acima
    // da altura real que a montanha (já escalada) efetivamente alcança —
    // isso é o que evitava o ponto final "flutuar" acima de onde a rocha
    // termina e sumir.
    // ─────────────────────────────────────────────
    void AjustarEscalaMontanhaSeNecessario(float distanciaVertical)
    {
        if (montanha == null) return; // recurso desativado se nada foi arrastado no campo

        if (!escalaInicializada)
        {
            escalaOriginalMontanha = montanha.localScale;

            Bounds boundsOriginais = ObterBoundsMundoMontanha();
            groundYOriginal = boundsOriginais.min.y;

            anchorLocalNaMontanha = montanha.InverseTransformPoint(posicaoComeco.position);
            anchorMundoOriginal = posicaoComeco.position;
            offsetFinalComecoOriginal = posicaoFinal.position - posicaoComeco.position;

            // quantos metros de montanha existem ACIMA da âncora, na escala original.
            alturaAcimaDaAncoraOriginal = boundsOriginais.max.y - anchorMundoOriginal.y;

            if (alturaAcimaDaAncoraOriginal <= 0f)
            {
                Debug.LogWarning(
                    "[Degrais] 'Posicao Comeco' está no topo ou acima do topo da montanha — não há " +
                    "montanha acima da âncora para escalar. Verifique o posicionamento de 'Posicao Comeco'."
                );
            }

            escalaInicializada = true;
        }

        // altura REAL que o percurso pedido (pedras por série × número de séries) precisa —
        // usa sempre a 'distanciaVertical' e o 'offsetHorizontal' FIXOS, então a dificuldade
        // percebida (distância entre pedras) nunca muda com a escala da montanha.
        int totalPedrasEstimado = Mathf.Max(pedrasPorDescanso, 1) * Mathf.Max(numeroDeSeries, 1);
        float alturaNecessariaSeries = distanciaVertical * totalPedrasEstimado;

        // a montanha precisa ter, acima da âncora, pelo menos essa altura + a margem
        float alturaNecessariaMontanha = alturaNecessariaSeries + margemAlturaMontanha;

        float fator = alturaAcimaDaAncoraOriginal > 0f
            ? alturaNecessariaMontanha / alturaAcimaDaAncoraOriginal
            : 1f;
        fator = Mathf.Clamp(fator, 1f, escalaMaximaMontanha);

        Vector3 fatorPorEixo = escalarApenasEixoY
            ? new Vector3(1f, fator, 1f)
            : new Vector3(fator, fator, fator);

        montanha.localScale = Vector3.Scale(escalaOriginalMontanha, fatorPorEixo);

        // ─── ÂNCORA ───
        // Recoloca a montanha para que o ponto que estava em 'Posicao Comeco'
        // continue exatamente no mesmo lugar do mundo, mesmo depois de crescer.
        Vector3 offsetAncoraMundo = montanha.TransformVector(anchorLocalNaMontanha);
        montanha.position = anchorMundoOriginal - offsetAncoraMundo;

        // ─── TRAVA DE CHÃO ───
        // Segurança extra: se mesmo assim a base real ficar abaixo do chão
        // original (por imprecisão do pivot do modelo), empurra pra cima.
        Bounds boundsAtual = ObterBoundsMundoMontanha();
        if (boundsAtual.min.y < groundYOriginal - 0.01f)
        {
            float ajuste = groundYOriginal - boundsAtual.min.y;
            montanha.position += new Vector3(0f, ajuste, 0f);
            boundsAtual = ObterBoundsMundoMontanha();
        }

        // altura REAL que a montanha (já escalada e reposicionada) efetivamente
        // alcança acima da âncora — pode ser MENOR que 'alturaNecessariaMontanha'
        // se o fator foi limitado por 'escalaMaximaMontanha'.
        float alturaRealDisponivel = boundsAtual.max.y - posicaoComeco.position.y;

        // 'Posicao Final' nunca passa da altura real disponível (com 1m de folga
        // do topo absoluto, pra não cair bem na ponta da geometria). Isso é o que
        // garante que o ponto final NUNCA fique flutuando acima da rocha de verdade.
        float alturaFinalEfetiva = Mathf.Min(alturaNecessariaSeries, Mathf.Max(alturaRealDisponivel - 1f, 0f));

        // 'Posicao Comeco' NUNCA se move — é a âncora fixa do percurso.
        // 'Posicao Final' vai para a altura EFETIVA (limitada pela rocha real),
        // e mantém, na horizontal, a mesma proporção de afastamento que tinha
        // originalmente em relação ao começo.
        Vector3 offsetFinalEscalado = Vector3.Scale(offsetFinalComecoOriginal, fatorPorEixo);

        if (moverPosicaoFinalAoEscalar)
        {
            posicaoFinal.position = posicaoComeco.position + new Vector3(
                offsetFinalEscalado.x,
                alturaFinalEfetiva,
                offsetFinalEscalado.z
            );
        }
        else
        {
            posicaoFinal.position = posicaoComeco.position + offsetFinalEscalado;
        }

        bool naoCoube = alturaRealDisponivel < alturaNecessariaMontanha - 0.01f;

        if (naoCoube)
        {
            Debug.LogWarning(
                $"[Degrais] Mesmo escalando a montanha em {escalaMaximaMontanha:F1}x (o máximo permitido), " +
                $"ela ainda não tem altura suficiente acima de 'Posicao Comeco' " +
                $"(necessário: {alturaNecessariaMontanha:F2}m, disponível: {alturaRealDisponivel:F2}m). " +
                $"'Posicao Final' foi ajustado para {alturaFinalEfetiva:F2}m (dentro da rocha real) em vez dos " +
                $"{alturaNecessariaSeries:F2}m ideais. Considere reduzir 'Pedras Por Descanso', 'Número de Séries', " +
                "'Margem Altura Montanha', ou aumentar 'Escala Maxima Montanha'."
            );
        }
        else
        {
            Debug.Log(
                $"[Degrais] Montanha escalada em {fator:F2}x (limite: {escalaMaximaMontanha:F1}x). " +
                $"Altura disponível acima da âncora: {alturaRealDisponivel:F1}m | " +
                $"Posicao Final a {alturaFinalEfetiva:F1}m | margem: {margemAlturaMontanha:F1}m."
            );
        }
    }

    // ─────────────────────────────────────────────
    // BOUNDS REAIS DA MONTANHA NO MUNDO
    // ─────────────────────────────────────────────
    Bounds ObterBoundsMundoMontanha()
    {
        // combina os bounds de TODOS os renderers da montanha (ela pode ser
        // feita de vários pedaços de mesh) — pegar só o primeiro encontrado
        // mediria apenas um pedaço, subestimando a altura/base real
        Renderer[] renderers = montanha.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        Collider[] colliders = montanha.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                b.Encapsulate(colliders[i].bounds);
            return b;
        }

        Debug.LogWarning(
            "[Degrais] Não encontrei Renderer nem Collider em 'Montanha' (ou seus filhos) para medir a altura/base real. " +
            "A escala automática não poderá ser calculada com precisão."
        );
        return new Bounds(montanha.position, Vector3.zero);
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
    // RAYCASTS 4 DIREÇÕES — pega a superfície MAIS PRÓXIMA, não a primeira.
    //
    // Antes, pegava o primeiro acerto entre as 4 direções testadas. Perto
    // de picos/fendas mais complexos, isso às vezes acertava a parede de
    // DENTRO de uma fenda em vez da face externa da rocha, cravando a
    // pedra no meio da montanha. Pegar o acerto mais próximo do ponto de
    // busca é sempre a face externa mais provável.
    // ─────────────────────────────────────────────
    bool TentarEncontrarSuperficie(Vector3 centroBusca, out RaycastHit hit)
    {
        hit = new RaycastHit();
        bool achou = false;
        float menorDistancia = float.MaxValue;

        foreach (Vector3 dir in Direcoes)
        {
            Vector3 origem = centroBusca - dir * 5f;

            if (Physics.Raycast(origem, dir, out RaycastHit hitAtual, distanciaRaycast, layerMontanha))
            {
                if (hitAtual.distance < menorDistancia)
                {
                    menorDistancia = hitAtual.distance;
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